using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using RncPlatform.Application.Abstractions.ExternalServices;
using RncPlatform.Application.Abstractions.Locking;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Contracts.Responses;
using RncPlatform.Domain.Entities;
using RncPlatform.Domain.Enums;

namespace RncPlatform.Application.Features.Sync.Services;

public class RncSyncService : IRncSyncService
{
    private readonly IRncSnapshotRepository _snapshotRepo;
    private readonly IRncStagingRepository _stagingRepo;
    private readonly ITaxpayerRepository _taxpayerRepo;
    private readonly ISyncJobStateRepository _jobStateRepo;
    private readonly IRncSourceDownloader _downloader;
    private readonly IRncFileParser _parser;
    private readonly IDistributedLockService _lockService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RncSyncService> _logger;

    public RncSyncService(
        IRncSnapshotRepository snapshotRepo,
        IRncStagingRepository stagingRepo,
        ITaxpayerRepository taxpayerRepo,
        ISyncJobStateRepository jobStateRepo,
        IRncSourceDownloader downloader,
        IRncFileParser parser,
        IDistributedLockService lockService,
        IServiceScopeFactory scopeFactory,
        ILogger<RncSyncService> logger)
    {
        _snapshotRepo = snapshotRepo;
        _stagingRepo = stagingRepo;
        _taxpayerRepo = taxpayerRepo;
        _jobStateRepo = jobStateRepo;
        _downloader = downloader;
        _parser = parser;
        _lockService = lockService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<SyncResultDto> RunSyncAsync(CancellationToken cancellationToken = default)
    {
        const string lockResource = "RncPlatform.Sync";
        var lockedBy = Guid.NewGuid().ToString();

        if (!await _lockService.AcquireLockAsync(lockResource, lockedBy, TimeSpan.FromHours(2), cancellationToken))
        {
            _logger.LogWarning("Sync abandonado porque ya hay un proceso en ejecución.");
            return new SyncResultDto { Status = "Skipped - Already Running" };
        }

        var snapshot = new RncSnapshot 
        { 
            Id = Guid.NewGuid(), 
            Status = SnapshotStatus.Running,
            SourceName = "Pending",
            SourceFileName = "Pending",
            SourceUrl = "https://dgii.gov.do" // Valor temporal hasta que el downloader lo use
        };

        try
        {
            _logger.LogInformation("Guardando Snapshot inicial {SnapshotId}...", snapshot.Id);
            await _snapshotRepo.AddAsync(snapshot, cancellationToken);
            _logger.LogInformation("Snapshot inicial guardado. Iniciando descarga...");

            var (filePath, sourceName) = await _downloader.DownloadLatestDataAsync(cancellationToken);
            var fileHash = await _downloader.GetLastFileHashAsync(cancellationToken);

            snapshot.SourceName = sourceName;
            snapshot.SourceFileName = filePath;
            snapshot.FileHash = fileHash;

            var lastSuccess = await _snapshotRepo.GetLatestSuccessfulAsync(cancellationToken);
            if (lastSuccess != null && lastSuccess.FileHash == fileHash)
            {
                snapshot.Status = SnapshotStatus.NoChanges;
                snapshot.CompletedAt = DateTime.UtcNow;
                await _snapshotRepo.UpdateAsync(snapshot, cancellationToken);
                await UpdateJobState(snapshot);
                return new SyncResultDto { SnapshotId = snapshot.Id, Status = "NoChanges" };
            }

            // Staging insertion (batch processing simplificado)
            var batch = new List<RncStaging>();
            await foreach (var item in _parser.ParseFileAsync(filePath, snapshot.Id, cancellationToken))
            {
                batch.Add(item);
                if (batch.Count >= 10000)
                {
                    await _stagingRepo.AddBatchAsync(batch, cancellationToken);
                    snapshot.RecordCount += batch.Count;
                    batch.Clear();
                }
            }
            if (batch.Any())
            {
                await _stagingRepo.AddBatchAsync(batch, cancellationToken);
                snapshot.RecordCount += batch.Count;
            }

            // Comparación y Upsert usando el Bulk MERGE
            var affectedRows = await _stagingRepo.MergeStagingToTaxpayersAsync(snapshot.Id, cancellationToken);
            
            snapshot.Status = SnapshotStatus.Success;
            snapshot.CompletedAt = DateTime.UtcNow;
            snapshot.InsertedCount = affectedRows;
            
            await _snapshotRepo.UpdateAsync(snapshot, cancellationToken);
            
            // Cleanup Staging
            await _stagingRepo.ClearBatchAsync(snapshot.Id, cancellationToken);
            await UpdateJobState(snapshot);

            return new SyncResultDto { SnapshotId = snapshot.Id, Status = "Success", InsertedCount = snapshot.InsertedCount };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico durante la sincronización");
            snapshot.Status = SnapshotStatus.Failed;
            snapshot.ErrorMessage = ex.Message;
            snapshot.CompletedAt = DateTime.UtcNow;
            
            await UpdateSnapshotStatusInNewScope(snapshot, cancellationToken);
            await UpdateJobState(snapshot);
            throw;
        }
        finally
        {
            await _lockService.ReleaseLockAsync(lockResource, lockedBy, cancellationToken);
        }
    }

    private async Task UpdateSnapshotStatusInNewScope(RncSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var scopedRepo = scope.ServiceProvider.GetRequiredService<IRncSnapshotRepository>();
            await scopedRepo.UpdateAsync(snapshot, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo actualizar el estado del snapshot final (shadow error)");
        }
    }

    private async Task UpdateJobState(RncSnapshot snapshot)
    {
        var jobState = new SyncJobState
        {
            JobName = "DailySync",
            LastRunAt = snapshot.StartedAt,
            LastStatus = snapshot.Status.ToString(),
            LastMessage = snapshot.ErrorMessage
        };
        if (snapshot.Status == SnapshotStatus.Success || snapshot.Status == SnapshotStatus.NoChanges)
            jobState.LastSuccessAt = snapshot.CompletedAt;
        else if (snapshot.Status == SnapshotStatus.Failed)
            jobState.LastFailureAt = snapshot.CompletedAt;

        await _jobStateRepo.UpsertStateAsync(jobState, CancellationToken.None);
    }

    public async Task<SyncStatusDto> GetSystemStatusAsync(CancellationToken cancellationToken = default)
    {
        var state = await _jobStateRepo.GetStateAsync("DailySync", cancellationToken);
        if (state == null) return new SyncStatusDto { LastStatus = "Never Run" };

        return new SyncStatusDto
        {
            LastRunAt = state.LastRunAt,
            LastSuccessAt = state.LastSuccessAt,
            LastFailureAt = state.LastFailureAt,
            LastStatus = state.LastStatus,
            LastMessage = state.LastMessage
        };
    }

    public Task<SyncResultDto> ReprocessSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("El reproceso manual se implementará en la siguiente fase de desarrollo.");
    }
}

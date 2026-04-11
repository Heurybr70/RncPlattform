using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RncPlatform.Application.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RncPlatform.Application.Abstractions.ExternalServices;
using RncPlatform.Application.Abstractions.Locking;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Contracts.Responses;
using RncPlatform.Domain.Entities;
using RncPlatform.Domain.Enums;

namespace RncPlatform.Application.Features.Sync.Services;

public class RncSyncService : IRncSyncService
{
    private const string TaxpayerCacheNamespace = "taxpayer";
    private const string TaxpayerSearchCacheNamespace = "taxpayer-search";
    private const string TaxpayerChangeLogCacheNamespace = "taxpayer-change-log";
    private const string SyncStatusCacheNamespace = "sync-status";
    private readonly IRncSnapshotRepository _snapshotRepo;
    private readonly IRncStagingRepository _stagingRepo;
    private readonly ITaxpayerRepository _taxpayerRepo;
    private readonly ISyncJobStateRepository _jobStateRepo;
    private readonly IRncCacheService _cacheService;
    private readonly IRncSourceDownloader _downloader;
    private readonly IRncFileParser _parser;
    private readonly IDistributedLockService _lockService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SyncArchiveOptions _archiveOptions;
    private readonly ILogger<RncSyncService> _logger;

    public RncSyncService(
        IRncSnapshotRepository snapshotRepo,
        IRncStagingRepository stagingRepo,
        ITaxpayerRepository taxpayerRepo,
        ISyncJobStateRepository jobStateRepo,
        IRncCacheService cacheService,
        IRncSourceDownloader downloader,
        IRncFileParser parser,
        IDistributedLockService lockService,
        IServiceScopeFactory scopeFactory,
        IOptions<SyncArchiveOptions> archiveOptions,
        ILogger<RncSyncService> logger)
    {
        _snapshotRepo = snapshotRepo;
        _stagingRepo = stagingRepo;
        _taxpayerRepo = taxpayerRepo;
        _jobStateRepo = jobStateRepo;
        _cacheService = cacheService;
        _downloader = downloader;
        _parser = parser;
        _lockService = lockService;
        _scopeFactory = scopeFactory;
        _archiveOptions = archiveOptions.Value;
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
            snapshot.SourceFileName = Path.GetFileName(filePath);
            snapshot.FileHash = fileHash;
            snapshot.ArchivedFilePath = await ArchiveSourceFileAsync(filePath, sourceName, fileHash, cancellationToken);

            return await ProcessSnapshotFileAsync(snapshot, filePath, allowNoChangesShortCircuit: true, cancellationToken);
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

    public async Task<SyncResultDto> ReprocessSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken = default)
    {
        const string lockResource = "RncPlatform.Sync";
        var lockedBy = Guid.NewGuid().ToString();

        if (!await _lockService.AcquireLockAsync(lockResource, lockedBy, TimeSpan.FromHours(2), cancellationToken))
        {
            _logger.LogWarning("Reproceso abandonado porque ya hay un proceso en ejecución.");
            return new SyncResultDto { Status = "Skipped - Already Running" };
        }

        RncSnapshot? snapshot = null;

        try
        {
            var sourceSnapshot = await _snapshotRepo.GetByIdAsync(snapshotId, cancellationToken);
            if (sourceSnapshot == null)
            {
                return new SyncResultDto
                {
                    SnapshotId = snapshotId,
                    Status = "SnapshotNotFound",
                    ErrorMessage = "No se encontró el snapshot solicitado."
                };
            }

            if (string.IsNullOrWhiteSpace(sourceSnapshot.ArchivedFilePath) || !File.Exists(sourceSnapshot.ArchivedFilePath))
            {
                return new SyncResultDto
                {
                    SnapshotId = snapshotId,
                    Status = "SnapshotArchiveMissing",
                    ErrorMessage = "El archivo archivado del snapshot no está disponible para reproceso."
                };
            }

            snapshot = new RncSnapshot
            {
                Id = Guid.NewGuid(),
                ReprocessedFromSnapshotId = sourceSnapshot.Id,
                Status = SnapshotStatus.Running,
                SourceName = sourceSnapshot.SourceName,
                SourceUrl = sourceSnapshot.SourceUrl,
                SourceFileName = sourceSnapshot.SourceFileName,
                FileHash = sourceSnapshot.FileHash,
                ArchivedFilePath = sourceSnapshot.ArchivedFilePath
            };

            await _snapshotRepo.AddAsync(snapshot, cancellationToken);
            return await ProcessSnapshotFileAsync(snapshot, sourceSnapshot.ArchivedFilePath, allowNoChangesShortCircuit: false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico durante el reproceso del snapshot {SnapshotId}", snapshotId);

            if (snapshot != null)
            {
                snapshot.Status = SnapshotStatus.Failed;
                snapshot.ErrorMessage = ex.Message;
                snapshot.CompletedAt = DateTime.UtcNow;
                await UpdateSnapshotStatusInNewScope(snapshot, cancellationToken);
                await UpdateJobState(snapshot);
            }

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
        await _cacheService.InvalidateNamespaceAsync(SyncStatusCacheNamespace, CancellationToken.None);
    }

    public async Task<SyncStatusDto> GetSystemStatusAsync(CancellationToken cancellationToken = default)
    {
        var namespaceVersion = await _cacheService.GetNamespaceVersionAsync(SyncStatusCacheNamespace, cancellationToken);
        var cacheKey = $"{SyncStatusCacheNamespace}:{namespaceVersion}:job:daily-sync";
        var cached = await _cacheService.GetAsync<SyncStatusDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var state = await _jobStateRepo.GetStateAsync("DailySync", cancellationToken);
        var response = state == null
            ? new SyncStatusDto { LastStatus = "Never Run" }
            : new SyncStatusDto
            {
                LastRunAt = state.LastRunAt,
                LastSuccessAt = state.LastSuccessAt,
                LastFailureAt = state.LastFailureAt,
                LastStatus = state.LastStatus,
                LastMessage = state.LastMessage
            };

        await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5), cancellationToken);
        return response;
    }

    private async Task<SyncResultDto> ProcessSnapshotFileAsync(
        RncSnapshot snapshot,
        string filePath,
        bool allowNoChangesShortCircuit,
        CancellationToken cancellationToken)
    {
        try
        {
            if (allowNoChangesShortCircuit)
            {
                var lastSuccess = await _snapshotRepo.GetLatestSuccessfulAsync(cancellationToken);
                if (lastSuccess != null && lastSuccess.Id != snapshot.Id && lastSuccess.FileHash == snapshot.FileHash)
                {
                    snapshot.Status = SnapshotStatus.NoChanges;
                    snapshot.CompletedAt = DateTime.UtcNow;
                    await _snapshotRepo.UpdateAsync(snapshot, cancellationToken);
                    await UpdateJobState(snapshot);
                    return new SyncResultDto { SnapshotId = snapshot.Id, Status = "NoChanges" };
                }
            }

            snapshot.RecordCount = 0;

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

            var mergeResult = await _stagingRepo.MergeStagingToTaxpayersAsync(snapshot.Id, cancellationToken);

            snapshot.Status = SnapshotStatus.Success;
            snapshot.CompletedAt = DateTime.UtcNow;
            snapshot.InsertedCount = mergeResult.InsertedCount;
            snapshot.UpdatedCount = mergeResult.UpdatedCount;
            snapshot.DeactivatedCount = mergeResult.DeactivatedCount;

            await _snapshotRepo.UpdateAsync(snapshot, cancellationToken);
            await _cacheService.InvalidateNamespaceAsync(TaxpayerCacheNamespace, cancellationToken);
            await _cacheService.InvalidateNamespaceAsync(TaxpayerSearchCacheNamespace, cancellationToken);
            await _cacheService.InvalidateNamespaceAsync(TaxpayerChangeLogCacheNamespace, cancellationToken);
            await UpdateJobState(snapshot);

            return new SyncResultDto
            {
                SnapshotId = snapshot.Id,
                Status = snapshot.ReprocessedFromSnapshotId.HasValue ? "Reprocessed" : "Success",
                InsertedCount = snapshot.InsertedCount,
                UpdatedCount = snapshot.UpdatedCount,
                DeactivatedCount = snapshot.DeactivatedCount
            };
        }
        finally
        {
            await _stagingRepo.ClearBatchAsync(snapshot.Id, cancellationToken);
        }
    }

    private async Task<string> ArchiveSourceFileAsync(string sourceFilePath, string sourceName, string fileHash, CancellationToken cancellationToken)
    {
        var archiveRoot = ResolveArchiveRootPath();
        var sourceFolder = SanitizePathSegment(string.IsNullOrWhiteSpace(sourceName) ? "dgii" : sourceName);
        var fileExtension = Path.GetExtension(sourceFilePath);
        if (string.IsNullOrWhiteSpace(fileExtension))
        {
            fileExtension = ".txt";
        }

        var archiveDirectory = Path.Combine(archiveRoot, sourceFolder);
        Directory.CreateDirectory(archiveDirectory);

        var archivedFilePath = Path.Combine(archiveDirectory, $"{fileHash}{fileExtension}");
        if (File.Exists(archivedFilePath))
        {
            return archivedFilePath;
        }

        await using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        await using var targetStream = new FileStream(archivedFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await sourceStream.CopyToAsync(targetStream, cancellationToken);

        return archivedFilePath;
    }

    private string ResolveArchiveRootPath()
    {
        if (!string.IsNullOrWhiteSpace(_archiveOptions.RootPath))
        {
            return Path.GetFullPath(_archiveOptions.RootPath);
        }

        return Path.Combine(AppContext.BaseDirectory, "data", "rnc-archive");
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Where(ch => !invalidChars.Contains(ch))
            .Select(ch => char.IsWhiteSpace(ch) ? '-' : ch)
            .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(sanitized) ? "dgii" : sanitized;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RncPlatform.Application.Abstractions.Caching;
using RncPlatform.Application.Abstractions.ExternalServices;
using RncPlatform.Application.Abstractions.Locking;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Application.Features.Sync.Services;
using RncPlatform.Domain.Entities;
using RncPlatform.Domain.Enums;

namespace RncPlatform.Application.Tests;

public class RncSyncServiceTests
{
    [Fact]
    public async Task ReprocessSnapshotAsync_WhenSnapshotDoesNotExist_ReturnsSnapshotNotFound()
    {
        var dependencies = new TestDependencies();
        var requestedSnapshotId = Guid.NewGuid();

        dependencies.SnapshotRepository
            .Setup(x => x.GetByIdAsync(requestedSnapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RncSnapshot?)null);

        var service = dependencies.CreateSubject();

        var result = await service.ReprocessSnapshotAsync(requestedSnapshotId);

        Assert.Equal(requestedSnapshotId, result.SnapshotId);
        Assert.Equal("SnapshotNotFound", result.Status);
        Assert.Equal("No se encontró el snapshot solicitado.", result.ErrorMessage);
        dependencies.LockService.Verify(
            x => x.ReleaseLockAsync("RncPlatform.Sync", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReprocessSnapshotAsync_WhenArchivedFileDoesNotExist_ReturnsSnapshotArchiveMissing()
    {
        var dependencies = new TestDependencies();
        var requestedSnapshotId = Guid.NewGuid();

        dependencies.SnapshotRepository
            .Setup(x => x.GetByIdAsync(requestedSnapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RncSnapshot
            {
                Id = requestedSnapshotId,
                Status = SnapshotStatus.Success,
                SourceName = "DGII_RNC",
                SourceUrl = "https://dgii.gov.do",
                SourceFileName = "missing.txt",
                ArchivedFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.txt")
            });

        var service = dependencies.CreateSubject();

        var result = await service.ReprocessSnapshotAsync(requestedSnapshotId);

        Assert.Equal(requestedSnapshotId, result.SnapshotId);
        Assert.Equal("SnapshotArchiveMissing", result.Status);
        Assert.Equal("El archivo archivado del snapshot no está disponible para reproceso.", result.ErrorMessage);
        dependencies.LockService.Verify(
            x => x.ReleaseLockAsync("RncPlatform.Sync", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunSyncAsync_WhenDownloadSucceeds_ArchivesSourceFileAndReturnsSuccess()
    {
        var dependencies = new TestDependencies();
        var rootPath = CreateTemporaryDirectory();

        try
        {
            var sourceFilePath = Path.Combine(rootPath, "dgii-source.txt");
            await File.WriteAllTextAsync(sourceFilePath, "contenido de prueba", CancellationToken.None);

            const string fileHash = "ABC123SYNC";
            RncSnapshot? createdSnapshot = null;

            dependencies.Downloader
                .Setup(x => x.DownloadLatestDataAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((sourceFilePath, "DGII_RNC"));
            dependencies.Downloader
                .Setup(x => x.GetLastFileHashAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(fileHash);
            dependencies.Parser
                .Setup(x => x.ParseFileAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((string _, Guid _, CancellationToken token) => EmptyStagingAsync(token));
            dependencies.SnapshotRepository
                .Setup(x => x.AddAsync(It.IsAny<RncSnapshot>(), It.IsAny<CancellationToken>()))
                .Callback<RncSnapshot, CancellationToken>((snapshot, _) => createdSnapshot = snapshot)
                .Returns(Task.CompletedTask);
            dependencies.StagingRepository
                .Setup(x => x.MergeStagingToTaxpayersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StagingMergeResult
                {
                    InsertedCount = 2,
                    UpdatedCount = 1,
                    DeactivatedCount = 0
                });

            var service = dependencies.CreateSubject(Path.Combine(rootPath, "archive"));

            var result = await service.RunSyncAsync();

            Assert.NotNull(createdSnapshot);
            Assert.Equal("Success", result.Status);
            Assert.Equal(createdSnapshot!.Id, result.SnapshotId);
            Assert.Equal(2, result.InsertedCount);
            Assert.Equal(1, result.UpdatedCount);
            Assert.Equal(0, result.DeactivatedCount);
            Assert.Equal(SnapshotStatus.Success, createdSnapshot.Status);
            Assert.Equal(fileHash, createdSnapshot.FileHash);
            Assert.NotNull(createdSnapshot.ArchivedFilePath);
            Assert.True(File.Exists(createdSnapshot.ArchivedFilePath));
            Assert.Equal(
                Path.Combine(rootPath, "archive", "DGII_RNC", $"{fileHash}.txt"),
                createdSnapshot.ArchivedFilePath);

            dependencies.CacheService.Verify(
                x => x.InvalidateNamespaceAsync("taxpayer", It.IsAny<CancellationToken>()),
                Times.Once);
            dependencies.StagingRepository.Verify(
                x => x.ClearBatchAsync(createdSnapshot.Id, It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task ReprocessSnapshotAsync_WhenArchivedFileExists_ReturnsReprocessed()
    {
        var dependencies = new TestDependencies();
        var rootPath = CreateTemporaryDirectory();

        try
        {
            var archivedFilePath = Path.Combine(rootPath, "snapshot.txt");
            await File.WriteAllTextAsync(archivedFilePath, "contenido archivado", CancellationToken.None);

            var sourceSnapshot = new RncSnapshot
            {
                Id = Guid.NewGuid(),
                Status = SnapshotStatus.Success,
                SourceName = "DGII_RNC",
                SourceUrl = "https://dgii.gov.do",
                SourceFileName = "snapshot.txt",
                FileHash = "HASH-REPROCESS",
                ArchivedFilePath = archivedFilePath
            };

            RncSnapshot? reprocessedSnapshot = null;

            dependencies.SnapshotRepository
                .Setup(x => x.GetByIdAsync(sourceSnapshot.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sourceSnapshot);
            dependencies.SnapshotRepository
                .Setup(x => x.AddAsync(It.IsAny<RncSnapshot>(), It.IsAny<CancellationToken>()))
                .Callback<RncSnapshot, CancellationToken>((snapshot, _) => reprocessedSnapshot = snapshot)
                .Returns(Task.CompletedTask);
            dependencies.Parser
                .Setup(x => x.ParseFileAsync(archivedFilePath, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns((string _, Guid _, CancellationToken token) => EmptyStagingAsync(token));

            var service = dependencies.CreateSubject(Path.Combine(rootPath, "archive"));

            var result = await service.ReprocessSnapshotAsync(sourceSnapshot.Id);

            Assert.NotNull(reprocessedSnapshot);
            Assert.Equal("Reprocessed", result.Status);
            Assert.Equal(reprocessedSnapshot!.Id, result.SnapshotId);
            Assert.Equal(sourceSnapshot.Id, reprocessedSnapshot.ReprocessedFromSnapshotId);
            Assert.Equal(sourceSnapshot.ArchivedFilePath, reprocessedSnapshot.ArchivedFilePath);
            Assert.Equal(sourceSnapshot.FileHash, reprocessedSnapshot.FileHash);
            Assert.Equal(SnapshotStatus.Success, reprocessedSnapshot.Status);
            dependencies.StagingRepository.Verify(
                x => x.ClearBatchAsync(reprocessedSnapshot.Id, It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "RncPlatformTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async IAsyncEnumerable<RncStaging> EmptyStagingAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield break;
    }

    private sealed class TestDependencies
    {
        public Mock<IRncSnapshotRepository> SnapshotRepository { get; } = new();
        public Mock<IRncStagingRepository> StagingRepository { get; } = new();
        public Mock<ITaxpayerRepository> TaxpayerRepository { get; } = new();
        public Mock<ISyncJobStateRepository> JobStateRepository { get; } = new();
        public Mock<IRncCacheService> CacheService { get; } = new();
        public Mock<IRncSourceDownloader> Downloader { get; } = new();
        public Mock<IRncFileParser> Parser { get; } = new();
        public Mock<IDistributedLockService> LockService { get; } = new();
        public Mock<IServiceScopeFactory> ScopeFactory { get; } = new();

        public TestDependencies()
        {
            LockService
                .Setup(x => x.AcquireLockAsync("RncPlatform.Sync", It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            LockService
                .Setup(x => x.ReleaseLockAsync("RncPlatform.Sync", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            SnapshotRepository
                .Setup(x => x.AddAsync(It.IsAny<RncSnapshot>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            SnapshotRepository
                .Setup(x => x.UpdateAsync(It.IsAny<RncSnapshot>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            SnapshotRepository
                .Setup(x => x.GetLatestSuccessfulAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((RncSnapshot?)null);

            StagingRepository
                .Setup(x => x.AddBatchAsync(It.IsAny<IEnumerable<RncStaging>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            StagingRepository
                .Setup(x => x.ClearBatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            StagingRepository
                .Setup(x => x.MergeStagingToTaxpayersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StagingMergeResult());

            JobStateRepository
                .Setup(x => x.UpsertStateAsync(It.IsAny<SyncJobState>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            CacheService
                .Setup(x => x.InvalidateNamespaceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public RncSyncService CreateSubject(string? archiveRootPath = null)
        {
            return new RncSyncService(
                SnapshotRepository.Object,
                StagingRepository.Object,
                TaxpayerRepository.Object,
                JobStateRepository.Object,
                CacheService.Object,
                Downloader.Object,
                Parser.Object,
                LockService.Object,
                ScopeFactory.Object,
                Options.Create(new SyncArchiveOptions { RootPath = archiveRootPath }),
                Mock.Of<ILogger<RncSyncService>>());
        }
    }
}
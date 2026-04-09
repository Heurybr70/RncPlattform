using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RncPlatform.Api.Tests.Infrastructure;
using RncPlatform.Contracts.Responses;
using RncPlatform.Domain.Entities;
using RncPlatform.Domain.Enums;

namespace RncPlatform.Api.Tests;

public class SyncIntegrationTests
{
    [Fact]
    public async Task SyncRunAndReprocess_ShouldPersistArchivedSourceAndReprocessFromIt()
    {
        await using var app = await RncApiTestContext.CreateAsync();
        app.Factory.SyncScenario.Records.Add(new RncApiTestFactory.TestTaxpayerRecord(
            "131313131",
            "ALFA COMPANY",
            Estado: "ACTIVO"));
        app.Factory.SyncScenario.Records.Add(new RncApiTestFactory.TestTaxpayerRecord(
            "141414141",
            "BETA COMPANY",
            Estado: "ACTIVO"));

        await app.Factory.SeedUserAsync("sync-admin", "Password123!", UserRole.Admin);
        var login = await app.Client.LoginAsync("sync-admin", "Password123!");
        app.Client.SetBearerToken(login.Token);

        var syncResponse = await app.Client.PostAsync("/api/v1/admin/sync/run", content: null);
        syncResponse.EnsureSuccessStatusCode();
        var syncResult = await syncResponse.Content.ReadFromJsonAsync<SyncResultDto>();

        Assert.NotNull(syncResult);
        Assert.Equal("Success", syncResult!.Status);
        Assert.Equal(2, syncResult.InsertedCount);

        var snapshot = await app.Factory.ExecuteDbContextAsync(async dbContext =>
            await dbContext.RncSnapshots.AsNoTracking().SingleAsync(x => x.Id == syncResult.SnapshotId));

        Assert.False(string.IsNullOrWhiteSpace(snapshot.ArchivedFilePath));
        Assert.True(File.Exists(snapshot.ArchivedFilePath!));

        var reprocessResponse = await app.Client.PostAsync($"/api/v1/admin/sync/reprocess/{syncResult.SnapshotId}", content: null);
        reprocessResponse.EnsureSuccessStatusCode();
        var reprocessResult = await reprocessResponse.Content.ReadFromJsonAsync<SyncResultDto>();

        Assert.NotNull(reprocessResult);
        Assert.Equal("Reprocessed", reprocessResult!.Status);

        var reprocessedSnapshot = await app.Factory.ExecuteDbContextAsync(async dbContext =>
            await dbContext.RncSnapshots.AsNoTracking().SingleAsync(x => x.Id == reprocessResult.SnapshotId));

        Assert.Equal(syncResult.SnapshotId, reprocessedSnapshot.ReprocessedFromSnapshotId);
        Assert.Equal(snapshot.ArchivedFilePath, reprocessedSnapshot.ArchivedFilePath);
    }

    [Fact]
    public async Task Reprocess_ShouldReturnNotFoundAndConflictForMissingCases()
    {
        await using var app = await RncApiTestContext.CreateAsync();
        await app.Factory.SeedUserAsync("admin-reprocess", "Password123!", UserRole.Admin);
        var login = await app.Client.LoginAsync("admin-reprocess", "Password123!");
        app.Client.SetBearerToken(login.Token);

        var missingSnapshotResponse = await app.Client.PostAsync($"/api/v1/admin/sync/reprocess/{Guid.NewGuid()}", content: null);
        Assert.Equal(HttpStatusCode.NotFound, missingSnapshotResponse.StatusCode);

        var storedSnapshot = await app.Factory.SeedSnapshotAsync(new RncSnapshot
        {
            Status = SnapshotStatus.Success,
            SourceName = "TEST_SOURCE",
            SourceUrl = "https://tests.local/source.zip",
            SourceFileName = "missing.txt",
            ArchivedFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.txt"),
            FileHash = Guid.NewGuid().ToString("N")
        });

        var missingArchiveResponse = await app.Client.PostAsync($"/api/v1/admin/sync/reprocess/{storedSnapshot.Id}", content: null);
        Assert.Equal(HttpStatusCode.Conflict, missingArchiveResponse.StatusCode);
    }
}
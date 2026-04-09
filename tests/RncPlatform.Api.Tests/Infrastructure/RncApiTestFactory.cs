using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using RncPlatform.Api.Startup;
using RncPlatform.Application.Abstractions.ExternalServices;
using RncPlatform.Application.Abstractions.Identity;
using RncPlatform.Application.Abstractions.Locking;
using RncPlatform.Domain.Entities;
using RncPlatform.Domain.Enums;
using RncPlatform.Infrastructure.BackgroundJobs;
using RncPlatform.Infrastructure.Persistence;

namespace RncPlatform.Api.Tests.Infrastructure;

public sealed class RncApiTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"RncPlatformApiTests_{Guid.NewGuid():N}";
    private readonly string _workingRoot = Path.Combine(Path.GetTempPath(), "RncPlatformApiTests", Guid.NewGuid().ToString("N"));

    public RncApiTestFactory()
    {
        Directory.CreateDirectory(_workingRoot);
        SyncScenario = new TestSyncScenario(_workingRoot);
    }

    public TestSyncScenario SyncScenario { get; }

    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RncDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        using (var scope = Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RncDbContext>();
            await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        }

        if (Directory.Exists(_workingRoot))
        {
            Directory.Delete(_workingRoot, recursive: true);
        }
    }

    public async Task<User> SeedUserAsync(
        string username,
        string password,
        UserRole role,
        bool isActive = true,
        int failedLoginAttempts = 0,
        DateTime? lockoutUntil = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RncDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = new User
        {
            Username = username,
            PasswordHash = passwordHasher.HashPassword(password),
            Email = $"{username}@tests.local",
            FullName = username,
            Role = role,
            IsActive = isActive,
            FailedLoginAttempts = failedLoginAttempts,
            LockoutUntil = lockoutUntil,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<RncSnapshot> SeedSnapshotAsync(RncSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RncDbContext>();
        dbContext.RncSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);
        return snapshot;
    }

    public async Task SeedTaxpayersAsync(IEnumerable<Taxpayer> taxpayers, CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RncDbContext>();
        dbContext.Taxpayers.AddRange(taxpayers);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TResult> ExecuteDbContextAsync<TResult>(Func<RncDbContext, Task<TResult>> action, CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RncDbContext>();
        return await action(dbContext);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = BuildConnectionString(),
                ["ConnectionStrings:Valkey"] = string.Empty,
                ["Jwt:SecretKey"] = "integration-tests-secret-key-2026-very-long",
                ["Jwt:Issuer"] = "RncPlatform.IntegrationTests",
                ["Jwt:Audience"] = "RncPlatform.IntegrationClients",
                ["Jwt:ExpiryMinutes"] = "60",
                ["Jwt:RefreshTokenExpiryDays"] = "7",
                ["Security:LoginMaxFailedAttempts"] = "2",
                ["Security:LoginLockoutMinutes"] = "1",
                ["Security:LoginRateLimitPermitLimit"] = "100",
                ["Security:LoginRateLimitWindowMinutes"] = "1",
                ["Worker:Enabled"] = "false",
                ["SyncArchive:RootPath"] = Path.Combine(_workingRoot, "archive"),
                ["Bootstrap:Username"] = string.Empty,
                ["Bootstrap:Password"] = string.Empty,
                ["Bootstrap:Email"] = string.Empty,
                ["Bootstrap:FullName"] = string.Empty,
                ["Bootstrap:Role"] = "Admin",
                ["Bootstrap:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            var workerDescriptors = services
                .Where(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(RncSyncWorker))
                .ToList();

            foreach (var descriptor in workerDescriptors)
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<IRncSourceDownloader>();
            services.RemoveAll<IRncFileParser>();
            services.RemoveAll<IDistributedLockService>();

            services.AddSingleton(SyncScenario);
            services.AddSingleton<IRncSourceDownloader, TestRncSourceDownloader>();
            services.AddSingleton<IRncFileParser, TestRncFileParser>();
            services.AddSingleton<IDistributedLockService, TestDistributedLockService>();
        });
    }

    private string BuildConnectionString()
    {
        return $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
    }

    public sealed class TestSyncScenario
    {
        public TestSyncScenario(string workingRoot)
        {
            WorkingRoot = workingRoot;
        }

        public string WorkingRoot { get; }

        public string SourceName { get; set; } = "TEST_RNC_SOURCE";

        public string SourceFileContents { get; set; } = "contenido de prueba";

        public IList<TestTaxpayerRecord> Records { get; } = new List<TestTaxpayerRecord>();

        public string? LastDownloadedFilePath { get; set; }

        public string? LastHash { get; set; }
    }

    public sealed record TestTaxpayerRecord(
        string Rnc,
        string NombreORazonSocial,
        string? Estado = "ACTIVO",
        string? Cedula = null,
        string? NombreComercial = null,
        string? Categoria = null,
        string? RegimenPago = null,
        string? ActividadEconomica = null,
        string? FechaConstitucion = null);

    private sealed class TestRncSourceDownloader : IRncSourceDownloader
    {
        private readonly TestSyncScenario _scenario;

        public TestRncSourceDownloader(TestSyncScenario scenario)
        {
            _scenario = scenario;
        }

        public async Task<(string FilePath, string SourceName)> DownloadLatestDataAsync(CancellationToken cancellationToken = default)
        {
            var sourceDirectory = Path.Combine(_scenario.WorkingRoot, "source");
            Directory.CreateDirectory(sourceDirectory);

            var filePath = Path.Combine(sourceDirectory, $"{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(filePath, _scenario.SourceFileContents, cancellationToken);

            await using var stream = File.OpenRead(filePath);
            _scenario.LastHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            _scenario.LastDownloadedFilePath = filePath;
            return (filePath, _scenario.SourceName);
        }

        public Task<string> GetLastFileHashAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_scenario.LastHash))
            {
                throw new InvalidOperationException("No hay hash disponible para la fuente de prueba.");
            }

            return Task.FromResult(_scenario.LastHash);
        }
    }

    private sealed class TestRncFileParser : IRncFileParser
    {
        private readonly TestSyncScenario _scenario;

        public TestRncFileParser(TestSyncScenario scenario)
        {
            _scenario = scenario;
        }

        public async IAsyncEnumerable<RncStaging> ParseFileAsync(string filePath, Guid executionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var record in _scenario.Records)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return new RncStaging
                {
                    ExecutionId = executionId,
                    Rnc = record.Rnc,
                    Cedula = record.Cedula,
                    NombreORazonSocial = record.NombreORazonSocial,
                    NombreComercial = record.NombreComercial,
                    Categoria = record.Categoria,
                    RegimenPago = record.RegimenPago,
                    Estado = record.Estado,
                    ActividadEconomica = record.ActividadEconomica,
                    FechaConstitucion = record.FechaConstitucion
                };

                await Task.Yield();
            }
        }
    }

    private sealed class TestDistributedLockService : IDistributedLockService
    {
        public Task<bool> AcquireLockAsync(string resource, string lockedBy, TimeSpan expiration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task ReleaseLockAsync(string resource, string lockedBy, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
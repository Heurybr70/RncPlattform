using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RncPlatform.Application.Abstractions.Caching;
using RncPlatform.Application.Abstractions.ExternalServices;
using RncPlatform.Application.Abstractions.Locking;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Infrastructure.BackgroundJobs;
using RncPlatform.Infrastructure.Caching;
using RncPlatform.Infrastructure.External.Dgii;
using RncPlatform.Infrastructure.Locking;
using RncPlatform.Infrastructure.Persistence;
using RncPlatform.Infrastructure.Persistence.Repositories;

namespace RncPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RncDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(RncDbContext).Assembly.FullName)));

        // Redis for Caching
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Valkey");
            options.InstanceName = "RncPlatform_";
        });

        // Repositories
        services.AddScoped<ITaxpayerRepository, TaxpayerRepository>();
        services.AddScoped<IRncSnapshotRepository, RncSnapshotRepository>();
        services.AddScoped<IRncChangeLogRepository, RncChangeLogRepository>();
        services.AddScoped<ISyncJobStateRepository, SyncJobStateRepository>();
        services.AddScoped<IRncStagingRepository, RncStagingRepository>();

        // Services
        services.AddScoped<IRncCacheService, RncCacheService>();
        services.AddScoped<IDistributedLockService, DistributedLockService>();
        services.AddScoped<IRncFileParser, RncFileParser>();
        
        services.AddHttpClient<IRncSourceDownloader, DgiiRncDownloader>();

        // Background worker
        services.AddHostedService<RncSyncWorker>();

        return services;
    }
}

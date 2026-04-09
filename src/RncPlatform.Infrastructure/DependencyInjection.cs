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
using RncPlatform.Application.Abstractions.Identity;
using RncPlatform.Application.Features.Sync.Services;
using RncPlatform.Infrastructure.Identity;

namespace RncPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SyncArchiveOptions>(configuration.GetSection("SyncArchive"));

        var defaultConnectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(defaultConnectionString))
        {
            throw new InvalidOperationException("No se configuro ConnectionStrings__DefaultConnection para RncPlatform.");
        }

        services.AddDbContext<RncDbContext>(options =>
            options.UseSqlServer(
                defaultConnectionString,
                b => b.MigrationsAssembly(typeof(RncDbContext).Assembly.FullName)));

        // Redis for Caching with Memory Fallback
        var redisConnectionString = configuration.GetConnectionString("Valkey");
        if (string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "RncPlatform_";
            });
        }

        // Repositories
        services.AddScoped<ITaxpayerRepository, TaxpayerRepository>();
        services.AddScoped<IRncSnapshotRepository, RncSnapshotRepository>();
        services.AddScoped<IRncChangeLogRepository, RncChangeLogRepository>();
        services.AddScoped<ISyncJobStateRepository, SyncJobStateRepository>();
        services.AddScoped<IRncStagingRepository, RncStagingRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Services
        services.AddScoped<IRncCacheService, RncCacheService>();
        services.AddScoped<IDistributedLockService, DistributedLockService>();
        services.AddScoped<IRncFileParser, RncFileParser>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtProvider, JwtProvider>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        
        services.AddHttpClient<IRncSourceDownloader, DgiiRncDownloader>();

        // Background worker
        services.AddHostedService<RncSyncWorker>();

        return services;
    }
}

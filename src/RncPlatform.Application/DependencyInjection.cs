using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RncPlatform.Application.Features.Rncs.Services;
using RncPlatform.Application.Features.Sync.Services;

namespace RncPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRncQueryService, RncQueryService>();
        services.AddScoped<IRncSyncService, RncSyncService>();

        return services;
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RncPlatform.Application.Features.Sync.Services;

namespace RncPlatform.Infrastructure.BackgroundJobs;

public class RncSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RncSyncWorker> _logger;
    private readonly IConfiguration _configuration;

    public RncSyncWorker(IServiceProvider serviceProvider, ILogger<RncSyncWorker> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("Worker:Enabled", true);
        var runHourUtc = _configuration.GetValue<int>("Worker:RunHourUtc", 4); // 4 AM UTC default

        _logger.LogInformation("RncSyncWorker iniciado. Habilitado: {Enabled}, Hora UTC objetivo: {RunHour}", enabled, runHourUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (enabled)
            {
                var now = DateTime.UtcNow;
                // Calculamos si debemos correr ahora 
                // Para una ejecución MVP en el background worker, esperaremos a la hora especificada.
                // En pruebas, lo podemos ajustar.
                if (now.Hour == runHourUtc && now.Minute < 10) 
                {
                    _logger.LogInformation("Ejecutando Sincronización Automática Programada por el Worker...");
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var syncService = scope.ServiceProvider.GetRequiredService<IRncSyncService>();
                        await syncService.RunSyncAsync(stoppingToken);
                        
                        // Wait out the hour to avoid multiple triggers
                        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error no manejado en la sincronización del Worker");
                    }
                }
            }

            // Poll every 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}

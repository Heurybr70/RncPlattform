using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RncPlatform.Application.Features.Sync.Services;
using RncPlatform.Contracts.Responses;
using Swashbuckle.AspNetCore.Annotations;

namespace RncPlatform.Api.Controllers;

/// <summary>
/// Expone informacion operacional de la API.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class SystemController : ControllerBase
{
    private readonly IRncSyncService _syncService;

    public SystemController(IRncSyncService syncService)
    {
        _syncService = syncService;
    }

    /// <summary>
    /// Obtiene el ultimo estado registrado del proceso de sincronizacion diaria.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    [HttpGet("sync-status")]
    [ProducesResponseType(typeof(SyncStatusDto), StatusCodes.Status200OK)]
    [SwaggerOperation(Summary = "Consultar estado de sincronizacion", Description = "Devuelve el ultimo estado persistido del job DailySync, incluyendo fechas de ultimo exito o ultimo fallo.")]
    public async Task<IActionResult> GetSyncStatus(CancellationToken cancellationToken)
    {
        var status = await _syncService.GetSystemStatusAsync(cancellationToken);
        return Ok(status);
    }
}

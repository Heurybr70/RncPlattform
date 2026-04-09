using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RncPlatform.Application.Abstractions.Identity;
using RncPlatform.Application.Features.Sync.Services;
using RncPlatform.Contracts.Responses;
using Swashbuckle.AspNetCore.Annotations;

namespace RncPlatform.Api.Controllers;

/// <summary>
/// Expone operaciones administrativas de sincronizacion y reproceso del padron.
/// </summary>
[Authorize(Policy = IdentityConstants.CanRunSyncPolicy)]
[EnableRateLimiting(IdentityConstants.AdminSyncRateLimitPolicy)]
[ApiController]
[Route("api/v1/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IRncSyncService _syncService;

    public AdminController(IRncSyncService syncService)
    {
        _syncService = syncService;
    }

    /// <summary>
    /// Ejecuta una sincronizacion manual contra la fuente externa configurada.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    [HttpPost("sync/run")]
    [ProducesResponseType(typeof(SyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [SwaggerOperation(Summary = "Ejecutar sync manual", Description = "Descarga el padron DGII, archiva el archivo fuente, procesa staging y devuelve el resultado consolidado del snapshot.")]
    public async Task<IActionResult> RunSync(CancellationToken cancellationToken)
    {
        // En una app real, esto podría lanzar un worker background (Fire&Forget) o devolver 202 Accepted.
        // Como MVP bloqueamos y retornamos el resultado (el archivo TXT parseado en 1 millón a staging y upsert).
        var result = await _syncService.RunSyncAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reprocesa un snapshot usando su archivo archivado previamente.
    /// </summary>
    /// <param name="snapshotId">Identificador del snapshot origen.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    [HttpPost("sync/reprocess/{snapshotId}")]
    [ProducesResponseType(typeof(SyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(SyncResultDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(SyncResultDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [SwaggerOperation(Summary = "Reprocesar snapshot", Description = "Reutiliza el archivo archivado de un snapshot previo para reconstruir el estado mediante un nuevo proceso de merge.")]
    public async Task<IActionResult> ReprocessSnapshot(Guid snapshotId, CancellationToken cancellationToken)
    {
        var result = await _syncService.ReprocessSnapshotAsync(snapshotId, cancellationToken);

        if (string.Equals(result.Status, "SnapshotNotFound", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(result);
        }

        if (string.Equals(result.Status, "SnapshotArchiveMissing", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(result);
        }

        return Ok(result);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RncPlatform.Application.Features.Sync.Services;
using RncPlatform.Contracts.Responses;

namespace RncPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IRncSyncService _syncService;

    public AdminController(IRncSyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpPost("sync/run")]
    [ProducesResponseType(typeof(SyncResultDto), 200)]
    public async Task<IActionResult> RunSync(CancellationToken cancellationToken)
    {
        // En una app real, esto podría lanzar un worker background (Fire&Forget) o devolver 202 Accepted.
        // Como MVP bloqueamos y retornamos el resultado (el archivo TXT parseado en 1 millón a staging y upsert).
        var result = await _syncService.RunSyncAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("sync/reprocess/{snapshotId}")]
    [ProducesResponseType(typeof(SyncResultDto), 200)]
    public async Task<IActionResult> ReprocessSnapshot(Guid snapshotId, CancellationToken cancellationToken)
    {
        var result = await _syncService.ReprocessSnapshotAsync(snapshotId, cancellationToken);
        return Ok(result);
    }
}

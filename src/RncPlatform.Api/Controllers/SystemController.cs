using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RncPlatform.Application.Features.Sync.Services;
using RncPlatform.Contracts.Responses;

namespace RncPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SystemController : ControllerBase
{
    private readonly IRncSyncService _syncService;

    public SystemController(IRncSyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpGet("sync-status")]
    [ProducesResponseType(typeof(SyncStatusDto), 200)]
    public async Task<IActionResult> GetSyncStatus(CancellationToken cancellationToken)
    {
        var status = await _syncService.GetSystemStatusAsync(cancellationToken);
        return Ok(status);
    }
}

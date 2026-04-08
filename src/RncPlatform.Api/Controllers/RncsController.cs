using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using RncPlatform.Application.Features.Rncs.Services;
using RncPlatform.Contracts.Requests;
using RncPlatform.Contracts.Responses;

namespace RncPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class RncsController : ControllerBase
{
    private readonly IRncQueryService _queryService;

    public RncsController(IRncQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet("{rnc}")]
    [ProducesResponseType(typeof(TaxpayerDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByRnc(string rnc, CancellationToken cancellationToken)
    {
        var dto = await _queryService.GetByRncAsync(rnc, cancellationToken);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<TaxpayerSearchItemDto>), 200)]
    public async Task<IActionResult> Search([FromQuery] string term, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return BadRequest("El término de búsqueda es requerido.");
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _queryService.SearchAsync(term, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{rnc}/changes")]
    [ProducesResponseType(typeof(System.Collections.Generic.IEnumerable<TaxpayerChangeDto>), 200)]
    public async Task<IActionResult> GetChanges(string rnc, CancellationToken cancellationToken)
    {
        var result = await _queryService.GetChangesByRncAsync(rnc, cancellationToken);
        return Ok(result);
    }
}

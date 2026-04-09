using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RncPlatform.Application.Abstractions.Identity;
using RncPlatform.Application.Features.Rncs.Services;
using RncPlatform.Contracts.Requests;
using RncPlatform.Contracts.Responses;
using Swashbuckle.AspNetCore.Annotations;

namespace RncPlatform.Api.Controllers;

/// <summary>
/// Expone consultas autenticadas sobre el padron RNC sincronizado.
/// </summary>
[Authorize]
[EnableRateLimiting(IdentityConstants.RncReadRateLimitPolicy)]
[ApiController]
[Route("api/v1/[controller]")]
public class RncsController : ControllerBase
{
    private readonly IRncQueryService _queryService;

    public RncsController(IRncQueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>
    /// Obtiene un contribuyente por RNC exacto.
    /// </summary>
    /// <param name="rnc">RNC a consultar.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    [HttpGet("{rnc}")]
    [ProducesResponseType(typeof(TaxpayerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [SwaggerOperation(Summary = "Consultar RNC exacto", Description = "Devuelve el registro completo del contribuyente encontrado por RNC exacto.")]
    public async Task<IActionResult> GetByRnc(string rnc, CancellationToken cancellationToken)
    {
        var dto = await _queryService.GetByRncAsync(rnc, cancellationToken);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    /// <summary>
    /// Busca contribuyentes por RNC exacto o por prefijo de nombre legal o comercial.
    /// </summary>
    /// <param name="term">Termino de busqueda. Si parece un RNC numerico, se ejecuta como coincidencia exacta.</param>
    /// <param name="page">Numero de pagina para el modo offset.</param>
    /// <param name="pageSize">Tamano de pagina. La API aplica un rango efectivo de 1 a 100.</param>
    /// <param name="cursor">Cursor opcional para paginacion aditiva. En la primera pagina envie el parametro vacio: <c>cursor=</c>.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<TaxpayerSearchItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [SwaggerOperation(Summary = "Buscar contribuyentes", Description = "Soporta paginacion por page/pageSize o por cursor. Para terminos de texto exige al menos 3 caracteres y usa busqueda por prefijo sobre nombre legal y nombre comercial.")]
    public async Task<IActionResult> Search([FromQuery] string term, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        term = term.Trim();
        var isCursorMode = Request.Query.ContainsKey("cursor");
        cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim();

        if (string.IsNullOrWhiteSpace(term))
        {
            return BadRequest("El término de búsqueda es requerido.");
        }

        if (!LooksLikeExactRnc(term) && term.Length < 3)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Término inválido",
                detail: "Para búsqueda por nombre debe indicar al menos 3 caracteres.");
        }

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        if (isCursorMode)
        {
            page = 1;
            cursor ??= string.Empty;
        }

        var result = await _queryService.SearchAsync(term, page, pageSize, cursor, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Devuelve el historial de cambios detectados para un RNC a traves de snapshots sucesivos.
    /// </summary>
    /// <param name="rnc">RNC a consultar.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    [HttpGet("{rnc}/changes")]
    [ProducesResponseType(typeof(System.Collections.Generic.IEnumerable<TaxpayerChangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [SwaggerOperation(Summary = "Consultar changelog por RNC", Description = "Expone la traza historica de cambios registrada durante las sincronizaciones del padron.")]
    public async Task<IActionResult> GetChanges(string rnc, CancellationToken cancellationToken)
    {
        var result = await _queryService.GetChangesByRncAsync(rnc, cancellationToken);
        return Ok(result);
    }

    private static bool LooksLikeExactRnc(string term)
    {
        var digits = new string(term.Where(char.IsDigit).ToArray());
        return digits.Length is >= 9 and <= 20
            && term.All(ch => char.IsDigit(ch) || ch == '-' || char.IsWhiteSpace(ch));
    }
}

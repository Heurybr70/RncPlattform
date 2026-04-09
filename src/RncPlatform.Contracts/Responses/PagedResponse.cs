using System.Collections.Generic;

namespace RncPlatform.Contracts.Responses;

/// <summary>
/// Contrato generico para respuestas paginadas por offset o cursor.
/// </summary>
public class PagedResponse<T>
{
    /// <summary>
    /// Elementos devueltos en la pagina actual.
    /// </summary>
    public IEnumerable<T> Items { get; set; } = new List<T>();

    /// <summary>
    /// Total de resultados que cumplen el filtro.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Numero de pagina en modo offset. En modo cursor la API conserva el valor 1 por compatibilidad.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Tamano de pagina solicitado o ajustado por la API.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Cursor para solicitar la siguiente pagina en modo seek. Sera nulo cuando no existan mas resultados.
    /// </summary>
    public string? NextCursor { get; set; }
}

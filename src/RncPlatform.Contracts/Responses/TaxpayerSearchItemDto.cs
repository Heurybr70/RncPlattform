namespace RncPlatform.Contracts.Responses;

/// <summary>
/// Item resumido de un resultado de busqueda de contribuyentes.
/// </summary>
public class TaxpayerSearchItemDto
{
    /// <summary>
    /// Numero de RNC del contribuyente.
    /// </summary>
    public string Rnc { get; set; } = default!;

    /// <summary>
    /// Nombre o razon social principal.
    /// </summary>
    public string NombreORazonSocial { get; set; } = default!;

    /// <summary>
    /// Nombre comercial reportado, si existe.
    /// </summary>
    public string? NombreComercial { get; set; }

    /// <summary>
    /// Estado del contribuyente en la fuente.
    /// </summary>
    public string? Estado { get; set; }

    /// <summary>
    /// Indica si el registro sigue activo en el ultimo snapshot.
    /// </summary>
    public bool IsActive { get; set; }
}

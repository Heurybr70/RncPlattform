using System;

namespace RncPlatform.Contracts.Responses;

/// <summary>
/// Representa el detalle completo de un contribuyente consultado por RNC.
/// </summary>
public class TaxpayerDto
{
    /// <summary>
    /// Numero de RNC del contribuyente.
    /// </summary>
    public string Rnc { get; set; } = default!;

    /// <summary>
    /// Cedula asociada cuando la fuente la provee.
    /// </summary>
    public string? Cedula { get; set; }

    /// <summary>
    /// Nombre o razon social principal.
    /// </summary>
    public string NombreORazonSocial { get; set; } = default!;

    /// <summary>
    /// Nombre comercial reportado por la fuente.
    /// </summary>
    public string? NombreComercial { get; set; }

    /// <summary>
    /// Categoria tributaria o societaria.
    /// </summary>
    public string? Categoria { get; set; }

    /// <summary>
    /// Regimen de pago reportado.
    /// </summary>
    public string? RegimenPago { get; set; }

    /// <summary>
    /// Estado actual del contribuyente en la fuente sincronizada.
    /// </summary>
    public string? Estado { get; set; }

    /// <summary>
    /// Actividad economica asociada.
    /// </summary>
    public string? ActividadEconomica { get; set; }

    /// <summary>
    /// Fecha de constitucion reportada por la fuente, si aplica.
    /// </summary>
    public string? FechaConstitucion { get; set; }

    /// <summary>
    /// Indica si el contribuyente sigue activo en el ultimo snapshot procesado.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Fecha UTC de remocion logica de la fuente, si el registro ya no aparece en el ultimo snapshot.
    /// </summary>
    public DateTime? RemovedAt { get; set; }
}

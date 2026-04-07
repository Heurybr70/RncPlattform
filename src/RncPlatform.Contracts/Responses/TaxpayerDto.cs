using System;

namespace RncPlatform.Contracts.Responses;

public class TaxpayerDto
{
    public string Rnc { get; set; } = default!;
    public string? Cedula { get; set; }
    public string NombreORazonSocial { get; set; } = default!;
    public string? NombreComercial { get; set; }
    public string? Categoria { get; set; }
    public string? RegimenPago { get; set; }
    public string? Estado { get; set; }
    public string? ActividadEconomica { get; set; }
    public string? FechaConstitucion { get; set; }
    public bool IsActive { get; set; }
    public DateTime? RemovedAt { get; set; }
}

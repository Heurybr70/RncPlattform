using System;

namespace RncPlatform.Domain.Entities;

public class RncStaging
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExecutionId { get; set; }
    public string Rnc { get; set; } = default!;
    public string? Cedula { get; set; }
    public string NombreORazonSocial { get; set; } = default!;
    public string? NombreComercial { get; set; }
    public string? Categoria { get; set; }
    public string? RegimenPago { get; set; }
    public string? Estado { get; set; }
    public string? ActividadEconomica { get; set; }
    public string? FechaConstitucion { get; set; }
}

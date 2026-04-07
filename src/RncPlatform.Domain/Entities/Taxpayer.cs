using System;

namespace RncPlatform.Domain.Entities;

public class Taxpayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Rnc { get; set; } = default!;
    public string? Cedula { get; set; }
    public string NombreORazonSocial { get; set; } = default!;
    public string? NombreComercial { get; set; }
    public string? Categoria { get; set; }
    public string? RegimenPago { get; set; }
    public string? Estado { get; set; }
    public string? ActividadEconomica { get; set; }
    public string? FechaConstitucion { get; set; }
    public bool IsActiveInLatestSnapshot { get; set; }
    public DateTime SourceFirstSeenAt { get; set; }
    public DateTime SourceLastSeenAt { get; set; }
    public DateTime? SourceRemovedAt { get; set; }
    public Guid LastSnapshotId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public byte[]? RowVersion { get; set; }
}

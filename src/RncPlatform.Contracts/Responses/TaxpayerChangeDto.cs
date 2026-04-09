using System;

namespace RncPlatform.Contracts.Responses;

/// <summary>
/// Representa un cambio historico detectado para un RNC durante una sincronizacion.
/// </summary>
public class TaxpayerChangeDto
{
    /// <summary>
    /// Identificador del cambio registrado.
    /// </summary>
    public Guid ChangeId { get; set; }

    /// <summary>
    /// Snapshot en el que se detecto el cambio.
    /// </summary>
    public Guid SnapshotId { get; set; }

    /// <summary>
    /// Tipo de cambio, por ejemplo Added, Updated o Removed.
    /// </summary>
    public string ChangeType { get; set; } = default!;

    /// <summary>
    /// Fecha y hora UTC en la que se detecto el cambio.
    /// </summary>
    public DateTime DetectedAt { get; set; }

    /// <summary>
    /// Valores anteriores serializados en JSON cuando aplica.
    /// </summary>
    public string? OldValuesJson { get; set; }

    /// <summary>
    /// Valores nuevos serializados en JSON cuando aplica.
    /// </summary>
    public string? NewValuesJson { get; set; }
}

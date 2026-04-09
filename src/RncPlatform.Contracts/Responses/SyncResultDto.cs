using System;

namespace RncPlatform.Contracts.Responses;

/// <summary>
/// Resultado de una ejecucion de sincronizacion o reproceso.
/// </summary>
public class SyncResultDto
{
    /// <summary>
    /// Identificador del snapshot generado o afectado por la operacion.
    /// </summary>
    public Guid SnapshotId { get; set; }

    /// <summary>
    /// Cantidad de registros insertados.
    /// </summary>
    public int InsertedCount { get; set; }

    /// <summary>
    /// Cantidad de registros actualizados.
    /// </summary>
    public int UpdatedCount { get; set; }

    /// <summary>
    /// Cantidad de registros desactivados por ausencia en el snapshot fuente.
    /// </summary>
    public int DeactivatedCount { get; set; }

    /// <summary>
    /// Estado textual de la operacion, por ejemplo Success, NoChanges o Reprocessed.
    /// </summary>
    public string Status { get; set; } = default!;

    /// <summary>
    /// Mensaje de error o detalle adicional cuando aplica.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

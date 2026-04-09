using System;

namespace RncPlatform.Contracts.Responses;

/// <summary>
/// Estado consolidado del ultimo job de sincronizacion registrado.
/// </summary>
public class SyncStatusDto
{
    /// <summary>
    /// Fecha y hora UTC de la ultima ejecucion registrada.
    /// </summary>
    public DateTime LastRunAt { get; set; }

    /// <summary>
    /// Fecha y hora UTC del ultimo exito registrado.
    /// </summary>
    public DateTime? LastSuccessAt { get; set; }

    /// <summary>
    /// Fecha y hora UTC del ultimo fallo registrado.
    /// </summary>
    public DateTime? LastFailureAt { get; set; }

    /// <summary>
    /// Estado textual mas reciente del job.
    /// </summary>
    public string? LastStatus { get; set; }

    /// <summary>
    /// Mensaje adicional asociado al ultimo estado.
    /// </summary>
    public string? LastMessage { get; set; }
}

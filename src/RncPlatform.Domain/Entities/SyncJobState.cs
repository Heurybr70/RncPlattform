using System;

namespace RncPlatform.Domain.Entities;

public class SyncJobState
{
    // Usaremos el JobName como clave primaria si quieres, pero por EF será convención. Clave primaria JobName:
    public string JobName { get; set; } = default!;
    public DateTime LastRunAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public string? LastStatus { get; set; }
    public string? LastMessage { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

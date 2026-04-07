using System;

namespace RncPlatform.Domain.Entities;

public class RncChangeLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SnapshotId { get; set; }
    public string Rnc { get; set; } = default!;
    public string ChangeType { get; set; } = default!; // "Inserted", "Updated", "Deactivated"
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

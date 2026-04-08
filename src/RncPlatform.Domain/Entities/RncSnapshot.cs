using System;
using RncPlatform.Domain.Enums;

namespace RncPlatform.Domain.Entities;

public class RncSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? SourceName { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string? SourceFileName { get; set; }
    public string? FileHash { get; set; }
    public int RecordCount { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int DeactivatedCount { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public SnapshotStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

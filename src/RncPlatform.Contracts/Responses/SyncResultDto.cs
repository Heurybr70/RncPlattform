using System;

namespace RncPlatform.Contracts.Responses;

public class SyncResultDto
{
    public Guid SnapshotId { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int DeactivatedCount { get; set; }
    public string Status { get; set; } = default!;
    public string? ErrorMessage { get; set; }
}

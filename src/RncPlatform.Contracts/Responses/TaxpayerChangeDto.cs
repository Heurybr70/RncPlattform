using System;

namespace RncPlatform.Contracts.Responses;

public class TaxpayerChangeDto
{
    public Guid ChangeId { get; set; }
    public Guid SnapshotId { get; set; }
    public string ChangeType { get; set; } = default!;
    public DateTime DetectedAt { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
}

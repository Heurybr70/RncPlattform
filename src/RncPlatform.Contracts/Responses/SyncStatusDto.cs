using System;

namespace RncPlatform.Contracts.Responses;

public class SyncStatusDto
{
    public DateTime LastRunAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public string? LastStatus { get; set; }
    public string? LastMessage { get; set; }
}

namespace RncPlatform.Application.Abstractions.Persistence;

public class StagingMergeResult
{
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int DeactivatedCount { get; set; }
}
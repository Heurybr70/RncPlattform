using System;

namespace RncPlatform.Domain.Entities;

public class DistributedLock
{
    public string Resource { get; set; } = default!;
    public string LockedBy { get; set; } = default!;
    public DateTime LockedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}

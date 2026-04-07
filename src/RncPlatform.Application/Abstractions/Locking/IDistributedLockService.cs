using System;
using System.Threading;
using System.Threading.Tasks;

namespace RncPlatform.Application.Abstractions.Locking;

public interface IDistributedLockService
{
    Task<bool> AcquireLockAsync(string resource, string lockedBy, TimeSpan expiration, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(string resource, string lockedBy, CancellationToken cancellationToken = default);
}

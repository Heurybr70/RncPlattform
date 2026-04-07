using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RncPlatform.Application.Abstractions.Locking;
using RncPlatform.Domain.Entities;
using RncPlatform.Infrastructure.Persistence;

namespace RncPlatform.Infrastructure.Locking;

public class DistributedLockService : IDistributedLockService
{
    private readonly RncDbContext _dbContext;

    public DistributedLockService(RncDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> AcquireLockAsync(string resource, string lockedBy, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var lockEntity = await _dbContext.DistributedLocks.FindAsync(new object[] { resource }, cancellationToken);

            if (lockEntity != null)
            {
                if (lockEntity.ExpiresAt > DateTime.UtcNow) return false; // ya bloqueado
                
                // Expiró, lo retomamos
                lockEntity.LockedBy = lockedBy;
                lockEntity.LockedAt = DateTime.UtcNow;
                lockEntity.ExpiresAt = DateTime.UtcNow.Add(expiration);
                _dbContext.DistributedLocks.Update(lockEntity);
            }
            else
            {
                lockEntity = new DistributedLock
                {
                    Resource = resource,
                    LockedBy = lockedBy,
                    LockedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.Add(expiration)
                };
                _dbContext.DistributedLocks.Add(lockEntity);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Podría dar FK exception o duplicate (concurrencia)
            return false;
        }
    }

    public async Task ReleaseLockAsync(string resource, string lockedBy, CancellationToken cancellationToken = default)
    {
        var lockEntity = await _dbContext.DistributedLocks.FindAsync(new object[] { resource }, cancellationToken);
        if (lockEntity != null && lockEntity.LockedBy == lockedBy)
        {
            _dbContext.DistributedLocks.Remove(lockEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

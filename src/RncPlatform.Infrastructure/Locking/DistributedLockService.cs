using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RncPlatform.Application.Abstractions.Locking;
using RncPlatform.Domain.Entities;
using RncPlatform.Infrastructure.Persistence;

namespace RncPlatform.Infrastructure.Locking;

public class DistributedLockService : IDistributedLockService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _connectionString;

    public DistributedLockService(IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<bool> AcquireLockAsync(string resource, string lockedBy, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RncDbContext>();
            
            var lockEntity = await dbContext.DistributedLocks.FindAsync(new object[] { resource }, cancellationToken);

            if (lockEntity != null)
            {
                if (lockEntity.ExpiresAt > DateTime.UtcNow) return false; // ya bloqueado
                
                // Expiró, lo retomamos
                lockEntity.LockedBy = lockedBy;
                lockEntity.LockedAt = DateTime.UtcNow;
                lockEntity.ExpiresAt = DateTime.UtcNow.Add(expiration);
                dbContext.DistributedLocks.Update(lockEntity);
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
                dbContext.DistributedLocks.Add(lockEntity);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task ReleaseLockAsync(string resource, string lockedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RncDbContext>();

            var lockEntity = await dbContext.DistributedLocks.FindAsync(new object[] { resource }, cancellationToken);
            if (lockEntity != null && lockEntity.LockedBy == lockedBy)
            {
                dbContext.DistributedLocks.Remove(lockEntity);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception)
        {
            // Lock release is best-effort or it will expire naturally.
        }
    }
}

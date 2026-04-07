using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Domain.Entities;
using RncPlatform.Domain.Enums;

namespace RncPlatform.Infrastructure.Persistence.Repositories;

public class RncSnapshotRepository : IRncSnapshotRepository
{
    private readonly RncDbContext _dbContext;

    public RncSnapshotRepository(RncDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RncSnapshot?> GetLatestSuccessfulAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.RncSnapshots
            .Where(x => x.Status == SnapshotStatus.Success || x.Status == SnapshotStatus.NoChanges)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(RncSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _dbContext.RncSnapshots.Add(snapshot);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RncSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _dbContext.RncSnapshots.Update(snapshot);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<RncSnapshot>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RncSnapshots
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}

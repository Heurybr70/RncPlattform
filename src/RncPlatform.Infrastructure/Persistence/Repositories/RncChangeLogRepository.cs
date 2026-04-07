using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.Persistence.Repositories;

public class RncChangeLogRepository : IRncChangeLogRepository
{
    private readonly RncDbContext _dbContext;

    public RncChangeLogRepository(RncDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddBatchAsync(IEnumerable<RncChangeLog> changes, CancellationToken cancellationToken = default)
    {
        _dbContext.RncChangeLogs.AddRange(changes);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<RncChangeLog>> GetByRncAsync(string rnc, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RncChangeLogs
            .AsNoTracking()
            .Where(x => x.Rnc == rnc)
            .OrderByDescending(x => x.DetectedAt)
            .ToListAsync(cancellationToken);
    }
}

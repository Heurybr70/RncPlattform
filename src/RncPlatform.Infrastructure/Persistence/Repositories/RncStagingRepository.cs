using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.Persistence.Repositories;

public class RncStagingRepository : IRncStagingRepository
{
    private readonly RncDbContext _dbContext;

    public RncStagingRepository(RncDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ClearBatchAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        // Forma rápida usando ExecuteDeleteAsync de EF Core 7+
        await _dbContext.RncStaging
            .Where(x => x.ExecutionId == executionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task AddBatchAsync(IEnumerable<RncStaging> stagingRecords, CancellationToken cancellationToken = default)
    {
        _dbContext.RncStaging.AddRange(stagingRecords);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CompareAndGetOperationsAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        // En una BD real esto llamaría un SP o retornaría una enumeración.
        // Por simplicidad, retornamos el conteo procesado de staging
        return await _dbContext.RncStaging.CountAsync(x => x.ExecutionId == executionId, cancellationToken);
    }
}

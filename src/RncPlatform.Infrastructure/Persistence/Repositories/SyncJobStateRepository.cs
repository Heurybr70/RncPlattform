using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.Persistence.Repositories;

public class SyncJobStateRepository : ISyncJobStateRepository
{
    private readonly RncDbContext _dbContext;

    public SyncJobStateRepository(RncDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SyncJobState?> GetStateAsync(string jobName, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SyncJobStates.FindAsync(new object[] { jobName }, cancellationToken);
    }

    public async Task UpsertStateAsync(SyncJobState state, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.SyncJobStates.FindAsync(new object[] { state.JobName }, cancellationToken);
        if (existing != null)
        {
            existing.LastRunAt = state.LastRunAt;
            existing.LastSuccessAt = state.LastSuccessAt;
            existing.LastFailureAt = state.LastFailureAt;
            existing.LastStatus = state.LastStatus;
            existing.LastMessage = state.LastMessage;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _dbContext.SyncJobStates.Add(state);
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

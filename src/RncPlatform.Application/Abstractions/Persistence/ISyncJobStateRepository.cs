using System.Threading;
using System.Threading.Tasks;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Application.Abstractions.Persistence;

public interface ISyncJobStateRepository
{
    Task<SyncJobState?> GetStateAsync(string jobName, CancellationToken cancellationToken = default);
    Task UpsertStateAsync(SyncJobState state, CancellationToken cancellationToken = default);
}

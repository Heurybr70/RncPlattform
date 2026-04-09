using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Application.Abstractions.Persistence;

public interface IRncSnapshotRepository
{
    Task<RncSnapshot?> GetByIdAsync(Guid snapshotId, CancellationToken cancellationToken = default);
    Task<RncSnapshot?> GetLatestSuccessfulAsync(CancellationToken cancellationToken = default);
    Task AddAsync(RncSnapshot snapshot, CancellationToken cancellationToken = default);
    Task UpdateAsync(RncSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IEnumerable<RncSnapshot>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
}

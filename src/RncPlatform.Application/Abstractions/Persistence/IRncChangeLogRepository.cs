using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Application.Abstractions.Persistence;

public interface IRncChangeLogRepository
{
    Task AddBatchAsync(IEnumerable<RncChangeLog> changes, CancellationToken cancellationToken = default);
    Task<IEnumerable<RncChangeLog>> GetByRncAsync(string rnc, CancellationToken cancellationToken = default);
}

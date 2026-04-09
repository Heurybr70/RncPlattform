using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Application.Abstractions.Persistence;

public interface IRncStagingRepository
{
    Task ClearBatchAsync(Guid executionId, CancellationToken cancellationToken = default);
    Task AddBatchAsync(IEnumerable<RncStaging> stagingRecords, CancellationToken cancellationToken = default);
    Task<int> CompareAndGetOperationsAsync(Guid executionId, CancellationToken cancellationToken = default);
    Task<StagingMergeResult> MergeStagingToTaxpayersAsync(Guid executionId, CancellationToken cancellationToken = default);
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Application.Abstractions.Persistence;

public interface ITaxpayerRepository
{
    Task<Taxpayer?> GetByRncAsync(string rnc, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Taxpayer> Items, int TotalCount, string? NextCursor)> SearchAsync(string term, int page, int pageSize, string? cursor = null, CancellationToken cancellationToken = default);
    Task UpsertBatchAsync(IEnumerable<Taxpayer> taxpayers, CancellationToken cancellationToken = default);
}

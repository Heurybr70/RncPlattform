using System;
using System.Threading;
using System.Threading.Tasks;
using RncPlatform.Contracts.Responses;

namespace RncPlatform.Application.Features.Sync.Services;

public interface IRncSyncService
{
    Task<SyncResultDto> RunSyncAsync(CancellationToken cancellationToken = default);
    Task<SyncStatusDto> GetSystemStatusAsync(CancellationToken cancellationToken = default);
    Task<SyncResultDto> ReprocessSnapshotAsync(Guid snapshotId, CancellationToken cancellationToken = default);
}

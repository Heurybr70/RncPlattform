using System;
using System.Threading;
using System.Threading.Tasks;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task RotateAsync(RefreshToken currentToken, RefreshToken replacementToken, CancellationToken cancellationToken = default);
    Task RevokeAllActiveByUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default);
}
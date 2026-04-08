using System;
using System.Threading;
using System.Threading.Tasks;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

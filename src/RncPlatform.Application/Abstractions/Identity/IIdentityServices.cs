using RncPlatform.Domain.Entities;

namespace RncPlatform.Application.Abstractions.Identity;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}

public interface IJwtProvider
{
    string GenerateToken(User user);
}

public interface IRefreshTokenService
{
    string GenerateToken();
    string HashToken(string token);
}

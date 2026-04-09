using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RncPlatform.Application.Abstractions.Identity;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Domain.Entities;
using RncPlatform.Domain.Enums;

namespace RncPlatform.Api.Startup;

public static class PrivilegedUserBootstrapper
{
    public static async Task EnsurePrivilegedUserAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Bootstrap:Enabled", true))
        {
            return;
        }

        using var scope = services.CreateScope();

        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PrivilegedUserBootstrapper");

        var hasPrivilegedUser = await userRepository.HasAnyUserInRolesAsync(
            [UserRole.Admin, UserRole.UserManager],
            cancellationToken);

        if (hasPrivilegedUser)
        {
            return;
        }

        var bootstrapSection = configuration.GetSection("Bootstrap");
        var username = bootstrapSection["Username"]?.Trim();
        var password = bootstrapSection["Password"];
        var email = bootstrapSection["Email"]?.Trim();
        var fullName = bootstrapSection["FullName"]?.Trim();

        if (!TryParsePrivilegedRole(bootstrapSection["Role"], out var role))
        {
            role = UserRole.Admin;
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            var message = "No hay usuarios privilegiados y faltan Bootstrap:Username o Bootstrap:Password. Configure variables de entorno para inicializar el acceso administrativo.";
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(message);
            }

            logger.LogWarning(message);
            return;
        }

        var existing = await userRepository.GetByUsernameAsync(username, cancellationToken);
        if (existing != null)
        {
            existing.PasswordHash = passwordHasher.HashPassword(password);
            existing.Email = string.IsNullOrWhiteSpace(email) ? existing.Email : email;
            existing.FullName = string.IsNullOrWhiteSpace(fullName) ? existing.FullName : fullName;
            existing.Role = role;
            existing.IsActive = true;
            existing.FailedLoginAttempts = 0;
            existing.LockoutUntil = null;
            existing.TokenVersion += 1;
            existing.UpdatedAt = DateTime.UtcNow;

            await userRepository.UpdateAsync(existing, cancellationToken);
            logger.LogWarning("Usuario bootstrap existente {Username} promovido al rol {Role}.", existing.Username, existing.Role);
            return;
        }

        var user = new User
        {
            Username = username,
            PasswordHash = passwordHasher.HashPassword(password),
            Email = email,
            FullName = string.IsNullOrWhiteSpace(fullName) ? "Bootstrap Administrator" : fullName,
            Role = role,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        };

        await userRepository.AddAsync(user, cancellationToken);
        logger.LogWarning("Usuario bootstrap {Username} creado con rol {Role}.", user.Username, user.Role);
    }

    private static bool TryParsePrivilegedRole(string? value, out UserRole role)
    {
        if (Enum.TryParse<UserRole>(value, ignoreCase: true, out role) &&
            (role == UserRole.Admin || role == UserRole.UserManager))
        {
            return true;
        }

        role = UserRole.Admin;
        return false;
    }
}
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RncPlatform.Application.Abstractions.Identity;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Contracts.Requests;
using RncPlatform.Contracts.Responses;
using RncPlatform.Domain.Entities;
using RncPlatform.Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace RncPlatform.Api.Controllers;

/// <summary>
/// Gestiona autenticacion, renovacion de sesion y administracion de usuarios.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly SecurityOptions _securityOptions;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider,
        IRefreshTokenService refreshTokenService,
        IOptions<JwtOptions> jwtOptions,
        IOptions<SecurityOptions> securityOptions,
        ILogger<AuthController> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
        _refreshTokenService = refreshTokenService;
        _jwtOptions = jwtOptions.Value;
        _securityOptions = securityOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Inicia sesion y devuelve un access token JWT junto con un refresh token rotativo.
    /// </summary>
    /// <param name="request">Credenciales del usuario.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    /// <returns>Par de tokens y metadatos de expiracion.</returns>
    [HttpPost("login")]
    [EnableRateLimiting(IdentityConstants.LoginRateLimitPolicy)]
    [SwaggerOperation(Summary = "Iniciar sesion", Description = "Valida credenciales, aplica lockout por intentos fallidos y devuelve access token y refresh token.")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        var now = DateTime.UtcNow;

        if (user == null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Credenciales inválidas",
                detail: "Usuario o contraseña incorrectos.");
        }

        if (!user.IsActive)
        {
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Cuenta inactiva",
                detail: "La cuenta está desactivada. Contacte a un administrador.");
        }

        if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > now)
        {
            return Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Cuenta bloqueada",
                detail: $"Demasiados intentos fallidos. Intente nuevamente después de {user.LockoutUntil.Value:u}.");
        }

        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts += 1;
            user.UpdatedAt = now;

            if (user.FailedLoginAttempts >= _securityOptions.LoginMaxFailedAttempts)
            {
                user.LockoutUntil = now.AddMinutes(_securityOptions.LoginLockoutMinutes);
                _logger.LogWarning("Usuario {Username} bloqueado temporalmente por intentos fallidos.", user.Username);
            }

            await _userRepository.UpdateAsync(user, cancellationToken);

            return Problem(
                statusCode: user.LockoutUntil.HasValue ? StatusCodes.Status423Locked : StatusCodes.Status401Unauthorized,
                title: user.LockoutUntil.HasValue ? "Cuenta bloqueada" : "Credenciales inválidas",
                detail: user.LockoutUntil.HasValue
                    ? "Se excedió el máximo de intentos permitidos. Intente más tarde."
                    : "Usuario o contraseña incorrectos.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await _userRepository.UpdateAsync(user, cancellationToken);

        var response = await IssueTokensAsync(user, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Renueva la sesion con un refresh token vigente y rota el token de refresh actual.
    /// </summary>
    /// <param name="request">Refresh token emitido previamente por la API.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    /// <returns>Nuevo access token y nuevo refresh token.</returns>
    [HttpPost("refresh")]
    [EnableRateLimiting(IdentityConstants.RefreshRateLimitPolicy)]
    [SwaggerOperation(Summary = "Renovar sesion", Description = "Rota el refresh token activo y emite un nuevo par de tokens. Si detecta reuse invalida las sesiones del usuario.")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var hashedToken = _refreshTokenService.HashToken(request.RefreshToken.Trim());
        var storedRefreshToken = await _refreshTokenRepository.GetByTokenHashAsync(hashedToken, cancellationToken);
        var now = DateTime.UtcNow;

        if (storedRefreshToken == null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Refresh token inválido",
                detail: "El refresh token suministrado no es válido.");
        }

        if (storedRefreshToken.RevokedAt.HasValue)
        {
            await InvalidateUserSessionsAsync(storedRefreshToken.UserId, "Se detectó reutilización de refresh token.", cancellationToken);
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Refresh token inválido",
                detail: "La sesión ya no es válida. Inicie sesión nuevamente.");
        }

        if (storedRefreshToken.ExpiresAt <= now)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Refresh token expirado",
                detail: "El refresh token expiró. Inicie sesión nuevamente.");
        }

        var user = await _userRepository.GetByIdAsync(storedRefreshToken.UserId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            await _refreshTokenRepository.RevokeAllActiveByUserAsync(storedRefreshToken.UserId, "Usuario inactivo o inexistente.", cancellationToken);
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Cuenta inactiva",
                detail: "La cuenta no está disponible para renovar la sesión.");
        }

        var response = await IssueTokensAsync(user, cancellationToken, storedRefreshToken);
        return Ok(response);
    }

    /// <summary>
    /// Cierra la sesion del usuario actual e invalida sus refresh tokens activos.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    [HttpPost("logout")]
    [Authorize]
    [EnableRateLimiting(IdentityConstants.RefreshRateLimitPolicy)]
    [SwaggerOperation(Summary = "Cerrar sesion", Description = "Invalida las sesiones activas del usuario autenticado mediante revocacion de refresh tokens y aumento de token version.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        await InvalidateUserSessionsAsync(userId, "Sesión cerrada por el usuario.", cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Registra un nuevo usuario en la plataforma.
    /// </summary>
    /// <param name="request">Datos del usuario a crear y rol deseado.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    /// <returns>El usuario creado.</returns>
    [HttpPost("register")]
    [Authorize(Policy = IdentityConstants.CanManageUsersPolicy)]
    [EnableRateLimiting(IdentityConstants.UserManagementRateLimitPolicy)]
    [SwaggerOperation(Summary = "Registrar usuario", Description = "Disponible para Admin y UserManager. UserManager solo puede crear usuarios con rol User o SyncOperator.")]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseRole(request.Role, out var requestedRole))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Rol inválido",
                detail: "El rol solicitado no es válido.");
        }

        if (!CanAssignRole(requestedRole))
        {
            return Forbid();
        }

        var username = request.Username.Trim();
        var existing = await _userRepository.GetByUsernameAsync(username, cancellationToken);
        if (existing != null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Usuario duplicado",
                detail: "El nombre de usuario ya existe.");
        }

        var user = new User
        {
            Username = username,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Email = request.Email,
            FullName = request.FullName,
            Role = requestedRole,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _userRepository.AddAsync(user, cancellationToken);

        return CreatedAtAction(
            nameof(GetUserById),
            new { userId = user.Id },
            MapUser(user));
    }

    /// <summary>
    /// Lista los usuarios visibles para gestion administrativa.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    [HttpGet("users")]
    [Authorize(Policy = IdentityConstants.CanManageUsersPolicy)]
    [EnableRateLimiting(IdentityConstants.UserManagementRateLimitPolicy)]
    [ProducesResponseType(typeof(UserSummaryDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [SwaggerOperation(Summary = "Listar usuarios", Description = "Devuelve el inventario de usuarios para tareas de administracion y soporte.")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);
        return Ok(users.Select(MapUser));
    }

    /// <summary>
    /// Obtiene el detalle administrativo de un usuario por su identificador.
    /// </summary>
    /// <param name="userId">Identificador GUID del usuario.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    [HttpGet("users/{userId:guid}")]
    [Authorize(Policy = IdentityConstants.CanManageUsersPolicy)]
    [EnableRateLimiting(IdentityConstants.UserManagementRateLimitPolicy)]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [SwaggerOperation(Summary = "Consultar usuario", Description = "Obtiene informacion administrativa de una cuenta existente.")]
    public async Task<IActionResult> GetUserById(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(MapUser(user));
    }

    /// <summary>
    /// Actualiza rol y estado activo de un usuario existente.
    /// </summary>
    /// <param name="userId">Identificador GUID del usuario a modificar.</param>
    /// <param name="request">Nuevo rol y estado activo esperado.</param>
    /// <param name="cancellationToken">Token de cancelacion de la solicitud.</param>
    [HttpPatch("users/{userId:guid}/access")]
    [Authorize(Policy = IdentityConstants.AdminOnlyPolicy)]
    [EnableRateLimiting(IdentityConstants.UserManagementRateLimitPolicy)]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [SwaggerOperation(Summary = "Actualizar acceso de usuario", Description = "Solo Admin. Cambia rol y estado activo, invalida sesiones previas y revoca refresh tokens si hay cambios efectivos.")]
    public async Task<IActionResult> UpdateUserAccess(Guid userId, [FromBody] UpdateUserAccessRequest request, CancellationToken cancellationToken)
    {
        if (!TryParseRole(request.Role, out var requestedRole))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Rol inválido",
                detail: "El rol solicitado no es válido.");
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(currentUserId, out var actorId) && actorId == user.Id && !request.IsActive)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Operación inválida",
                detail: "No puede desactivar su propio usuario.");
        }

        var accessChanged = user.Role != requestedRole || user.IsActive != request.IsActive;

        user.Role = requestedRole;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        if (accessChanged)
        {
            user.TokenVersion += 1;
            user.FailedLoginAttempts = 0;
            user.LockoutUntil = null;
        }

        await _userRepository.UpdateAsync(user, cancellationToken);

        if (accessChanged)
        {
            await _refreshTokenRepository.RevokeAllActiveByUserAsync(user.Id, "Acceso actualizado por administrador.", cancellationToken);
        }

        return Ok(MapUser(user));
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken cancellationToken, RefreshToken? currentRefreshToken = null)
    {
        var now = DateTime.UtcNow;
        var accessToken = _jwtProvider.GenerateToken(user);
        var refreshTokenValue = _refreshTokenService.GenerateToken();
        var refreshTokenExpiresAt = now.AddDays(_jwtOptions.RefreshTokenExpiryDays);
        var replacementRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _refreshTokenService.HashToken(refreshTokenValue),
            ExpiresAt = refreshTokenExpiresAt,
            CreatedAt = now
        };

        if (currentRefreshToken == null)
        {
            await _refreshTokenRepository.AddAsync(replacementRefreshToken, cancellationToken);
        }
        else
        {
            currentRefreshToken.RevokedAt = now;
            currentRefreshToken.RevokedReason = "Refresh token rotado.";
            currentRefreshToken.ReplacedByTokenHash = replacementRefreshToken.TokenHash;
            currentRefreshToken.LastUsedAt = now;
            await _refreshTokenRepository.RotateAsync(currentRefreshToken, replacementRefreshToken, cancellationToken);
        }

        return new AuthResponse
        {
            UserId = user.Id,
            Token = accessToken,
            RefreshToken = refreshTokenValue,
            Username = user.Username,
            Role = user.Role.ToString(),
            ExpiresAt = now.AddMinutes(_jwtOptions.ExpiryMinutes),
            RefreshTokenExpiresAt = refreshTokenExpiresAt
        };
    }

    private async Task InvalidateUserSessionsAsync(Guid userId, string reason, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return;
        }

        user.TokenVersion += 1;
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _refreshTokenRepository.RevokeAllActiveByUserAsync(userId, reason, cancellationToken);
    }

    private bool CanAssignRole(UserRole requestedRole)
    {
        if (User.IsInRole(UserRole.Admin.ToString()))
        {
            return true;
        }

        return requestedRole is UserRole.User or UserRole.SyncOperator;
    }

    private static bool TryParseRole(string value, out UserRole role)
    {
        return Enum.TryParse(value, ignoreCase: true, out role);
    }

    private static UserSummaryDto MapUser(User user)
    {
        return new UserSummaryDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            LockoutUntil = user.LockoutUntil
        };
    }
}

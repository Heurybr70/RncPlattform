using System;

namespace RncPlatform.Contracts.Responses;

/// <summary>
/// Respuesta emitida al iniciar sesion o renovar la sesion.
/// </summary>
public class AuthResponse
{
    /// <summary>
    /// Identificador del usuario autenticado.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Access token JWT para consumir endpoints autenticados.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token rotativo para renovar la sesion.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de usuario autenticado.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Rol actual del usuario autenticado.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Fecha y hora UTC de expiracion del access token.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Fecha y hora UTC de expiracion del refresh token.
    /// </summary>
    public DateTime RefreshTokenExpiresAt { get; set; }
}

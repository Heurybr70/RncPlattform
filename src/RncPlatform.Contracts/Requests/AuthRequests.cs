using System.ComponentModel.DataAnnotations;

namespace RncPlatform.Contracts.Requests;

/// <summary>
/// Credenciales requeridas para iniciar sesion.
/// </summary>
public class LoginRequest
{
    /// <summary>
    /// Nombre de usuario registrado en la plataforma.
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Contrasena en texto plano del usuario.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Datos requeridos para crear una nueva cuenta mediante la API.
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// Nombre de usuario unico.
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Contrasena inicial del usuario.
    /// </summary>
    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Correo electronico de contacto del usuario.
    /// </summary>
    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    /// <summary>
    /// Nombre completo o nombre visible del usuario.
    /// </summary>
    [StringLength(100)]
    public string? FullName { get; set; }

    /// <summary>
    /// Rol solicitado para la cuenta nueva.
    /// </summary>
    [StringLength(32)]
    public string Role { get; set; } = "User";
}

/// <summary>
/// Solicitud para cambiar rol y estado de una cuenta existente.
/// </summary>
public class UpdateUserAccessRequest
{
    /// <summary>
    /// Nuevo rol que se aplicara al usuario.
    /// </summary>
    [Required]
    [StringLength(32)]
    public string Role { get; set; } = "User";

    /// <summary>
    /// Indica si la cuenta debe quedar activa despues del cambio.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Solicitud para renovar una sesion con refresh token.
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// Refresh token emitido previamente por la API.
    /// </summary>
    [Required]
    [StringLength(512, MinimumLength = 32)]
    public string RefreshToken { get; set; } = string.Empty;
}

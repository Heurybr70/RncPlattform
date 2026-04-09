using System;

namespace RncPlatform.Contracts.Responses;

/// <summary>
/// Resumen administrativo de un usuario del sistema.
/// </summary>
public class UserSummaryDto
{
    /// <summary>
    /// Identificador GUID del usuario.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Nombre de usuario unico.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Correo electronico del usuario.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Nombre visible o completo del usuario.
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// Rol actual asignado.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Indica si la cuenta puede autenticarse actualmente.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Fecha y hora UTC de creacion del usuario.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Fecha y hora UTC del ultimo inicio de sesion exitoso.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Fecha y hora UTC hasta la que la cuenta permanecera bloqueada, si aplica.
    /// </summary>
    public DateTime? LockoutUntil { get; set; }
}
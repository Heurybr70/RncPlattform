using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RncPlatform.Api.Filters;
using RncPlatform.Application.Abstractions.Identity;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Contracts.Requests;
using RncPlatform.Contracts.Responses;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;

    public AuthController(
        IUserRepository userRepository, 
        IPasswordHasher passwordHasher, 
        IJwtProvider jwtProvider)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized("Usuario o contraseña incorrectos.");
        }

        var token = _jwtProvider.GenerateToken(user);

        return Ok(new AuthResponse(
            token, 
            user.Username, 
            DateTime.UtcNow.AddHours(8))); // Simplificación del expiry para el response
    }

    [LocalOnly]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var existing = await _userRepository.GetByUsernameAsync(request.Username, cancellationToken);
        if (existing != null)
        {
            return BadRequest("El nombre de usuario ya existe.");
        }

        var user = new User
        {
            Username = request.Username,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Email = request.Email,
            FullName = request.FullName
        };

        await _userRepository.AddAsync(user, cancellationToken);

        return Ok(new { Message = "Usuario creado exitosamente.", Username = user.Username });
    }
}

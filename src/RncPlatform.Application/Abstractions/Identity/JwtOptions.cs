namespace RncPlatform.Application.Abstractions.Identity;

public class JwtOptions
{
    public string SecretKey { get; set; } = default!;
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public int ExpiryMinutes { get; set; }
    public int RefreshTokenExpiryDays { get; set; } = 14;
}

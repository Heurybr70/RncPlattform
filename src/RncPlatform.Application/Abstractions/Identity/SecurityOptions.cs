namespace RncPlatform.Application.Abstractions.Identity;

public class SecurityOptions
{
    public int LoginMaxFailedAttempts { get; set; } = 5;
    public int LoginLockoutMinutes { get; set; } = 15;
    public int LoginRateLimitPermitLimit { get; set; } = 20;
    public int LoginRateLimitWindowMinutes { get; set; } = 5;
}
namespace RncPlatform.Application.Abstractions.Identity;

public static class IdentityConstants
{
    public const string AdminOnlyPolicy = "AdminOnly";
    public const string CanManageUsersPolicy = "CanManageUsers";
    public const string CanRunSyncPolicy = "CanRunSync";

    public const string LoginRateLimitPolicy = "auth-login";
    public const string RefreshRateLimitPolicy = "auth-refresh";
    public const string UserManagementRateLimitPolicy = "user-management";
    public const string RncReadRateLimitPolicy = "rnc-read";
    public const string AdminSyncRateLimitPolicy = "admin-sync";

    public const string TokenVersionClaim = "token_version";
}
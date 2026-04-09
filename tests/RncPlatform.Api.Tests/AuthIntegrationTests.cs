using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RncPlatform.Api.Tests.Infrastructure;
using RncPlatform.Contracts.Requests;
using RncPlatform.Contracts.Responses;
using RncPlatform.Domain.Enums;

namespace RncPlatform.Api.Tests;

public class AuthIntegrationTests
{
    [Fact]
    public async Task LoginRefreshAndLogout_ShouldRotateAndRevokeRefreshTokens()
    {
        await using var app = await RncApiTestContext.CreateAsync();
        await app.Factory.SeedUserAsync("standard-user", "Password123!", UserRole.User);

        var login = await app.Client.LoginAsync("standard-user", "Password123!");
        Assert.False(string.IsNullOrWhiteSpace(login.Token));
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));

        var refreshResponse = await app.Client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = login.RefreshToken
        });

        refreshResponse.EnsureSuccessStatusCode();
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(login.RefreshToken, refreshed!.RefreshToken);

        app.Client.SetBearerToken(refreshed.Token);
        var logoutResponse = await app.Client.PostAsync("/api/v1/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var afterLogoutRefresh = await app.Client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = refreshed.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, afterLogoutRefresh.StatusCode);
    }

    [Fact]
    public async Task Login_ShouldRejectInactiveUsers()
    {
        await using var app = await RncApiTestContext.CreateAsync();
        await app.Factory.SeedUserAsync("inactive-user", "Password123!", UserRole.User, isActive: false);

        var response = await app.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Username = "inactive-user",
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Cuenta inactiva", problem?.Title);
    }

    [Fact]
    public async Task Login_ShouldLockUserAfterConfiguredFailedAttempts()
    {
        await using var app = await RncApiTestContext.CreateAsync();
        await app.Factory.SeedUserAsync("lockout-user", "Password123!", UserRole.User);

        var firstAttempt = await app.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Username = "lockout-user",
            Password = "WrongPassword123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, firstAttempt.StatusCode);

        var secondAttempt = await app.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Username = "lockout-user",
            Password = "WrongPassword123!"
        });
        Assert.Equal((HttpStatusCode)423, secondAttempt.StatusCode);

        var validAttemptWhileLocked = await app.Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Username = "lockout-user",
            Password = "Password123!"
        });
        Assert.Equal((HttpStatusCode)423, validAttemptWhileLocked.StatusCode);
    }

    [Fact]
    public async Task Policies_ShouldEnforceRoleBoundariesAcrossUserManagementAndSync()
    {
        await using var app = await RncApiTestContext.CreateAsync();
        app.Factory.SyncScenario.Records.Add(new RncApiTestFactory.TestTaxpayerRecord(
            "101010101",
            "EMPRESA INTEGRATION TEST",
            Estado: "ACTIVO"));

        await app.Factory.SeedUserAsync("basic-user", "Password123!", UserRole.User);
        await app.Factory.SeedUserAsync("manager-user", "Password123!", UserRole.UserManager);
        await app.Factory.SeedUserAsync("sync-user", "Password123!", UserRole.SyncOperator);
        var managedUser = await app.Factory.SeedUserAsync("managed-user", "Password123!", UserRole.User);
        await app.Factory.SeedUserAsync("admin-user", "Password123!", UserRole.Admin);

        var basicLogin = await app.Client.LoginAsync("basic-user", "Password123!");
        app.Client.SetBearerToken(basicLogin.Token);

        var basicRegisterResponse = await app.Client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Username = "created-by-basic",
            Password = "Password123!",
            Role = "User"
        });
        Assert.Equal(HttpStatusCode.Forbidden, basicRegisterResponse.StatusCode);

        var basicSyncResponse = await app.Client.PostAsync("/api/v1/admin/sync/run", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, basicSyncResponse.StatusCode);

        var managerLogin = await app.Client.LoginAsync("manager-user", "Password123!");
        app.Client.SetBearerToken(managerLogin.Token);

        var managerRegisterResponse = await app.Client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Username = "created-by-manager",
            Password = "Password123!",
            Role = "User"
        });
        Assert.Equal(HttpStatusCode.Created, managerRegisterResponse.StatusCode);

        var managerSyncResponse = await app.Client.PostAsync("/api/v1/admin/sync/run", content: null);
        Assert.Equal(HttpStatusCode.Forbidden, managerSyncResponse.StatusCode);

        var managerPatchResponse = await app.Client.PatchAsJsonAsync($"/api/v1/auth/users/{managedUser.Id}/access", new UpdateUserAccessRequest
        {
            Role = "User",
            IsActive = false
        });
        Assert.Equal(HttpStatusCode.Forbidden, managerPatchResponse.StatusCode);

        var syncLogin = await app.Client.LoginAsync("sync-user", "Password123!");
        app.Client.SetBearerToken(syncLogin.Token);

        var syncRunResponse = await app.Client.PostAsync("/api/v1/admin/sync/run", content: null);
        syncRunResponse.EnsureSuccessStatusCode();
        var syncResult = await syncRunResponse.Content.ReadFromJsonAsync<SyncResultDto>();
        Assert.NotNull(syncResult);
        Assert.Equal("Success", syncResult!.Status);

        var syncRegisterResponse = await app.Client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Username = "created-by-sync",
            Password = "Password123!",
            Role = "User"
        });
        Assert.Equal(HttpStatusCode.Forbidden, syncRegisterResponse.StatusCode);

        var adminLogin = await app.Client.LoginAsync("admin-user", "Password123!");
        app.Client.SetBearerToken(adminLogin.Token);

        var adminPatchResponse = await app.Client.PatchAsJsonAsync($"/api/v1/auth/users/{managedUser.Id}/access", new UpdateUserAccessRequest
        {
            Role = "SyncOperator",
            IsActive = true
        });
        adminPatchResponse.EnsureSuccessStatusCode();

        var updatedUserJson = await adminPatchResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("SyncOperator", updatedUserJson.GetProperty("role").GetString());
    }
}
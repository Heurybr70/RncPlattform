using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using RncPlatform.Contracts.Requests;
using RncPlatform.Contracts.Responses;

namespace RncPlatform.Api.Tests.Infrastructure;

public static class HttpTestExtensions
{
    public static async Task<AuthResponse> LoginAsync(this HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())
            ?? throw new InvalidOperationException("No se pudo deserializar la respuesta de login.");
    }

    public static void SetBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
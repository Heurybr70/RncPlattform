using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RncPlatform.Api.Tests.Infrastructure;
using RncPlatform.Contracts.Responses;
using RncPlatform.Domain.Entities;
using RncPlatform.Domain.Enums;

namespace RncPlatform.Api.Tests;

public class SearchIntegrationTests
{
    [Fact]
    public async Task Search_ShouldRequireMinimumLengthForNameTerms()
    {
        await using var app = await RncApiTestContext.CreateAsync();
        await app.Factory.SeedUserAsync("search-user", "Password123!", UserRole.User);

        var login = await app.Client.LoginAsync("search-user", "Password123!");
        app.Client.SetBearerToken(login.Token);

        var response = await app.Client.GetAsync("/api/v1/rncs?term=ab");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Término inválido", problem?.Title);
    }

    [Fact]
    public async Task Search_ShouldSupportExactRncAndPrefixNameQueries()
    {
        await using var app = await RncApiTestContext.CreateAsync();
        await app.Factory.SeedUserAsync("search-user", "Password123!", UserRole.User);
        await app.Factory.SeedTaxpayersAsync(new[]
        {
            BuildTaxpayer("101010101", "ALFA INDUSTRIES"),
            BuildTaxpayer("202020202", "ALFABETA LOGISTICS", nombreComercial: "EXPRESO ALFA"),
            BuildTaxpayer("303030303", "BETA SERVICES", nombreComercial: "GAMMA TRADING")
        });

        var login = await app.Client.LoginAsync("search-user", "Password123!");
        app.Client.SetBearerToken(login.Token);

        var exactResponse = await app.Client.GetFromJsonAsync<PagedResponse<TaxpayerSearchItemDto>>("/api/v1/rncs?term=202020202");
        Assert.NotNull(exactResponse);
        Assert.Equal(1, exactResponse!.TotalCount);
        Assert.Equal("202020202", Assert.Single(exactResponse.Items).Rnc);

        var prefixResponse = await app.Client.GetFromJsonAsync<PagedResponse<TaxpayerSearchItemDto>>("/api/v1/rncs?term=ALF");
        Assert.NotNull(prefixResponse);
        Assert.Equal(2, prefixResponse!.TotalCount);

        var commercialResponse = await app.Client.GetFromJsonAsync<PagedResponse<TaxpayerSearchItemDto>>("/api/v1/rncs?term=EXP");
        Assert.NotNull(commercialResponse);
        Assert.Equal(1, commercialResponse!.TotalCount);
        Assert.Equal("202020202", Assert.Single(commercialResponse.Items).Rnc);
    }

    [Fact]
    public async Task Search_ShouldSupportCursorPaginationWithoutBreakingPageMode()
    {
        await using var app = await RncApiTestContext.CreateAsync();
        await app.Factory.SeedUserAsync("cursor-user", "Password123!", UserRole.User);
        await app.Factory.SeedTaxpayersAsync(new[]
        {
            BuildTaxpayer("101010101", "ALFA ONE"),
            BuildTaxpayer("202020202", "ALFA TWO", nombreComercial: "ALFA COMMERCIAL TWO"),
            BuildTaxpayer("303030303", "ALFA THREE"),
            BuildTaxpayer("404040404", "ALFA FOUR")
        });

        var login = await app.Client.LoginAsync("cursor-user", "Password123!");
        app.Client.SetBearerToken(login.Token);

        var pageMode = await app.Client.GetFromJsonAsync<PagedResponse<TaxpayerSearchItemDto>>("/api/v1/rncs?term=ALF&page=1&pageSize=2");
        Assert.NotNull(pageMode);
        Assert.Equal(4, pageMode!.TotalCount);
        Assert.Null(pageMode.NextCursor);
        Assert.Equal(2, pageMode.Items.Count());

        var firstCursorPage = await app.Client.GetFromJsonAsync<PagedResponse<TaxpayerSearchItemDto>>("/api/v1/rncs?term=ALF&pageSize=2&cursor=");
        Assert.NotNull(firstCursorPage);
        Assert.Equal(4, firstCursorPage!.TotalCount);
        Assert.Equal(2, firstCursorPage.Items.Count());
        Assert.Equal("202020202", firstCursorPage.NextCursor);

        var secondCursorPage = await app.Client.GetFromJsonAsync<PagedResponse<TaxpayerSearchItemDto>>($"/api/v1/rncs?term=ALF&pageSize=2&cursor={firstCursorPage.NextCursor}");
        Assert.NotNull(secondCursorPage);
        Assert.Equal(4, secondCursorPage!.TotalCount);
        Assert.Equal(2, secondCursorPage.Items.Count());
        Assert.Null(secondCursorPage.NextCursor);
        Assert.Equal(new[] { "303030303", "404040404" }, secondCursorPage.Items.Select(x => x.Rnc));
    }

    [Fact]
    public async Task SearchCache_ShouldBeInvalidatedAfterSync()
    {
        await using var app = await RncApiTestContext.CreateAsync();
        await app.Factory.SeedUserAsync("admin-search", "Password123!", UserRole.Admin);
        await app.Factory.SeedTaxpayersAsync(new[]
        {
            BuildTaxpayer("111111111", "ALFA UNO")
        });

        var login = await app.Client.LoginAsync("admin-search", "Password123!");
        app.Client.SetBearerToken(login.Token);

        var firstSearch = await app.Client.GetFromJsonAsync<PagedResponse<TaxpayerSearchItemDto>>("/api/v1/rncs?term=ALF");
        Assert.NotNull(firstSearch);
        Assert.Equal(1, firstSearch!.TotalCount);

        app.Factory.SyncScenario.Records.Add(new RncApiTestFactory.TestTaxpayerRecord("111111111", "ALFA UNO", Estado: "ACTIVO"));
        app.Factory.SyncScenario.Records.Add(new RncApiTestFactory.TestTaxpayerRecord("222222222", "ALFA DOS", Estado: "ACTIVO"));

        var syncResponse = await app.Client.PostAsync("/api/v1/admin/sync/run", content: null);
        syncResponse.EnsureSuccessStatusCode();

        var secondSearch = await app.Client.GetFromJsonAsync<PagedResponse<TaxpayerSearchItemDto>>("/api/v1/rncs?term=ALF");
        Assert.NotNull(secondSearch);
        Assert.Equal(2, secondSearch!.TotalCount);
    }

    private static Taxpayer BuildTaxpayer(string rnc, string nombre, string? nombreComercial = null)
    {
        return new Taxpayer
        {
            Rnc = rnc,
            NombreORazonSocial = nombre,
            NombreComercial = nombreComercial,
            IsActiveInLatestSnapshot = true,
            Estado = "ACTIVO",
            SourceFirstSeenAt = DateTime.UtcNow,
            SourceLastSeenAt = DateTime.UtcNow,
            LastSnapshotId = Guid.NewGuid(),
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }
}
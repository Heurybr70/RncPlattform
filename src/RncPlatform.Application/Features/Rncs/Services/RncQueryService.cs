using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RncPlatform.Application.Abstractions.Caching;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Contracts.Responses;

namespace RncPlatform.Application.Features.Rncs.Services;

public class RncQueryService : IRncQueryService
{
    private const string TaxpayerCacheNamespace = "taxpayer";
    private const string TaxpayerSearchCacheNamespace = "taxpayer-search";
    private const string TaxpayerChangeLogCacheNamespace = "taxpayer-change-log";
    private readonly ITaxpayerRepository _repository;
    private readonly IRncChangeLogRepository _changeLogRepository;
    private readonly IRncCacheService _cache;
    private readonly ILogger<RncQueryService> _logger;

    public RncQueryService(ITaxpayerRepository repository, IRncChangeLogRepository changeLogRepository, IRncCacheService cache, ILogger<RncQueryService> logger)
    {
        _repository = repository;
        _changeLogRepository = changeLogRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TaxpayerDto?> GetByRncAsync(string rnc, CancellationToken cancellationToken = default)
    {
        var namespaceVersion = await _cache.GetNamespaceVersionAsync(TaxpayerCacheNamespace, cancellationToken);
        var cacheKey = $"{TaxpayerCacheNamespace}:{namespaceVersion}:rnc:{rnc}";
        var cached = await _cache.GetAsync<TaxpayerDto>(cacheKey, cancellationToken);
        if (cached != null) return cached;

        var entity = await _repository.GetByRncAsync(rnc, cancellationToken);
        if (entity == null) return null;

        var dto = new TaxpayerDto
        {
            Rnc = entity.Rnc,
            Cedula = entity.Cedula,
            NombreORazonSocial = entity.NombreORazonSocial,
            NombreComercial = entity.NombreComercial,
            Categoria = entity.Categoria,
            RegimenPago = entity.RegimenPago,
            Estado = entity.Estado,
            ActividadEconomica = entity.ActividadEconomica,
            FechaConstitucion = entity.FechaConstitucion,
            IsActive = entity.IsActiveInLatestSnapshot,
            RemovedAt = entity.SourceRemovedAt
        };

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromHours(12), cancellationToken);
        return dto;
    }

    public async Task<PagedResponse<TaxpayerSearchItemDto>> SearchAsync(string term, int page, int pageSize, string? cursor = null, CancellationToken cancellationToken = default)
    {
        term = term.Trim();
        var isCursorMode = cursor != null;
        cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim();
        var repositoryCursor = isCursorMode ? cursor ?? string.Empty : null;
        var namespaceVersion = await _cache.GetNamespaceVersionAsync(TaxpayerSearchCacheNamespace, cancellationToken);
        var normalizedTerm = term.ToUpperInvariant();
        var cursorKey = isCursorMode ? cursor ?? "start" : "page-mode";
        var cacheKey = $"{TaxpayerSearchCacheNamespace}:{namespaceVersion}:term:{normalizedTerm}:page:{page}:size:{pageSize}:cursor:{cursorKey}";
        var cached = await _cache.GetAsync<PagedResponse<TaxpayerSearchItemDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var (items, total, nextCursor) = await _repository.SearchAsync(term, page, pageSize, repositoryCursor, cancellationToken);

        var dtoItems = items.Select(x => new TaxpayerSearchItemDto
        {
            Rnc = x.Rnc,
            NombreORazonSocial = x.NombreORazonSocial,
            NombreComercial = x.NombreComercial,
            Estado = x.Estado,
            IsActive = x.IsActiveInLatestSnapshot
        });

        var response = new PagedResponse<TaxpayerSearchItemDto>
        {
            Items = dtoItems,
            TotalCount = total,
            Page = isCursorMode ? 1 : page,
            PageSize = pageSize,
            NextCursor = nextCursor
        };

        await _cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5), cancellationToken);
        return response;
    }

    public async Task<IEnumerable<TaxpayerChangeDto>> GetChangesByRncAsync(string rnc, CancellationToken cancellationToken = default)
    {
        var normalizedRnc = rnc.Trim().ToUpperInvariant();
        var namespaceVersion = await _cache.GetNamespaceVersionAsync(TaxpayerChangeLogCacheNamespace, cancellationToken);
        var cacheKey = $"{TaxpayerChangeLogCacheNamespace}:{namespaceVersion}:rnc:{normalizedRnc}";
        var cached = await _cache.GetAsync<List<TaxpayerChangeDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var changes = await _changeLogRepository.GetByRncAsync(rnc, cancellationToken);
        var response = changes.Select(x => new TaxpayerChangeDto
        {
            ChangeId = x.Id,
            SnapshotId = x.SnapshotId,
            ChangeType = x.ChangeType,
            DetectedAt = x.DetectedAt,
            OldValuesJson = x.OldValuesJson,
            NewValuesJson = x.NewValuesJson
        }).ToList();

        await _cache.SetAsync(cacheKey, response, TimeSpan.FromHours(12), cancellationToken);
        return response;
    }
}

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
        var cacheKey = $"rnc:{rnc}";
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

    public async Task<PagedResponse<TaxpayerSearchItemDto>> SearchAsync(string term, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _repository.SearchAsync(term, page, pageSize, cancellationToken);

        var dtoItems = items.Select(x => new TaxpayerSearchItemDto
        {
            Rnc = x.Rnc,
            NombreORazonSocial = x.NombreORazonSocial,
            NombreComercial = x.NombreComercial,
            Estado = x.Estado,
            IsActive = x.IsActiveInLatestSnapshot
        });

        return new PagedResponse<TaxpayerSearchItemDto>
        {
            Items = dtoItems,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<TaxpayerChangeDto>> GetChangesByRncAsync(string rnc, CancellationToken cancellationToken = default)
    {
        var changes = await _changeLogRepository.GetByRncAsync(rnc, cancellationToken);
        return changes.Select(x => new TaxpayerChangeDto
        {
            ChangeId = x.Id,
            SnapshotId = x.SnapshotId,
            ChangeType = x.ChangeType,
            DetectedAt = x.DetectedAt,
            OldValuesJson = x.OldValuesJson,
            NewValuesJson = x.NewValuesJson
        });
    }
}

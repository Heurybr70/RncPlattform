using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Domain.Entities;

namespace RncPlatform.Infrastructure.Persistence.Repositories;

public class TaxpayerRepository : ITaxpayerRepository
{
    private readonly RncDbContext _dbContext;

    public TaxpayerRepository(RncDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Taxpayer?> GetByRncAsync(string rnc, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Taxpayers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Rnc == rnc, cancellationToken);
    }

    public async Task<(IEnumerable<Taxpayer> Items, int TotalCount, string? NextCursor)> SearchAsync(string term, int page, int pageSize, string? cursor = null, CancellationToken cancellationToken = default)
    {
        term = term.Trim();
        var isCursorMode = cursor != null;
        cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim();

        var filteredQuery = _dbContext.Taxpayers.AsNoTracking();

        if (TryNormalizeExactRnc(term, out var normalizedRnc))
        {
            filteredQuery = filteredQuery.Where(x => x.Rnc == normalizedRnc);
        }
        else if (!string.IsNullOrWhiteSpace(term))
        {
            var likePattern = $"{EscapeSqlLikePattern(term)}%";
            filteredQuery = filteredQuery.Where(x =>
                EF.Functions.Like(x.NombreORazonSocial, likePattern) ||
                (x.NombreComercial != null && EF.Functions.Like(x.NombreComercial, likePattern)));
        }

        var totalCount = await filteredQuery.CountAsync(cancellationToken);

        if (isCursorMode && !TryNormalizeExactRnc(term, out _))
        {
            var cursorQuery = filteredQuery;

            if (cursor != null)
            {
                cursorQuery = cursorQuery.Where(x => x.Rnc.CompareTo(cursor) > 0);
            }

            var cursorItems = await cursorQuery
                .OrderBy(x => x.Rnc)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);

            var hasMore = cursorItems.Count > pageSize;
            var pageItems = hasMore
                ? cursorItems.Take(pageSize).ToList()
                : cursorItems;

            return (pageItems, totalCount, hasMore ? pageItems[^1].Rnc : null);
        }

        var items = await filteredQuery
            .OrderBy(x => x.NombreORazonSocial)
            .ThenBy(x => x.Rnc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount, null);
    }

    private static bool TryNormalizeExactRnc(string term, out string normalizedRnc)
    {
        normalizedRnc = new string(term.Where(char.IsDigit).ToArray());

        return normalizedRnc.Length is >= 9 and <= 20
            && term.All(ch => char.IsDigit(ch) || ch == '-' || char.IsWhiteSpace(ch));
    }

    private static string EscapeSqlLikePattern(string value)
    {
        return value
            .Replace("[", "[[]")
            .Replace("%", "[%]")
            .Replace("_", "[_]");
    }

    public async Task UpsertBatchAsync(IEnumerable<Taxpayer> taxpayers, CancellationToken cancellationToken = default)
    {
        // En un escenario real de millones se usaría SqlBulkCopy y MERGE.
        // Aquí hacemos un upsert simple usando EF Core 
        
        var rncs = taxpayers.Select(x => x.Rnc).ToList();
        var existing = await _dbContext.Taxpayers
            .Where(x => rncs.Contains(x.Rnc))
            .ToDictionaryAsync(x => x.Rnc, cancellationToken);

        foreach (var taxpayer in taxpayers)
        {
            if (existing.TryGetValue(taxpayer.Rnc, out var current))
            {
                current.NombreORazonSocial = taxpayer.NombreORazonSocial;
                current.Cedula = taxpayer.Cedula;
                current.NombreComercial = taxpayer.NombreComercial;
                current.Categoria = taxpayer.Categoria;
                current.RegimenPago = taxpayer.RegimenPago;
                current.Estado = taxpayer.Estado;
                current.ActividadEconomica = taxpayer.ActividadEconomica;
                current.FechaConstitucion = taxpayer.FechaConstitucion;
                current.IsActiveInLatestSnapshot = taxpayer.IsActiveInLatestSnapshot;
                current.SourceLastSeenAt = taxpayer.SourceLastSeenAt;
                current.SourceRemovedAt = taxpayer.SourceRemovedAt;
                current.LastSnapshotId = taxpayer.LastSnapshotId;
                current.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _dbContext.Taxpayers.Add(taxpayer);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

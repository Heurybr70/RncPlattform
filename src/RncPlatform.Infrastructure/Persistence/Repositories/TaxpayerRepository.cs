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

    public async Task<(IEnumerable<Taxpayer> Items, int TotalCount)> SearchAsync(string term, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Taxpayers.AsNoTracking();
        
        if (!string.IsNullOrWhiteSpace(term))
        {
            // Busca por nombre (ignorando mayúsculas/minúsculas debido al default collation usual de SQL Server) o por RNC exacto
            query = query.Where(x => EF.Functions.Like(x.NombreORazonSocial, $"%{term}%") || x.Rnc == term);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderBy(x => x.NombreORazonSocial)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
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

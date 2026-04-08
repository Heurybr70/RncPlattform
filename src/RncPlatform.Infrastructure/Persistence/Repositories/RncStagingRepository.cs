using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using RncPlatform.Application.Abstractions.Persistence;
using RncPlatform.Domain.Entities;
using FastMember;

namespace RncPlatform.Infrastructure.Persistence.Repositories
{
    public class RncStagingRepository : IRncStagingRepository
    {
        private readonly RncDbContext _dbContext;

        public RncStagingRepository(RncDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task ClearBatchAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            await _dbContext.RncStaging
                .Where(x => x.ExecutionId == executionId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        public async Task AddBatchAsync(IEnumerable<RncStaging> stagingRecords, CancellationToken cancellationToken = default)
        {
            var connectionString = _dbContext.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString)) throw new InvalidOperationException("Connection string not found.");

            using var sqlCopy = new SqlBulkCopy(connectionString, SqlBulkCopyOptions.Default);
            sqlCopy.DestinationTableName = "RncStaging";
            sqlCopy.BatchSize = 10000;
            sqlCopy.BulkCopyTimeout = 600; // 10 minutos para carga masiva remota
            
            sqlCopy.ColumnMappings.Add("Id", "Id");
            sqlCopy.ColumnMappings.Add("ExecutionId", "ExecutionId");
            sqlCopy.ColumnMappings.Add("Rnc", "Rnc");
            sqlCopy.ColumnMappings.Add("Cedula", "Cedula");
            sqlCopy.ColumnMappings.Add("NombreORazonSocial", "NombreORazonSocial");
            sqlCopy.ColumnMappings.Add("NombreComercial", "NombreComercial");
            sqlCopy.ColumnMappings.Add("Categoria", "Categoria");
            sqlCopy.ColumnMappings.Add("RegimenPago", "RegimenPago");
            sqlCopy.ColumnMappings.Add("Estado", "Estado");
            sqlCopy.ColumnMappings.Add("ActividadEconomica", "ActividadEconomica");
            sqlCopy.ColumnMappings.Add("FechaConstitucion", "FechaConstitucion");
            
            using var reader = ObjectReader.Create(stagingRecords, "Id", "ExecutionId", "Rnc", "Cedula", "NombreORazonSocial", "NombreComercial", "Categoria", "RegimenPago", "Estado", "ActividadEconomica", "FechaConstitucion");
            await sqlCopy.WriteToServerAsync(reader, cancellationToken);
        }

        public async Task<int> CompareAndGetOperationsAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.RncStaging.CountAsync(x => x.ExecutionId == executionId, cancellationToken);
        }
        
        public async Task<int> MergeStagingToTaxpayersAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            _dbContext.Database.SetCommandTimeout(600); // 10 minutos para el MERGE remoto
            var sqlMerge = @"
                MERGE INTO Taxpayers AS Target
                USING (SELECT Rnc, Cedula, NombreORazonSocial, NombreComercial, Categoria, RegimenPago, Estado, ActividadEconomica, FechaConstitucion FROM RncStaging WHERE ExecutionId = @executionId) AS Source
                ON Target.Rnc = Source.Rnc
                WHEN MATCHED THEN 
                    UPDATE SET 
                        Target.Cedula = Source.Cedula,
                        Target.NombreORazonSocial = Source.NombreORazonSocial,
                        Target.NombreComercial = Source.NombreComercial,
                        Target.Categoria = Source.Categoria,
                        Target.RegimenPago = Source.RegimenPago,
                        Target.Estado = Source.Estado,
                        Target.ActividadEconomica = Source.ActividadEconomica,
                        Target.FechaConstitucion = Source.FechaConstitucion,
                        Target.IsActiveInLatestSnapshot = 1,
                        Target.SourceLastSeenAt = GETUTCDATE(),
                        Target.LastSnapshotId = @executionId,
                        Target.UpdatedAt = GETUTCDATE()
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT (Id, Rnc, Cedula, NombreORazonSocial, NombreComercial, Categoria, RegimenPago, Estado, ActividadEconomica, FechaConstitucion, IsActiveInLatestSnapshot, SourceFirstSeenAt, SourceLastSeenAt, LastSnapshotId, CreatedAt, UpdatedAt)
                    VALUES (NEWID(), Source.Rnc, Source.Cedula, Source.NombreORazonSocial, Source.NombreComercial, Source.Categoria, Source.RegimenPago, Source.Estado, Source.ActividadEconomica, Source.FechaConstitucion, 1, GETUTCDATE(), GETUTCDATE(), @executionId, GETUTCDATE(), GETUTCDATE());
                
                -- Set inactive missing ones
                UPDATE Taxpayers SET IsActiveInLatestSnapshot = 0, SourceRemovedAt = GETUTCDATE() WHERE LastSnapshotId != @executionId AND IsActiveInLatestSnapshot = 1;
            ";
            
            return await _dbContext.Database.ExecuteSqlRawAsync(sqlMerge, new[] { new SqlParameter("@executionId", executionId) }, cancellationToken);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
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
        
        public async Task<StagingMergeResult> MergeStagingToTaxpayersAsync(Guid executionId, CancellationToken cancellationToken = default)
        {
            _dbContext.Database.SetCommandTimeout(600); // 10 minutos para el MERGE remoto
            var sqlMerge = @"
                DECLARE @ChangeAudit TABLE
                (
                    SnapshotId UNIQUEIDENTIFIER,
                    Rnc NVARCHAR(20),
                    ChangeType NVARCHAR(20),
                    OldCedula NVARCHAR(20) NULL,
                    OldNombreORazonSocial NVARCHAR(255) NULL,
                    OldNombreComercial NVARCHAR(255) NULL,
                    OldCategoria NVARCHAR(100) NULL,
                    OldRegimenPago NVARCHAR(100) NULL,
                    OldEstado NVARCHAR(50) NULL,
                    OldActividadEconomica NVARCHAR(255) NULL,
                    OldFechaConstitucion NVARCHAR(50) NULL,
                    OldIsActive BIT NULL,
                    NewCedula NVARCHAR(20) NULL,
                    NewNombreORazonSocial NVARCHAR(255) NULL,
                    NewNombreComercial NVARCHAR(255) NULL,
                    NewCategoria NVARCHAR(100) NULL,
                    NewRegimenPago NVARCHAR(100) NULL,
                    NewEstado NVARCHAR(50) NULL,
                    NewActividadEconomica NVARCHAR(255) NULL,
                    NewFechaConstitucion NVARCHAR(50) NULL,
                    NewIsActive BIT NULL
                );

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
                    VALUES (NEWID(), Source.Rnc, Source.Cedula, Source.NombreORazonSocial, Source.NombreComercial, Source.Categoria, Source.RegimenPago, Source.Estado, Source.ActividadEconomica, Source.FechaConstitucion, 1, GETUTCDATE(), GETUTCDATE(), @executionId, GETUTCDATE(), GETUTCDATE())
                OUTPUT
                    @executionId,
                    COALESCE(inserted.Rnc, deleted.Rnc),
                    CASE WHEN $action = 'INSERT' THEN 'Inserted' ELSE 'Updated' END,
                    deleted.Cedula,
                    deleted.NombreORazonSocial,
                    deleted.NombreComercial,
                    deleted.Categoria,
                    deleted.RegimenPago,
                    deleted.Estado,
                    deleted.ActividadEconomica,
                    deleted.FechaConstitucion,
                    deleted.IsActiveInLatestSnapshot,
                    inserted.Cedula,
                    inserted.NombreORazonSocial,
                    inserted.NombreComercial,
                    inserted.Categoria,
                    inserted.RegimenPago,
                    inserted.Estado,
                    inserted.ActividadEconomica,
                    inserted.FechaConstitucion,
                    inserted.IsActiveInLatestSnapshot
                INTO @ChangeAudit (
                    SnapshotId,
                    Rnc,
                    ChangeType,
                    OldCedula,
                    OldNombreORazonSocial,
                    OldNombreComercial,
                    OldCategoria,
                    OldRegimenPago,
                    OldEstado,
                    OldActividadEconomica,
                    OldFechaConstitucion,
                    OldIsActive,
                    NewCedula,
                    NewNombreORazonSocial,
                    NewNombreComercial,
                    NewCategoria,
                    NewRegimenPago,
                    NewEstado,
                    NewActividadEconomica,
                    NewFechaConstitucion,
                    NewIsActive
                );
                
                -- Set inactive missing ones
                UPDATE Taxpayers
                SET IsActiveInLatestSnapshot = 0,
                    SourceRemovedAt = GETUTCDATE(),
                    UpdatedAt = GETUTCDATE()
                OUTPUT
                    @executionId,
                    inserted.Rnc,
                    'Deactivated',
                    deleted.Cedula,
                    deleted.NombreORazonSocial,
                    deleted.NombreComercial,
                    deleted.Categoria,
                    deleted.RegimenPago,
                    deleted.Estado,
                    deleted.ActividadEconomica,
                    deleted.FechaConstitucion,
                    deleted.IsActiveInLatestSnapshot,
                    inserted.Cedula,
                    inserted.NombreORazonSocial,
                    inserted.NombreComercial,
                    inserted.Categoria,
                    inserted.RegimenPago,
                    inserted.Estado,
                    inserted.ActividadEconomica,
                    inserted.FechaConstitucion,
                    inserted.IsActiveInLatestSnapshot
                INTO @ChangeAudit (
                    SnapshotId,
                    Rnc,
                    ChangeType,
                    OldCedula,
                    OldNombreORazonSocial,
                    OldNombreComercial,
                    OldCategoria,
                    OldRegimenPago,
                    OldEstado,
                    OldActividadEconomica,
                    OldFechaConstitucion,
                    OldIsActive,
                    NewCedula,
                    NewNombreORazonSocial,
                    NewNombreComercial,
                    NewCategoria,
                    NewRegimenPago,
                    NewEstado,
                    NewActividadEconomica,
                    NewFechaConstitucion,
                    NewIsActive
                )
                WHERE LastSnapshotId != @executionId AND IsActiveInLatestSnapshot = 1;

                INSERT INTO RncChangeLogs (Id, SnapshotId, Rnc, ChangeType, OldValuesJson, NewValuesJson, DetectedAt)
                SELECT
                    NEWID(),
                    SnapshotId,
                    Rnc,
                    ChangeType,
                    CASE
                        WHEN ChangeType = 'Inserted' THEN NULL
                        ELSE (
                            SELECT
                                ChangeAudit.Rnc AS Rnc,
                                ChangeAudit.OldCedula AS Cedula,
                                ChangeAudit.OldNombreORazonSocial AS NombreORazonSocial,
                                ChangeAudit.OldNombreComercial AS NombreComercial,
                                ChangeAudit.OldCategoria AS Categoria,
                                ChangeAudit.OldRegimenPago AS RegimenPago,
                                ChangeAudit.OldEstado AS Estado,
                                ChangeAudit.OldActividadEconomica AS ActividadEconomica,
                                ChangeAudit.OldFechaConstitucion AS FechaConstitucion,
                                ChangeAudit.OldIsActive AS IsActive
                            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                        )
                    END,
                    (
                        SELECT
                            ChangeAudit.Rnc AS Rnc,
                            ChangeAudit.NewCedula AS Cedula,
                            ChangeAudit.NewNombreORazonSocial AS NombreORazonSocial,
                            ChangeAudit.NewNombreComercial AS NombreComercial,
                            ChangeAudit.NewCategoria AS Categoria,
                            ChangeAudit.NewRegimenPago AS RegimenPago,
                            ChangeAudit.NewEstado AS Estado,
                            ChangeAudit.NewActividadEconomica AS ActividadEconomica,
                            ChangeAudit.NewFechaConstitucion AS FechaConstitucion,
                            ChangeAudit.NewIsActive AS IsActive
                        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                    ),
                    GETUTCDATE()
                FROM @ChangeAudit AS ChangeAudit
                WHERE ChangeType IN ('Inserted', 'Deactivated')
                    OR ISNULL(OldCedula, '') <> ISNULL(NewCedula, '')
                    OR ISNULL(OldNombreORazonSocial, '') <> ISNULL(NewNombreORazonSocial, '')
                    OR ISNULL(OldNombreComercial, '') <> ISNULL(NewNombreComercial, '')
                    OR ISNULL(OldCategoria, '') <> ISNULL(NewCategoria, '')
                    OR ISNULL(OldRegimenPago, '') <> ISNULL(NewRegimenPago, '')
                    OR ISNULL(OldEstado, '') <> ISNULL(NewEstado, '')
                    OR ISNULL(OldActividadEconomica, '') <> ISNULL(NewActividadEconomica, '')
                    OR ISNULL(OldFechaConstitucion, '') <> ISNULL(NewFechaConstitucion, '')
                    OR ISNULL(CAST(OldIsActive AS INT), -1) <> ISNULL(CAST(NewIsActive AS INT), -1);

                SELECT
                    SUM(CASE WHEN ChangeType = 'Inserted' THEN 1 ELSE 0 END) AS InsertedCount,
                    SUM(CASE WHEN ChangeType = 'Updated' AND (
                        ISNULL(OldCedula, '') <> ISNULL(NewCedula, '')
                        OR ISNULL(OldNombreORazonSocial, '') <> ISNULL(NewNombreORazonSocial, '')
                        OR ISNULL(OldNombreComercial, '') <> ISNULL(NewNombreComercial, '')
                        OR ISNULL(OldCategoria, '') <> ISNULL(NewCategoria, '')
                        OR ISNULL(OldRegimenPago, '') <> ISNULL(NewRegimenPago, '')
                        OR ISNULL(OldEstado, '') <> ISNULL(NewEstado, '')
                        OR ISNULL(OldActividadEconomica, '') <> ISNULL(NewActividadEconomica, '')
                        OR ISNULL(OldFechaConstitucion, '') <> ISNULL(NewFechaConstitucion, '')
                        OR ISNULL(CAST(OldIsActive AS INT), -1) <> ISNULL(CAST(NewIsActive AS INT), -1)
                    ) THEN 1 ELSE 0 END) AS UpdatedCount,
                    SUM(CASE WHEN ChangeType = 'Deactivated' THEN 1 ELSE 0 END) AS DeactivatedCount
                FROM @ChangeAudit;
            ";

            var connection = _dbContext.Database.GetDbConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = sqlMerge;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 600;
            command.Parameters.Add(new SqlParameter("@executionId", executionId));

            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new StagingMergeResult
                {
                    InsertedCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    UpdatedCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    DeactivatedCount = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
                };
            }

            return new StagingMergeResult();
        }
    }
}

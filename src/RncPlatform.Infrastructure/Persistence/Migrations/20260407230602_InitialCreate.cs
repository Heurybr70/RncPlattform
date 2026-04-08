using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RncPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DistributedLocks",
                columns: table => new
                {
                    Resource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LockedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributedLocks", x => x.Resource);
                });

            migrationBuilder.CreateTable(
                name: "RncChangeLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rnc = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChangeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RncChangeLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RncSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RecordCount = table.Column<int>(type: "int", nullable: false),
                    InsertedCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedCount = table.Column<int>(type: "int", nullable: false),
                    DeactivatedCount = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RncSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RncStaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rnc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Cedula = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NombreORazonSocial = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RegimenPago = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActividadEconomica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaConstitucion = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RncStaging", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncJobStates",
                columns: table => new
                {
                    JobName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSuccessAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncJobStates", x => x.JobName);
                });

            migrationBuilder.CreateTable(
                name: "Taxpayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rnc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Cedula = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NombreORazonSocial = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RegimenPago = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ActividadEconomica = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FechaConstitucion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActiveInLatestSnapshot = table.Column<bool>(type: "bit", nullable: false),
                    SourceFirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceLastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceRemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taxpayers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RncChangeLogs_Rnc",
                table: "RncChangeLogs",
                column: "Rnc");

            migrationBuilder.CreateIndex(
                name: "IX_RncChangeLogs_SnapshotId",
                table: "RncChangeLogs",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_RncStaging_ExecutionId_Rnc",
                table: "RncStaging",
                columns: new[] { "ExecutionId", "Rnc" });

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_Estado",
                table: "Taxpayers",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_IsActiveInLatestSnapshot",
                table: "Taxpayers",
                column: "IsActiveInLatestSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_NombreORazonSocial",
                table: "Taxpayers",
                column: "NombreORazonSocial");

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_Rnc",
                table: "Taxpayers",
                column: "Rnc",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistributedLocks");

            migrationBuilder.DropTable(
                name: "RncChangeLogs");

            migrationBuilder.DropTable(
                name: "RncSnapshots");

            migrationBuilder.DropTable(
                name: "RncStaging");

            migrationBuilder.DropTable(
                name: "SyncJobStates");

            migrationBuilder.DropTable(
                name: "Taxpayers");
        }
    }
}

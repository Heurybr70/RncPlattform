using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RncPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperationalSecurityIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RncSnapshots_FileHash",
                table: "RncSnapshots",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_RncSnapshots_Status_StartedAt",
                table: "RncSnapshots",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RncChangeLogs_Rnc_DetectedAt",
                table: "RncChangeLogs",
                columns: new[] { "Rnc", "DetectedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RncSnapshots_FileHash",
                table: "RncSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_RncSnapshots_Status_StartedAt",
                table: "RncSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_RncChangeLogs_Rnc_DetectedAt",
                table: "RncChangeLogs");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RncPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommercialNameSearchIndexAndCursorSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_CommercialName_Rnc",
                table: "Taxpayers",
                columns: new[] { "NombreComercial", "Rnc" },
                filter: "[NombreComercial] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Taxpayers_CommercialName_Rnc",
                table: "Taxpayers");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RncPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizedTaxpayerNameSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Taxpayers_NombreORazonSocial",
                table: "Taxpayers");

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_Name_Rnc",
                table: "Taxpayers",
                columns: new[] { "NombreORazonSocial", "Rnc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Taxpayers_Name_Rnc",
                table: "Taxpayers");

            migrationBuilder.CreateIndex(
                name: "IX_Taxpayers_NombreORazonSocial",
                table: "Taxpayers",
                column: "NombreORazonSocial");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RncPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotArchivalAndReprocess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchivedFilePath",
                table: "RncSnapshots",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReprocessedFromSnapshotId",
                table: "RncSnapshots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RncSnapshots_ReprocessedFromSnapshotId",
                table: "RncSnapshots",
                column: "ReprocessedFromSnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RncSnapshots_ReprocessedFromSnapshotId",
                table: "RncSnapshots");

            migrationBuilder.DropColumn(
                name: "ArchivedFilePath",
                table: "RncSnapshots");

            migrationBuilder.DropColumn(
                name: "ReprocessedFromSnapshotId",
                table: "RncSnapshots");
        }
    }
}

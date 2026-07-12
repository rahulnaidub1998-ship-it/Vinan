using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinan.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LiveIntelligenceTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Secret = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DueAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    IsComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CompletedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notes_UpdatedAt",
                table: "Notes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderCredentials_Provider",
                table: "ProviderCredentials",
                column: "Provider",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_IsComplete_DueAt_Priority",
                table: "Tasks",
                columns: new[] { "IsComplete", "DueAt", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "ProviderCredentials");

            migrationBuilder.DropTable(
                name: "Tasks");
        }
    }
}

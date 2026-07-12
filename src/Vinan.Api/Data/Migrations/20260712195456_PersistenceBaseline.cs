using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinan.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersistenceBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: false),
                    RiskLevel = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 20000, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 3000, nullable: false),
                    RiskLevel = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    When = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IsComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_CreatedAt",
                table: "AuditEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_ConversationId_CreatedAt",
                table: "ConversationMessages",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_UpdatedAt",
                table: "Conversations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PendingActions_Status_CreatedAt",
                table: "PendingActions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_IsComplete_CreatedAt",
                table: "Reminders",
                columns: new[] { "IsComplete", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "ConversationMessages");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "Memories");

            migrationBuilder.DropTable(
                name: "PendingActions");

            migrationBuilder.DropTable(
                name: "Reminders");
        }
    }
}

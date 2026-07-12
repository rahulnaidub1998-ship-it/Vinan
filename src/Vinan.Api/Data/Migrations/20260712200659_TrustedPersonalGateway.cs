using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vinan.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrustedPersonalGateway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OwnerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnerProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeviceEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    EnrolledAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<long>(type: "INTEGER", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceEnrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceEnrollments_OwnerProfiles_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "OwnerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceEnrollments_OwnerId_RevokedAt",
                table: "DeviceEnrollments",
                columns: new[] { "OwnerId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OwnerProfiles_Scope",
                table: "OwnerProfiles",
                column: "Scope",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceEnrollments");

            migrationBuilder.DropTable(
                name: "OwnerProfiles");
        }
    }
}

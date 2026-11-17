using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintDeck.Infrastructure.Identity.Migrations;

/// <inheritdoc />
public partial class _20261117091956_AppApiKeys : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApiKeys",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                Hint = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                LastUsedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiKeys", x => x.Id);
                table.ForeignKey(
                    name: "FK_ApiKeys_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApiKeys_Hash",
            table: "ApiKeys",
            column: "Hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ApiKeys_UserId",
            table: "ApiKeys",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApiKeys");
    }
}

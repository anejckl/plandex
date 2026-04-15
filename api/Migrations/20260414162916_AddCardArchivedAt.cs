using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plandex.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCardArchivedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "archived_at",
                table: "cards",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "archived_at",
                table: "cards");
        }
    }
}

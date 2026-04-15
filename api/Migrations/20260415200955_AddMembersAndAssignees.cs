using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plandex.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMembersAndAssignees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "board_members",
                columns: table => new
                {
                    board_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_board_members", x => new { x.board_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_board_members_boards_board_id",
                        column: x => x.board_id,
                        principalTable: "boards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_board_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "card_assignees",
                columns: table => new
                {
                    card_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_assignees", x => new { x.card_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_card_assignees_cards_card_id",
                        column: x => x.card_id,
                        principalTable: "cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_card_assignees_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_board_members_user_id",
                table: "board_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_assignees_user_id",
                table: "card_assignees",
                column: "user_id");

            // Backfill: every existing board's owner becomes a BoardMember with Role=Owner (0).
            // This is the single source of truth for access going forward.
            migrationBuilder.Sql(@"
                INSERT INTO board_members (board_id, user_id, role, added_at)
                SELECT id, owner_id, 0, created_at FROM boards
                ON CONFLICT DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "board_members");

            migrationBuilder.DropTable(
                name: "card_assignees");
        }
    }
}

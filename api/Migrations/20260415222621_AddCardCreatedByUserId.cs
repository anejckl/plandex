using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Plandex.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCardCreatedByUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "created_by_user_id",
                table: "cards",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_cards_created_by_user_id",
                table: "cards",
                column: "created_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_cards_users_created_by_user_id",
                table: "cards",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Backfill existing cards to the board owner that created the board.
            // Pre-existing rows can never count against anyone's limit otherwise
            // (their creator would be NULL and the COUNT query wouldn't match).
            migrationBuilder.Sql(@"
                UPDATE cards
                SET created_by_user_id = b.owner_id
                FROM lists l JOIN boards b ON l.board_id = b.id
                WHERE cards.list_id = l.id AND cards.created_by_user_id IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_cards_users_created_by_user_id",
                table: "cards");

            migrationBuilder.DropIndex(
                name: "ix_cards_created_by_user_id",
                table: "cards");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "cards");
        }
    }
}

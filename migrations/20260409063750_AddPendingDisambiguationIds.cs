using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    /// <inheritdoc />
    public partial class AddPendingDisambiguationIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Previous attempt to rename+alter failed on some DBs where the column contained non-JSON text.
            // Since we want to remove the column entirely, drop both possible column names if present.
            migrationBuilder.Sql("ALTER TABLE bot_conversation_state DROP COLUMN IF EXISTS pending_disambiguation_ids;");
            migrationBuilder.Sql("ALTER TABLE bot_conversation_state DROP COLUMN IF EXISTS \"PendingDisambiguationIds\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "pending_disambiguation_ids",
                table: "bot_conversation_state",
                newName: "PendingDisambiguationIds");

            migrationBuilder.AlterColumn<string>(
                name: "PendingDisambiguationIds",
                table: "bot_conversation_state",
                type: "text",
                nullable: true,
                oldClrType: typeof(JsonDocument),
                oldType: "jsonb",
                oldNullable: true);
        }
    }
}

using System;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    [DbContext(typeof(ARCompletionsContext))]
    [Migration("20260409070000_RemovePendingDisambiguationIds")]
    public partial class RemovePendingDisambiguationIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure both possible column names are removed (case-sensitive/insensitive differences)
            migrationBuilder.Sql("ALTER TABLE bot_conversation_state DROP COLUMN IF EXISTS pending_disambiguation_ids;");
            migrationBuilder.Sql("ALTER TABLE bot_conversation_state DROP COLUMN IF EXISTS \"PendingDisambiguationIds\";");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate as jsonb to match expected model if rolling back
            migrationBuilder.Sql("ALTER TABLE bot_conversation_state ADD COLUMN IF NOT EXISTS pending_disambiguation_ids jsonb;");
        }
    }
}

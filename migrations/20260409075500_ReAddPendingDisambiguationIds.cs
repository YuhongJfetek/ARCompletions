using System;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    [DbContext(typeof(ARCompletionsContext))]
    [Migration("20260409075500_ReAddPendingDisambiguationIds")]
    public partial class ReAddPendingDisambiguationIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-create the column as jsonb to match model expectations
            migrationBuilder.Sql("ALTER TABLE bot_conversation_state ADD COLUMN IF NOT EXISTS pending_disambiguation_ids jsonb;");
            migrationBuilder.Sql("ALTER TABLE bot_conversation_state ADD COLUMN IF NOT EXISTS \"PendingDisambiguationIds\" jsonb;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE bot_conversation_state DROP COLUMN IF EXISTS pending_disambiguation_ids;");
            migrationBuilder.Sql("ALTER TABLE bot_conversation_state DROP COLUMN IF EXISTS \"PendingDisambiguationIds\";");
        }
    }
}

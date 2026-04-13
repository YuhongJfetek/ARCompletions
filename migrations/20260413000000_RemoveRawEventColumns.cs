using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    public partial class RemoveRawEventColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop legacy raw event columns defensively so migration is safe across environments
            migrationBuilder.Sql("ALTER TABLE bot_incoming_events DROP COLUMN IF EXISTS \"RawEventJson\";");
            migrationBuilder.Sql("ALTER TABLE bot_incoming_events DROP COLUMN IF EXISTS \"RawEvent\";");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate columns with a sensible default to allow downgrade (if needed)
            migrationBuilder.Sql("ALTER TABLE bot_incoming_events ADD COLUMN IF NOT EXISTS \"RawEventJson\" text NOT NULL DEFAULT '';");
            migrationBuilder.Sql("ALTER TABLE bot_incoming_events ADD COLUMN IF NOT EXISTS \"RawEvent\" text NOT NULL DEFAULT '';");
        }
    }
}

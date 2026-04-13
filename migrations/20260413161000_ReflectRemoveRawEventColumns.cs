using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    public partial class ReflectRemoveRawEventColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure legacy raw event columns are removed
            migrationBuilder.Sql("ALTER TABLE bot_incoming_events DROP COLUMN IF EXISTS \"RawEventJson\";");
            migrationBuilder.Sql("ALTER TABLE bot_incoming_events DROP COLUMN IF EXISTS \"RawEvent\";");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate columns if downgrading
            migrationBuilder.Sql("ALTER TABLE bot_incoming_events ADD COLUMN IF NOT EXISTS \"RawEventJson\" text NOT NULL DEFAULT '';" );
            migrationBuilder.Sql("ALTER TABLE bot_incoming_events ADD COLUMN IF NOT EXISTS \"RawEvent\" text NOT NULL DEFAULT '';" );
        }
    }
}

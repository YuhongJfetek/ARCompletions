using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.Migrations
{
    public partial class SetAppLogsIdDefault : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
            migrationBuilder.Sql("ALTER TABLE app_logs ALTER COLUMN id SET DEFAULT gen_random_uuid()::text;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE app_logs ALTER COLUMN id DROP DEFAULT;");
        }
    }
}

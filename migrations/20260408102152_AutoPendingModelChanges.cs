using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    /// <inheritdoc />
    public partial class AutoPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use a conditional CREATE TABLE to avoid failures when the table already exists
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS app_logs (
    id text NOT NULL,
    time_stamp timestamp with time zone NOT NULL,
    level text,
    message text,
    message_template text,
    exception text,
    properties jsonb,
    log_event jsonb,
    CONSTRAINT PK_app_logs PRIMARY KEY (id)
);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left blank to avoid dropping pre-existing tables when rolling back.
        }
    }
}

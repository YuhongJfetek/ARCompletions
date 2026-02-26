using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    /// <inheritdoc />
    public partial class __CheckDiff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op migration: schema adjustments for SQLite are handled manually.
            // Removed ALTER COLUMN / raw SQL because SQLite does not support those operations.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op revert: nothing to revert for SQLite.
        }
    }
}

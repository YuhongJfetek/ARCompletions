using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    /// <inheritdoc />
    public partial class AddLineEventAndChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: Removed AlterColumn operations for existing 'Completions' table
            // because SQLite doesn't support ALTER COLUMN in-place. Keeping existing
            // table schema unchanged and only adding new tables for ChatMessages and LineEvents.
            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Meta = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LineEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    EventId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    RawEvent = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Processed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineEvents", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMessages");
            migrationBuilder.DropTable(
                name: "LineEvents");
        }
    }
}

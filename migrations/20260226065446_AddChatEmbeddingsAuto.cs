using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    /// <inheritdoc />
    public partial class AddChatEmbeddingsAuto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatEmbeddings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ChatMessageId = table.Column<string>(type: "TEXT", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatEmbeddings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatEmbeddings");
        }
    }
}

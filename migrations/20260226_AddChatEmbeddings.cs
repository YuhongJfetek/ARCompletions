using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    public partial class AddChatEmbeddings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only create pgvector extension and table when using Npgsql provider
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

                // Create table with pgvector column (dimension 1536 as an example)
                migrationBuilder.Sql(@"
                    CREATE TABLE IF NOT EXISTS ChatEmbeddings (
                        Id text PRIMARY KEY,
                        ChatMessageId text,
                        Embedding vector(1536),
                        CreatedAt bigint
                    );
                ");

                // Example IVFFLAT index for faster nearest neighbour (requires pgvector)
                migrationBuilder.Sql(@"
                    CREATE INDEX IF NOT EXISTS ix_chatembeddings_embedding_ivf
                    ON ChatEmbeddings USING ivfflat (Embedding vector_l2_ops) WITH (lists = 100);
                ");
            }
            else
            {
                // For SQLite fallback, create a simple table to store embedding JSON
                migrationBuilder.CreateTable(
                    name: "ChatEmbeddings",
                    columns: table => new
                    {
                        Id = table.Column<string>(type: "TEXT", nullable: false),
                        ChatMessageId = table.Column<string>(type: "TEXT", nullable: true),
                        EmbeddingJson = table.Column<string>(type: "TEXT", nullable: true),
                        CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                    },
                    constraints: table =>
                    {
                        table.PrimaryKey("PK_ChatEmbeddings", x => x.Id);
                    });
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            {
                migrationBuilder.Sql("DROP INDEX IF EXISTS \"ix_chatembeddings_embedding_ivf\";");
                migrationBuilder.Sql("DROP TABLE IF EXISTS \"ChatEmbeddings\";");
            }
            else
            {
                migrationBuilder.DropTable(
                    name: "ChatEmbeddings");
            }
        }
    }
}

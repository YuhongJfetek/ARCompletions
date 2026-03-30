using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    public partial class AddMessageResultAndFaqQueryLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageResults",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    MessageId = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    MatchedFaqId = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqQueryLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    MessageResultId = table.Column<string>(type: "text", nullable: true),
                    QueryText = table.Column<string>(type: "text", nullable: true),
                    MatchedFaqId = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqQueryLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageResults_CreatedAt",
                table: "MessageResults",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FaqQueryLogs_CreatedAt",
                table: "FaqQueryLogs",
                column: "CreatedAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "FaqQueryLogs");
            migrationBuilder.DropTable(name: "MessageResults");
        }
    }
}

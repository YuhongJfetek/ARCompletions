using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    public partial class AddFaqMinConfidenceScore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MinConfidenceScore",
                table: "bot_faq_items",
                type: "double precision",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinConfidenceScore",
                table: "bot_faq_items");
        }
    }
}

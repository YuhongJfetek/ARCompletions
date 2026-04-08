using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    public partial class AddSearchTextCacheIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_bot_faq_items_SearchTextCache",
                table: "bot_faq_items",
                column: "SearchTextCache");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bot_faq_items_SearchTextCache",
                table: "bot_faq_items");
        }
    }
}

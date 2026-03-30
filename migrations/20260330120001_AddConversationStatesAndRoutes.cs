using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    public partial class AddConversationStatesAndRoutes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    VendorId = table.Column<string>(type: "TEXT", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", nullable: false),
                    BotEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    HandoffUntil = table.Column<long>(type: "INTEGER", nullable: true),
                    PendingDisambiguationIds = table.Column<string>(type: "TEXT", nullable: true),
                    PendingDisambiguationAt = table.Column<long>(type: "INTEGER", nullable: true),
                    LastStaffMessageAt = table.Column<long>(type: "INTEGER", nullable: true),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_ConversationStates_Vendor_Source_Conversation",
                table: "ConversationStates",
                columns: new[] { "VendorId", "SourceType", "ConversationId" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "FaqAliases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    VendorId = table.Column<string>(type: "TEXT", nullable: false),
                    Term = table.Column<string>(type: "TEXT", nullable: false),
                    Synonyms = table.Column<string>(type: "TEXT", nullable: true),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    FaqIds = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqAliases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageRoutes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    VendorId = table.Column<string>(type: "TEXT", nullable: false),
                    InputLogId = table.Column<string>(type: "TEXT", nullable: true),
                    ConversationId = table.Column<string>(type: "TEXT", nullable: true),
                    Route = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true),
                    MatchedFaqId = table.Column<string>(type: "TEXT", nullable: true),
                    MatchedScore = table.Column<double>(type: "REAL", nullable: true),
                    MatchedBy = table.Column<string>(type: "TEXT", nullable: true),
                    FaqCategory = table.Column<string>(type: "TEXT", nullable: true),
                    LlmEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NeedsHandoff = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReplyText = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageRoutes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageRoutes_VendorId",
                table: "MessageRoutes",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageRoutes_InputLogId",
                table: "MessageRoutes",
                column: "InputLogId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageRoutes_Route",
                table: "MessageRoutes",
                column: "Route");

            migrationBuilder.CreateIndex(
                name: "IX_MessageRoutes_CreatedAt",
                table: "MessageRoutes",
                column: "CreatedAt");

            migrationBuilder.CreateTable(
                name: "VendorStaffUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    VendorId = table.Column<string>(type: "TEXT", nullable: false),
                    LineUserId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Role = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorStaffUsers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_VendorStaffUsers_Vendor_LineUser",
                table: "VendorStaffUsers",
                columns: new[] { "VendorId", "LineUserId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VendorStaffUsers");
            migrationBuilder.DropTable(name: "MessageRoutes");
            migrationBuilder.DropTable(name: "FaqAliases");
            migrationBuilder.DropTable(name: "ConversationStates");
        }
    }
}

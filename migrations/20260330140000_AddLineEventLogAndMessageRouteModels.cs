using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    public partial class AddLineEventLogAndMessageRouteModels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // LineEventLogs: add linkage and processing columns
            migrationBuilder.AddColumn<string>(
                name: "MessageResultId",
                table: "LineEventLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingStatus",
                table: "LineEventLogs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "LineEventLogs",
                type: "TEXT",
                nullable: true);

            // MessageRoutes: add llm/embedding model columns
            migrationBuilder.AddColumn<string>(
                name: "LlmModel",
                table: "MessageRoutes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingModel",
                table: "MessageRoutes",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "MessageResultId", table: "LineEventLogs");
            migrationBuilder.DropColumn(name: "ProcessingStatus", table: "LineEventLogs");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "LineEventLogs");

            migrationBuilder.DropColumn(name: "LlmModel", table: "MessageRoutes");
            migrationBuilder.DropColumn(name: "EmbeddingModel", table: "MessageRoutes");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    public partial class AddMessageResultFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VendorId",
                table: "MessageResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Route",
                table: "MessageResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MessageRouteId",
                table: "MessageResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatchedBy",
                table: "MessageResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MatchedScore",
                table: "MessageResults",
                type: "double precision",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "MessageResults");

            migrationBuilder.DropColumn(
                name: "Route",
                table: "MessageResults");

            migrationBuilder.DropColumn(
                name: "MessageRouteId",
                table: "MessageResults");

            migrationBuilder.DropColumn(
                name: "MatchedBy",
                table: "MessageResults");

            migrationBuilder.DropColumn(
                name: "MatchedScore",
                table: "MessageResults");
        }
    }
}

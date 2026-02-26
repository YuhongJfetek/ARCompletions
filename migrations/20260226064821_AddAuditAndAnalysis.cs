using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ARCompletions.migrations
{
    /// <inheritdoc />
    public partial class AddAuditAndAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisDetails",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    JobId = table.Column<string>(type: "TEXT", nullable: false),
                    CompletionId = table.Column<string>(type: "TEXT", nullable: false),
                    Similarity = table.Column<double>(type: "REAL", nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Flags = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisJobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Params = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Error = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LineEvents_EventId",
                table: "LineEvents",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_UserId",
                table: "ChatMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisJobs_Status",
                table: "AnalysisJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisDetails");

            migrationBuilder.DropTable(
                name: "AnalysisJobs");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_LineEvents_EventId",
                table: "LineEvents");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_UserId",
                table: "ChatMessages");
        }
    }
}

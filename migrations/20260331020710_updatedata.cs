using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ARCompletions.migrations
{
    /// <inheritdoc />
    public partial class updatedata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisDetails");

            migrationBuilder.DropTable(
                name: "AnalysisJobs");

            migrationBuilder.DropTable(
                name: "ChatEmbeddings");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "Completions");

            migrationBuilder.DropTable(
                name: "Feedbacks");

            migrationBuilder.DropTable(
                name: "LineEvents");

            migrationBuilder.DropTable(
                name: "LineUsers");

            migrationBuilder.DropTable(
                name: "UnmatchedQueries");

            migrationBuilder.DropIndex(
                name: "IX_VendorStaffUsers_VendorId_LineUserId",
                table: "VendorStaffUsers");

            migrationBuilder.DropIndex(
                name: "IX_MessageRoutes_CreatedAt",
                table: "MessageRoutes");

            migrationBuilder.DropIndex(
                name: "IX_MessageRoutes_InputLogId",
                table: "MessageRoutes");

            migrationBuilder.DropIndex(
                name: "IX_MessageRoutes_Route",
                table: "MessageRoutes");

            migrationBuilder.DropIndex(
                name: "IX_MessageRoutes_VendorId",
                table: "MessageRoutes");

            migrationBuilder.DropIndex(
                name: "IX_MessageResults_CreatedAt",
                table: "MessageResults");

            migrationBuilder.DropIndex(
                name: "IX_LineEventLogs_LineUserId",
                table: "LineEventLogs");

            migrationBuilder.DropIndex(
                name: "IX_FaqQueryLogs_CreatedAt",
                table: "FaqQueryLogs");

            migrationBuilder.DropIndex(
                name: "IX_ConversationStates_VendorId_SourceType_ConversationId",
                table: "ConversationStates");

            migrationBuilder.AlterColumn<string>(
                name: "LineUserId",
                table: "LineEventLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "LineEventLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "TargetId",
                table: "AuditLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Payload",
                table: "AuditLogs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "AiFaqAnalysisJobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    JobNo = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DateFrom = table.Column<long>(type: "bigint", nullable: false),
                    DateTo = table.Column<long>(type: "bigint", nullable: false),
                    ConversationCount = table.Column<int>(type: "integer", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: true),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<long>(type: "bigint", nullable: true),
                    FinishedAt = table.Column<long>(type: "bigint", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    TriggeredByType = table.Column<string>(type: "text", nullable: true),
                    TriggeredById = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiFaqAnalysisJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BulkJobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Initiator = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    FilterJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TotalCount = table.Column<long>(type: "bigint", nullable: false),
                    ProcessedCount = table.Column<long>(type: "bigint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    MessageText = table.Column<string>(type: "text", nullable: true),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    ExternalMessageId = table.Column<string>(type: "text", nullable: true),
                    ReplyToken = table.Column<string>(type: "text", nullable: true),
                    SenderId = table.Column<string>(type: "text", nullable: true),
                    SenderName = table.Column<string>(type: "text", nullable: true),
                    SourceFaqId = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: true),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: true),
                    ModelName = table.Column<string>(type: "text", nullable: true),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    ExternalUserId = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<long>(type: "bigint", nullable: false),
                    EndedAt = table.Column<long>(type: "bigint", nullable: true),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    LastMessageAt = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmbeddingItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EmbeddingJobId = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    FaqId = table.Column<string>(type: "text", nullable: false),
                    ChunkText = table.Column<string>(type: "text", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "text", nullable: false),
                    EmbeddingVector = table.Column<float[]>(type: "real[]", nullable: true),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmbeddingJobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    JobNo = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TriggerType = table.Column<string>(type: "text", nullable: false),
                    TotalFaqCount = table.Column<int>(type: "integer", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailCount = table.Column<int>(type: "integer", nullable: false),
                    JsonFilePath = table.Column<string>(type: "text", nullable: true),
                    VectorVersion = table.Column<string>(type: "text", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<long>(type: "bigint", nullable: true),
                    FinishedAt = table.Column<long>(type: "bigint", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    TriggeredByType = table.Column<string>(type: "text", nullable: true),
                    TriggeredById = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmbeddingLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    EmbeddingJobId = table.Column<string>(type: "text", nullable: false),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    DetailJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmbeddingSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    ActiveVectorVersion = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqCandidates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    AnalysisJobId = table.Column<string>(type: "text", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    SuggestedCategory = table.Column<string>(type: "text", nullable: true),
                    SuggestedTags = table.Column<string>(type: "text", nullable: true),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SourceConversationIds = table.Column<string>(type: "text", nullable: true),
                    AiModel = table.Column<string>(type: "text", nullable: true),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    AiSuggestionJson = table.Column<string>(type: "text", nullable: true),
                    GeneratedAt = table.Column<long>(type: "bigint", nullable: false),
                    ReviewedByType = table.Column<string>(type: "text", nullable: true),
                    ReviewedById = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqCategories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    FaqId = table.Column<string>(type: "text", nullable: false),
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    OperatedByType = table.Column<string>(type: "text", nullable: false),
                    OperatedById = table.Column<string>(type: "text", nullable: false),
                    OperatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Faqs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<string>(type: "text", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    SourceCandidateId = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByType = table.Column<string>(type: "text", nullable: true),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    UpdatedByType = table.Column<string>(type: "text", nullable: true),
                    UpdatedById = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faqs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LineBotInputLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    ExternalUserId = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    MessageText = table.Column<string>(type: "text", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "text", nullable: false),
                    ReplyToken = table.Column<string>(type: "text", nullable: true),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineBotInputLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LineBotOutputLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    ExternalUserId = table.Column<string>(type: "text", nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    MessageText = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: true),
                    SourceFaqId = table.Column<string>(type: "text", nullable: true),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: true),
                    ModelName = table.Column<string>(type: "text", nullable: true),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineBotOutputLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformPermissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    GroupName = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformRolePermissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PlatformRoleId = table.Column<string>(type: "text", nullable: false),
                    PlatformPermissionId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformRolePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformUserRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PlatformUserId = table.Column<string>(type: "text", nullable: false),
                    PlatformRoleId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUserRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Department = table.Column<string>(type: "text", nullable: true),
                    JobTitle = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<long>(type: "bigint", nullable: true),
                    LastLoginIp = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformUserVendorScopes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PlatformUserId = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUserVendorScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SettingKey = table.Column<string>(type: "text", nullable: false),
                    SettingValue = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorAccounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    LoginEmail = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    AccountName = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<long>(type: "bigint", nullable: true),
                    LastLoginIp = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ContactName = table.Column<string>(type: "text", nullable: true),
                    ContactEmail = table.Column<string>(type: "text", nullable: true),
                    ContactPhone = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    LineChannelId = table.Column<string>(type: "text", nullable: true),
                    LineChannelSecret = table.Column<string>(type: "text", nullable: true),
                    LineAccessToken = table.Column<string>(type: "text", nullable: true),
                    OpenAiApiKey = table.Column<string>(type: "text", nullable: true),
                    ChatModel = table.Column<string>(type: "text", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "text", nullable: true),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    AnswerThreshold = table.Column<double>(type: "double precision", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Remark = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiFaqAnalysisJobs");

            migrationBuilder.DropTable(
                name: "BulkJobs");

            migrationBuilder.DropTable(
                name: "ConversationMessages");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "EmbeddingItems");

            migrationBuilder.DropTable(
                name: "EmbeddingJobs");

            migrationBuilder.DropTable(
                name: "EmbeddingLogs");

            migrationBuilder.DropTable(
                name: "EmbeddingSettings");

            migrationBuilder.DropTable(
                name: "FaqCandidates");

            migrationBuilder.DropTable(
                name: "FaqCategories");

            migrationBuilder.DropTable(
                name: "FaqLogs");

            migrationBuilder.DropTable(
                name: "Faqs");

            migrationBuilder.DropTable(
                name: "LineBotInputLogs");

            migrationBuilder.DropTable(
                name: "LineBotOutputLogs");

            migrationBuilder.DropTable(
                name: "PlatformPermissions");

            migrationBuilder.DropTable(
                name: "PlatformRolePermissions");

            migrationBuilder.DropTable(
                name: "PlatformRoles");

            migrationBuilder.DropTable(
                name: "PlatformUserRoles");

            migrationBuilder.DropTable(
                name: "PlatformUsers");

            migrationBuilder.DropTable(
                name: "PlatformUserVendorScopes");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "VendorAccounts");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.AlterColumn<string>(
                name: "LineUserId",
                table: "LineEventLogs",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "LineEventLogs",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TargetId",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Payload",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "AnalysisDetails",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CompletionId = table.Column<string>(type: "text", nullable: false),
                    Flags = table.Column<string>(type: "text", nullable: false),
                    JobId = table.Column<string>(type: "text", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    Similarity = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisDetails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisJobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    FinishedAt = table.Column<long>(type: "bigint", nullable: true),
                    Params = table.Column<string>(type: "text", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    ResultSummary = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatEmbeddings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ChatMessageId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatEmbeddings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    Meta = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Completions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Complate = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    VenuesId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Completions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Feedbacks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedbacks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LineEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EventId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Processed = table.Column<bool>(type: "boolean", nullable: false),
                    RawEvent = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LineUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    IsFollowed = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LineUserId = table.Column<string>(type: "text", nullable: false),
                    PictureUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnmatchedQueries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    Query = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnmatchedQueries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorStaffUsers_VendorId_LineUserId",
                table: "VendorStaffUsers",
                columns: new[] { "VendorId", "LineUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageRoutes_CreatedAt",
                table: "MessageRoutes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MessageRoutes_InputLogId",
                table: "MessageRoutes",
                column: "InputLogId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageRoutes_Route",
                table: "MessageRoutes",
                column: "Route");

            migrationBuilder.CreateIndex(
                name: "IX_MessageRoutes_VendorId",
                table: "MessageRoutes",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageResults_CreatedAt",
                table: "MessageResults",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LineEventLogs_LineUserId",
                table: "LineEventLogs",
                column: "LineUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FaqQueryLogs_CreatedAt",
                table: "FaqQueryLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationStates_VendorId_SourceType_ConversationId",
                table: "ConversationStates",
                columns: new[] { "VendorId", "SourceType", "ConversationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisJobs_Status",
                table: "AnalysisJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_UserId",
                table: "ChatMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_CreatedAt",
                table: "Feedbacks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LineEvents_EventId",
                table: "LineEvents",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_LineUsers_LineUserId",
                table: "LineUsers",
                column: "LineUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnmatchedQueries_CreatedAt",
                table: "UnmatchedQueries",
                column: "CreatedAt");
        }
    }
}

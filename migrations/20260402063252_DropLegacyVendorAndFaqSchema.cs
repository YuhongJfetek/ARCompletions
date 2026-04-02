using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ARCompletions.migrations
{
    /// <inheritdoc />
    public partial class DropLegacyVendorAndFaqSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop legacy tables defensively (use IF EXISTS so migration can run on databases
            // where some or all of these tables were never created or already removed).
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AiFaqAnalysisJobs\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"BulkJobs\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"ConversationMessages\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"Conversations\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"ConversationStates\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"EmbeddingItems\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"EmbeddingJobs\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"EmbeddingLogs\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"EmbeddingSettings\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"FaqAliases\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"FaqCandidates\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"FaqCategories\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"FaqLogs\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"FaqQueryLogs\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"Faqs\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"LineBotInputLogs\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"LineBotOutputLogs\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"LineEventLogs\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"MessageResults\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"MessageRoutes\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"PlatformUserVendorScopes\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"VendorAccounts\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"Vendors\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"VendorStaffUsers\";");

            migrationBuilder.CreateTable(
                name: "bot_audit_logs",
                columns: table => new
                {
                    AuditId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActionType = table.Column<string>(type: "text", nullable: true),
                    TargetTable = table.Column<string>(type: "text", nullable: true),
                    TargetId = table.Column<string>(type: "text", nullable: true),
                    ChangedBy = table.Column<string>(type: "text", nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    Note = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_audit_logs", x => x.AuditId);
                });

            migrationBuilder.CreateTable(
                name: "bot_constants_config",
                columns: table => new
                {
                    ConfigKey = table.Column<string>(type: "text", nullable: false),
                    ConfigValue = table.Column<string>(type: "text", nullable: true),
                    ValueType = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_constants_config", x => x.ConfigKey);
                });

            migrationBuilder.CreateTable(
                name: "bot_context_messages",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Ts = table.Column<long>(type: "bigint", nullable: false),
                    MetaRoute = table.Column<string>(type: "text", nullable: true),
                    MetaMatchedFaqId = table.Column<string>(type: "text", nullable: true),
                    MetaCandidates = table.Column<string>(type: "text", nullable: true),
                    MetaAliasTerm = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_context_messages", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "bot_conversation_settings",
                columns: table => new
                {
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_conversation_settings", x => new { x.SourceType, x.ConversationId });
                });

            migrationBuilder.CreateTable(
                name: "bot_conversation_state",
                columns: table => new
                {
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    HandoffStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HandoffUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PendingDisambiguationIds = table.Column<string>(type: "text", nullable: true),
                    PendingDisambiguationRoute = table.Column<string>(type: "text", nullable: true),
                    PendingDisambiguationAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastStaffMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_conversation_state", x => new { x.SourceType, x.ConversationId });
                });

            migrationBuilder.CreateTable(
                name: "bot_faq_aliases",
                columns: table => new
                {
                    AliasId = table.Column<Guid>(type: "uuid", nullable: false),
                    Term = table.Column<string>(type: "text", nullable: false),
                    Synonyms = table.Column<string>(type: "text", nullable: true),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    FaqIds = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_faq_aliases", x => x.AliasId);
                });

            migrationBuilder.CreateTable(
                name: "bot_faq_embeddings",
                columns: table => new
                {
                    EmbeddingId = table.Column<Guid>(type: "uuid", nullable: false),
                    FaqId = table.Column<string>(type: "text", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: true),
                    SearchText = table.Column<string>(type: "text", nullable: true),
                    CategoryKey = table.Column<string>(type: "text", nullable: true),
                    EmbeddingProvider = table.Column<string>(type: "text", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "text", nullable: false),
                    VectorDim = table.Column<int>(type: "integer", nullable: false),
                    Embedding = table.Column<double[]>(type: "double precision[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RebuiltAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_faq_embeddings", x => x.EmbeddingId);
                });

            migrationBuilder.CreateTable(
                name: "bot_faq_items",
                columns: table => new
                {
                    FaqId = table.Column<string>(type: "text", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: true),
                    CategoryKey = table.Column<string>(type: "text", nullable: true),
                    Subcategory = table.Column<string>(type: "text", nullable: true),
                    Keywords = table.Column<string>(type: "text", nullable: true),
                    QueryExamples = table.Column<string>(type: "text", nullable: true),
                    AliasTerms = table.Column<string>(type: "text", nullable: true),
                    Sources = table.Column<string>(type: "text", nullable: true),
                    NeedsHumanHandoff = table.Column<bool>(type: "boolean", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    SearchTextCache = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_faq_items", x => x.FaqId);
                });

            migrationBuilder.CreateTable(
                name: "bot_incoming_events",
                columns: table => new
                {
                    EventRowId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RawEventJson = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: true),
                    MessageType = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: true),
                    LineUserId = table.Column<string>(type: "text", nullable: true),
                    LineGroupId = table.Column<string>(type: "text", nullable: true),
                    LineRoomId = table.Column<string>(type: "text", nullable: true),
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    ReplyToken = table.Column<string>(type: "text", nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_incoming_events", x => x.EventRowId);
                });

            migrationBuilder.CreateTable(
                name: "bot_llm_logs",
                columns: table => new
                {
                    LlmLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventRowId = table.Column<long>(type: "bigint", nullable: true),
                    LogEvent = table.Column<string>(type: "text", nullable: true),
                    Task = table.Column<string>(type: "text", nullable: true),
                    FaqId = table.Column<string>(type: "text", nullable: true),
                    MatchedBy = table.Column<string>(type: "text", nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    ReplyMode = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_llm_logs", x => x.LlmLogId);
                });

            migrationBuilder.CreateTable(
                name: "bot_message_routes",
                columns: table => new
                {
                    RouteRowId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventRowId = table.Column<long>(type: "bigint", nullable: true),
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: true),
                    LineUserId = table.Column<string>(type: "text", nullable: true),
                    LogEvent = table.Column<string>(type: "text", nullable: true),
                    Route = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    MatchedFaqId = table.Column<string>(type: "text", nullable: true),
                    MatchedScore = table.Column<double>(type: "double precision", nullable: true),
                    MatchedBy = table.Column<string>(type: "text", nullable: true),
                    FaqCategory = table.Column<string>(type: "text", nullable: true),
                    TopFaqIds = table.Column<string>(type: "text", nullable: true),
                    AliasTerm = table.Column<string>(type: "text", nullable: true),
                    ReplyText = table.Column<string>(type: "text", nullable: true),
                    LlmEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    NeedsHumanHandoff = table.Column<bool>(type: "boolean", nullable: true),
                    IsStaffTriggered = table.Column<bool>(type: "boolean", nullable: true),
                    ContextCountBefore = table.Column<int>(type: "integer", nullable: true),
                    ContextCountAfter = table.Column<int>(type: "integer", nullable: true),
                    LogClass = table.Column<string>(type: "text", nullable: true),
                    LogGroup = table.Column<string>(type: "text", nullable: true),
                    LogPriority = table.Column<string>(type: "text", nullable: true),
                    LogUseful = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_message_routes", x => x.RouteRowId);
                });

            migrationBuilder.CreateTable(
                name: "bot_staff_users",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_staff_users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "bot_system_prompts",
                columns: table => new
                {
                    PromptKey = table.Column<string>(type: "text", nullable: false),
                    PromptText = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_system_prompts", x => x.PromptKey);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bot_audit_logs");

            migrationBuilder.DropTable(
                name: "bot_constants_config");

            migrationBuilder.DropTable(
                name: "bot_context_messages");

            migrationBuilder.DropTable(
                name: "bot_conversation_settings");

            migrationBuilder.DropTable(
                name: "bot_conversation_state");

            migrationBuilder.DropTable(
                name: "bot_faq_aliases");

            migrationBuilder.DropTable(
                name: "bot_faq_embeddings");

            migrationBuilder.DropTable(
                name: "bot_faq_items");

            migrationBuilder.DropTable(
                name: "bot_incoming_events");

            migrationBuilder.DropTable(
                name: "bot_llm_logs");

            migrationBuilder.DropTable(
                name: "bot_message_routes");

            migrationBuilder.DropTable(
                name: "bot_staff_users");

            migrationBuilder.DropTable(
                name: "bot_system_prompts");

            migrationBuilder.CreateTable(
                name: "AiFaqAnalysisJobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    ConversationCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    DateFrom = table.Column<long>(type: "bigint", nullable: false),
                    DateTo = table.Column<long>(type: "bigint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    FinishedAt = table.Column<long>(type: "bigint", nullable: true),
                    JobNo = table.Column<string>(type: "text", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: true),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TriggeredById = table.Column<string>(type: "text", nullable: true),
                    TriggeredByType = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false)
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
                    Action = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    FilterJson = table.Column<string>(type: "text", nullable: true),
                    Initiator = table.Column<string>(type: "text", nullable: false),
                    ProcessedCount = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TotalCount = table.Column<long>(type: "bigint", nullable: false),
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
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: true),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    ExternalMessageId = table.Column<string>(type: "text", nullable: true),
                    MessageText = table.Column<string>(type: "text", nullable: true),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: true),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    ReplyToken = table.Column<string>(type: "text", nullable: true),
                    SenderId = table.Column<string>(type: "text", nullable: true),
                    SenderName = table.Column<string>(type: "text", nullable: true),
                    SourceFaqId = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false)
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
                    Channel = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    EndedAt = table.Column<long>(type: "bigint", nullable: true),
                    ExternalUserId = table.Column<string>(type: "text", nullable: false),
                    LastMessageAt = table.Column<long>(type: "bigint", nullable: true),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConversationStates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    BotEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    HandoffUntil = table.Column<long>(type: "bigint", nullable: true),
                    LastStaffMessageAt = table.Column<long>(type: "bigint", nullable: true),
                    PendingDisambiguationAt = table.Column<long>(type: "bigint", nullable: true),
                    PendingDisambiguationIds = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmbeddingItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ChunkText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    EmbeddingJobId = table.Column<string>(type: "text", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "text", nullable: false),
                    EmbeddingVector = table.Column<float[]>(type: "real[]", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    FaqId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false)
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
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    FailCount = table.Column<int>(type: "integer", nullable: false),
                    FinishedAt = table.Column<long>(type: "bigint", nullable: true),
                    JobNo = table.Column<string>(type: "text", nullable: false),
                    JsonFilePath = table.Column<string>(type: "text", nullable: true),
                    ModelName = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    TotalFaqCount = table.Column<int>(type: "integer", nullable: false),
                    TriggerType = table.Column<string>(type: "text", nullable: false),
                    TriggeredById = table.Column<string>(type: "text", nullable: true),
                    TriggeredByType = table.Column<string>(type: "text", nullable: true),
                    VectorVersion = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false)
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
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    DetailJson = table.Column<string>(type: "text", nullable: true),
                    EmbeddingJobId = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false)
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
                    ActiveVectorVersion = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqAliases",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    FaqIds = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    Synonyms = table.Column<string>(type: "text", nullable: true),
                    Term = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqAliases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqCandidates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AiModel = table.Column<string>(type: "text", nullable: true),
                    AiSuggestionJson = table.Column<string>(type: "text", nullable: true),
                    AnalysisJobId = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    GeneratedAt = table.Column<long>(type: "bigint", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    Question = table.Column<string>(type: "text", nullable: false),
                    ReviewedAt = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedById = table.Column<string>(type: "text", nullable: true),
                    ReviewedByType = table.Column<string>(type: "text", nullable: true),
                    SourceConversationIds = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SuggestedCategory = table.Column<string>(type: "text", nullable: true),
                    SuggestedTags = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false)
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
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Sort = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false)
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
                    ActionType = table.Column<string>(type: "text", nullable: false),
                    AfterJson = table.Column<string>(type: "text", nullable: true),
                    BeforeJson = table.Column<string>(type: "text", nullable: true),
                    FaqId = table.Column<string>(type: "text", nullable: false),
                    OperatedAt = table.Column<long>(type: "bigint", nullable: false),
                    OperatedById = table.Column<string>(type: "text", nullable: false),
                    OperatedByType = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqQueryLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    DetailsJson = table.Column<string>(type: "text", nullable: true),
                    MatchedFaqId = table.Column<string>(type: "text", nullable: true),
                    MessageResultId = table.Column<string>(type: "text", nullable: true),
                    QueryText = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqQueryLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Faqs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    CreatedByType = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    SourceCandidateId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedById = table.Column<string>(type: "text", nullable: true),
                    UpdatedByType = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
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
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "text", nullable: false),
                    ExternalUserId = table.Column<string>(type: "text", nullable: false),
                    MessageText = table.Column<string>(type: "text", nullable: true),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<long>(type: "bigint", nullable: false),
                    ReplyToken = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false)
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
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: true),
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    ExternalUserId = table.Column<string>(type: "text", nullable: false),
                    MessageText = table.Column<string>(type: "text", nullable: true),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    ModelName = table.Column<string>(type: "text", nullable: true),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<long>(type: "bigint", nullable: false),
                    SourceFaqId = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineBotOutputLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LineEventLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: true),
                    LineUserId = table.Column<string>(type: "text", nullable: true),
                    MessageResultId = table.Column<string>(type: "text", nullable: true),
                    MessageType = table.Column<string>(type: "text", nullable: true),
                    ProcessingStatus = table.Column<string>(type: "text", nullable: true),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineEventLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageResults",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: true),
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    MatchedBy = table.Column<string>(type: "text", nullable: true),
                    MatchedFaqId = table.Column<string>(type: "text", nullable: true),
                    MatchedScore = table.Column<double>(type: "double precision", nullable: true),
                    MessageId = table.Column<string>(type: "text", nullable: true),
                    MessageRouteId = table.Column<string>(type: "text", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Route = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageRoutes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    EmbeddingModel = table.Column<string>(type: "text", nullable: true),
                    FaqCategory = table.Column<string>(type: "text", nullable: true),
                    InputLogId = table.Column<string>(type: "text", nullable: true),
                    LlmEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LlmModel = table.Column<string>(type: "text", nullable: true),
                    MatchedBy = table.Column<string>(type: "text", nullable: true),
                    MatchedFaqId = table.Column<string>(type: "text", nullable: true),
                    MatchedScore = table.Column<double>(type: "double precision", nullable: true),
                    NeedsHandoff = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    ReplyText = table.Column<string>(type: "text", nullable: true),
                    Route = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageRoutes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformUserVendorScopes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    PlatformUserId = table.Column<string>(type: "text", nullable: false),
                    VendorId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUserVendorScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorAccounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    AccountName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginAt = table.Column<long>(type: "bigint", nullable: true),
                    LastLoginIp = table.Column<string>(type: "text", nullable: true),
                    LoginEmail = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false)
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
                    Address = table.Column<string>(type: "text", nullable: true),
                    AnswerThreshold = table.Column<double>(type: "double precision", nullable: true),
                    ChatModel = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ContactEmail = table.Column<string>(type: "text", nullable: true),
                    ContactName = table.Column<string>(type: "text", nullable: true),
                    ContactPhone = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LineAccessToken = table.Column<string>(type: "text", nullable: true),
                    LineChannelId = table.Column<string>(type: "text", nullable: true),
                    LineChannelSecret = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OpenAiApiKey = table.Column<string>(type: "text", nullable: true),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    Remark = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorStaffUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<long>(type: "bigint", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LineUserId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true),
                    VendorId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorStaffUsers", x => x.Id);
                });
        }
    }
}

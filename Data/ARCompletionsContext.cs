using Microsoft.EntityFrameworkCore;
using ARCompletions.Domain;

namespace ARCompletions.Data;

/// <summary>
/// 新版 DbContext：對應目前設計的 21 個 Domain Model。
/// 目前只負責模型宣告，不主動更新資料庫或執行遷移。
/// </summary>
public class ARCompletionsContext : DbContext
{
    public ARCompletionsContext(DbContextOptions<ARCompletionsContext> options)
        : base(options)
    {
    }

    // 平台 / 權限相關
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<PlatformRole> PlatformRoles => Set<PlatformRole>();
    public DbSet<PlatformUserRole> PlatformUserRoles => Set<PlatformUserRole>();
    public DbSet<PlatformPermission> PlatformPermissions => Set<PlatformPermission>();
    public DbSet<PlatformRolePermission> PlatformRolePermissions => Set<PlatformRolePermission>();

    // 系統設定與審計
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<ARCompletions.Domain.AuditLog> AuditLogs => Set<ARCompletions.Domain.AuditLog>();

    // JFETEK bot schema (13 tables)
    public DbSet<BotFaqItem> BotFaqItems => Set<BotFaqItem>();
    public DbSet<BotFaqAlias> BotFaqAliases => Set<BotFaqAlias>();
    public DbSet<BotFaqEmbedding> BotFaqEmbeddings => Set<BotFaqEmbedding>();
    public DbSet<BotStaffUser> BotStaffUsers => Set<BotStaffUser>();
    public DbSet<BotConversationSetting> BotConversationSettings => Set<BotConversationSetting>();
    public DbSet<BotConversationState> BotConversationStates => Set<BotConversationState>();
    public DbSet<BotContextMessage> BotContextMessages => Set<BotContextMessage>();
    public DbSet<BotSystemPrompt> BotSystemPrompts => Set<BotSystemPrompt>();
    public DbSet<BotIncomingEvent> BotIncomingEvents => Set<BotIncomingEvent>();
    public DbSet<BotMessageRoute> BotMessageRoutes => Set<BotMessageRoute>();
    public DbSet<BotLlmLog> BotLlmLogs => Set<BotLlmLog>();
    public DbSet<BotConstantsConfig> BotConstantsConfigs => Set<BotConstantsConfig>();
    public DbSet<BotAuditLog> BotAuditLogs => Set<BotAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // JFETEK bot tables
        modelBuilder.Entity<BotFaqItem>(e =>
        {
            e.ToTable("bot_faq_items");
            e.HasKey(x => x.FaqId);
        });

        modelBuilder.Entity<BotFaqAlias>(e =>
        {
            e.ToTable("bot_faq_aliases");
            e.HasKey(x => x.AliasId);
        });

        modelBuilder.Entity<BotFaqEmbedding>(e =>
        {
            e.ToTable("bot_faq_embeddings");
            e.HasKey(x => x.EmbeddingId);
        });

        modelBuilder.Entity<BotStaffUser>(e =>
        {
            e.ToTable("bot_staff_users");
            e.HasKey(x => x.UserId);
        });

        modelBuilder.Entity<BotConversationSetting>(e =>
        {
            e.ToTable("bot_conversation_settings");
            e.HasKey(x => new { x.SourceType, x.ConversationId });
        });

        modelBuilder.Entity<BotConversationState>(e =>
        {
            e.ToTable("bot_conversation_state");
            e.HasKey(x => new { x.SourceType, x.ConversationId });
        });

        modelBuilder.Entity<BotContextMessage>(e =>
        {
            e.ToTable("bot_context_messages");
            e.HasKey(x => x.MessageId);
        });

        modelBuilder.Entity<BotSystemPrompt>(e =>
        {
            e.ToTable("bot_system_prompts");
            e.HasKey(x => x.PromptKey);
        });

        modelBuilder.Entity<BotIncomingEvent>(e =>
        {
            e.ToTable("bot_incoming_events");
            e.HasKey(x => x.EventRowId);
        });

        modelBuilder.Entity<BotMessageRoute>(e =>
        {
            e.ToTable("bot_message_routes");
            e.HasKey(x => x.RouteRowId);
        });

        modelBuilder.Entity<BotLlmLog>(e =>
        {
            e.ToTable("bot_llm_logs");
            e.HasKey(x => x.LlmLogId);
        });

        modelBuilder.Entity<BotConstantsConfig>(e =>
        {
            e.ToTable("bot_constants_config");
            e.HasKey(x => x.ConfigKey);
        });

        modelBuilder.Entity<BotAuditLog>(e =>
        {
            e.ToTable("bot_audit_logs");
            e.HasKey(x => x.AuditId);
        });
    }
}

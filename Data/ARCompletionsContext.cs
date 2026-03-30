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
    public DbSet<PlatformUserVendorScope> PlatformUserVendorScopes => Set<PlatformUserVendorScope>();

    // 廠商與帳號
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorAccount> VendorAccounts => Set<VendorAccount>();

    // Line Bot / 對話紀錄
    public DbSet<LineBotInputLog> LineBotInputLogs => Set<LineBotInputLog>();
    public DbSet<LineBotOutputLog> LineBotOutputLogs => Set<LineBotOutputLog>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    // AI FAQ 分析
    public DbSet<AiFaqAnalysisJob> AiFaqAnalysisJobs => Set<AiFaqAnalysisJob>();
    public DbSet<FaqCandidate> FaqCandidates => Set<FaqCandidate>();

    // FAQ 與分類、紀錄
    public DbSet<FaqCategory> FaqCategories => Set<FaqCategory>();
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<FaqLog> FaqLogs => Set<FaqLog>();

    // Embedding 任務與紀錄
    public DbSet<EmbeddingJob> EmbeddingJobs => Set<EmbeddingJob>();
    public DbSet<EmbeddingItem> EmbeddingItems => Set<EmbeddingItem>();
    public DbSet<EmbeddingLog> EmbeddingLogs => Set<EmbeddingLog>();
    public DbSet<EmbeddingSetting> EmbeddingSettings => Set<EmbeddingSetting>();

    // Message results and FAQ query logs
    public DbSet<ARCompletions.Domain.MessageResult> MessageResults => Set<ARCompletions.Domain.MessageResult>();
    public DbSet<ARCompletions.Domain.FaqQueryLog> FaqQueryLogs => Set<ARCompletions.Domain.FaqQueryLog>();

    // Phase 2 additions
    public DbSet<ConversationState> ConversationStates => Set<ConversationState>();
    public DbSet<FaqAlias> FaqAliases => Set<FaqAlias>();
    public DbSet<MessageRoute> MessageRoutes => Set<MessageRoute>();
    public DbSet<VendorStaffUser> VendorStaffUsers => Set<VendorStaffUser>();

    // 系統設定
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    // 審計日誌
    public DbSet<ARCompletions.Domain.AuditLog> AuditLogs => Set<ARCompletions.Domain.AuditLog>();
    // 批次工作
    public DbSet<ARCompletions.Domain.BulkJob> BulkJobs => Set<ARCompletions.Domain.BulkJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 目前採用預設慣例：
        // - string 主鍵 Id 交由上層產生（如 GUID）
        // - long 時間欄位存 Unix timestamp
        // 之後如需索引/關聯/限制，可在此處逐步補上 Fluent API 設定。
    }
}

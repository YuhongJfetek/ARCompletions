# 導覽列對應清單（Audit）

產生時間: 2026-04-01

說明: 此檔列出「導覽列」下每一子選單項目，對應的 Controller 與 Views 檔案、是否已實作，以及需要補齊或注意的項目。

## FAQ 管理（JFETEK / Bot Schema）

- FAQ 列表（Bot FAQ Items）
  - Controller: `Areas/Admin/Controllers/BotFaqItemsController.cs`
  - Views: `Areas/Admin/Views/BotFaqItems/*`
  - Status: New (Admin-side; based on bot_faq_items)
  - Notes: 以 JFETEK `bot_faq_items` 為唯一 FAQ 主資料來源；舊 Vendor 區 FAQ / FAQCategories 已移除。

- FAQ 別名（Bot FAQ Aliases）
  - Controller: `Areas/Admin/Controllers/BotFaqAliasesController.cs`
  - Views: `Areas/Admin/Views/BotFaqAliases/*`
  - Status: New (Admin-side; based on bot_faq_aliases)
  - Notes: 採用 Enabled flag 控制啟用/停用，不做實體刪除；舊 FaqAliasesController + Views 已移除。

- FAQ 查詢紀錄（Legacy 已移除）
  - Controller: `Areas/Admin/Controllers/FaqQueryLogsController.cs`
  - Views: `Areas/Admin/Views/FaqQueryLogs/Index.cshtml`, `Details.cshtml`
  - Status: Exists (包含 ExportCsv)
  - Notes: 篩選與匯出已支援。

- FAQ 操作紀錄
  - Controller: `Areas/Admin/Controllers/FaqLogsController.cs`
  - Views: `Areas/Admin/Views/FaqLogs/Index.cshtml`, `Details.cshtml`
  - Status: Exists
  - Notes: 顯示操作者、時間與 Before/After（Details）支援。

- AI FAQ 分析任務
  - Controller: `Areas/Admin/Controllers/AiFaqAnalysisJobsController.cs`
  - Views: `Areas/Admin/Views/AiFaqAnalysisJobs/Index.cshtml`, `Details.cshtml`
  - Status: Exists
  - Notes: 支援查看候選並重試/排程。

## Embedding 管理

- Embedding 任務（Legacy 已完全移除）
  - Controller: `Areas/Admin/Controllers/EmbeddingJobsController.cs`
  - Views: `Areas/Admin/Views/EmbeddingJobs/*`
  - Status: Removed
  - Notes: 由 BotEmbeddingsController 及 `bot_faq_embeddings` 取代，亦不再提供 BulkJobs/BulkRetry 功能。

- Bot Embeddings（Bot FAQ 向量）
  - Controller: `Areas/Admin/Controllers/BotEmbeddingsController.cs`
  - Views: `Areas/Admin/Views/BotEmbeddings/*`
  - Status: New (Admin-side; based on bot_faq_embeddings)
  - Notes: 顯示 FAQ 向量重建紀錄與目前 Active 向量。

- Embedding 設定（Legacy Vendor-based 設定已移除）
  - Controller: MISSING (`EmbeddingSettingsController` not found)
  - Views: MISSING
  - Status: Missing
  - Notes: DB 存在 `EmbeddingSettings`，建議新增 Admin 管理頁面以設定 active model、vector version、threshold。

- 同步狀態
  - Controller: MISSING (`EmbeddingJobs/SyncStatus` action not found)
  - Views: MISSING
  - Status: Missing
  - Notes: layout 中已連結到 `SyncStatus`，需要實作不一致清單與一鍵補建功能。

## 訊息管理

- 訊息輸入紀錄
  - Controller: `Areas/Admin/Controllers/ConversationsController.cs`
  - Views: `Areas/Admin/Views/Conversations/Index.cshtml`, `Details.cshtml`
  - Status: Exists
  - Notes: 支援篩選；Raw JSON 顯示在 Details（確認附件欄位顯示）。

- Bot 會話設定 / 狀態（JFETEK / Bot Schema）
  - Controller: `Areas/Admin/Controllers/BotConversationsController.cs`
  - Views: `Areas/Admin/Views/BotConversations/*`（待補）
  - Status: New (Admin-side; based on bot_conversation_settings / bot_conversation_state)
  - Notes: 管理每個 SourceType / Conversation 的啟用、handoff 暫停狀態。

- 訊息結果（Legacy）
  - Controller: (已移除) `Areas/Admin/Controllers/MessageResultsController.cs`
  - Views: `Areas/Admin/Views/MessageResults/*`（Legacy）
  - Status: Removed (replaced by BotMessages + BotAuditLogs)
  - Notes: 由 BotMessages Routes/Events 與 BotAuditLogs 提供查詢。

- Bot 訊息路由 / 事件（JFETEK / Bot Schema）
  - Controller: `Areas/Admin/Controllers/BotMessagesController.cs`
  - Views: `Areas/Admin/Views/BotMessages/*`（待補）
  - Status: New (Admin-side; based on bot_incoming_events / bot_message_routes)
  - Notes: Routes 動作對應 bot_message_routes，Events 對應 bot_incoming_events。

- 訊息路由（Legacy）
  - Controller: (已移除) `Areas/Admin/Controllers/MessageRoutesController.cs`
  - Views: `Areas/Admin/Views/MessageRoutes/*`（Legacy）
  - Status: Removed (replaced by BotMessages Routes)
  - Notes: BotMessages Routes 使用 bot_message_routes 表提供相同資訊。

- 會話狀態（Legacy）
  - Controller: (已移除) `Areas/Admin/Controllers/ConversationStatesController.cs`
  - Views: `Areas/Admin/Views/ConversationStates/*`（Legacy）
  - Status: Removed (replaced by BotConversations)
  - Notes: BotConversations 使用 bot_conversation_settings/state 提供管理。

- API 設定（Legacy）
  - Controller: (已移除) `Areas/Admin/Controllers/MessageApiController.cs`
  - Views: `Areas/Admin/Views/MessageApi/*`（Legacy）
  - Status: Removed (integration via /internal/v1/bot/query)
  - Notes: 新的 webhook / Node.js 端改打 Internal API，不再使用此 UI。

## 系統管理

- 廠商管理
  - Controller: `Areas/Admin/Controllers/VendorsController.cs`
  - Views: `Areas/Admin/Views/Vendors/*`
  - Status: Exists
  - Notes: Create/Edit 支援 OpenAI Key 欄位（建議改成安全存儲/遮罩）。

- 廠商員工（Legacy）
  - Controller: (已移除) `Areas/Admin/Controllers/VendorStaffUsersController.cs`
  - Views: `Areas/Admin/Views/VendorStaffUsers/*`（Legacy）
  - Status: Removed (replaced by BotStaffUsers)
  - Notes: Staff 身分改由 bot_staff_users 與 BotStaffUsersController 管理。

- Bot Staff 使用者
  - Controller: `Areas/Admin/Controllers/BotStaffUsersController.cs`
  - Views: `Areas/Admin/Views/BotStaffUsers/*`（待補）
  - Status: New (Admin-side; based on bot_staff_users)
  - Notes: 管理可接手人工客服 / 查看對話的 Staff 名單與角色。

- Bot System Prompts
  - Controller: `Areas/Admin/Controllers/BotSystemPromptsController.cs`
  - Views: `Areas/Admin/Views/BotSystemPrompts/*`（待補）
  - Status: New (Admin-side; based on bot_system_prompts)
  - Notes: 管理 LLM 使用的 System Prompt 版本（如 general / handoff / staff_reply 等）。

- Bot 常數設定
  - Controller: `Areas/Admin/Controllers/BotConstantsConfigController.cs`
  - Views: `Areas/Admin/Views/BotConstantsConfig/*`（待補）
  - Status: New (Admin-side; based on bot_constants_config)
  - Notes: 管理 JFETEK bot flow 相關常數（如閾值、超時、預設 route 等）。

- Bot 操作稽核紀錄
  - Controller: `Areas/Admin/Controllers/BotAuditLogsController.cs`
  - Views: `Areas/Admin/Views/BotAuditLogs/*`（待補）
  - Status: New (Admin-side; based on bot_audit_logs)
  - Notes: 記錄 Bot 管理相關的敏感變更（FAQ/Prompt/Config 等）。

- 平台帳號
  - Controller: `Areas/Admin/Controllers/PlatformUsersController.cs`
  - Views: `Areas/Admin/Views/PlatformUsers/*`
  - Status: Exists
  - Notes: 密碼欄位目前直接儲存到 `PasswordHash`，建議改為安全散列流程與 password-reset 流程。

- 批次工作
  - Controller: `Areas/Admin/Controllers/BulkJobsController.cs`
  - Views: `Areas/Admin/Views/BulkJobs/*`
  - Status: Exists
  - Notes: 支援檢視與重試相關操作。

- 系統設定
  - Controller: `Areas/Admin/Controllers/SystemSettingsController.cs`
  - Views: `Areas/Admin/Views/SystemSettings/*`
  - Status: Exists
  - Notes: 可管理 System Prompt 與全域參數（建議補上變更歷史/操作紀錄視圖）。

## 高優先建議修補清單（簡短）
- 新增 Admin 端 `Faqs` 管理（或共用 Vendor 的管理）以符合「總部管理 FAQ」需求。 
- 新增 `EmbeddingSettingsController` + Views（active model、vector version、threshold）。
- 實作 `EmbeddingJobs/SyncStatus` 與「一鍵補建」功能。 
- 將 `FaqAliases` 的刪除行為改為 `IsActive`（停用不刪除）。
- 在 FAQ Edit/Create 成功後自動 enqueue Embedding 重建任務。 
- 改善平台密碼與 OpenAI Key 的儲存方式（密碼散列、Key 遮罩/安全存儲）。

---

檔案同時輸出為 CSV：`navigation_mapping.csv`（專案根目錄）。

若要，我可以把這些建議逐項轉成 issues 或直接開始實作其中某一項（請選擇）。

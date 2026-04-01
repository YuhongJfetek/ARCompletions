# 導覽列對應清單（Audit）

產生時間: 2026-04-01

說明: 此檔列出「導覽列」下每一子選單項目，對應的 Controller 與 Views 檔案、是否已實作，以及需要補齊或注意的項目。

## FAQ 管理

- FAQ 列表
  - Controller: `Areas/Vendor/Controllers/FaqsController.cs` (Vendor 區)
  - Views: `Areas/Vendor/Views/Faqs/Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`
  - Status: Exists (Vendor-side)
  - Notes: 若需總部可管理，需新增 Admin `FaqsController` 或共用該功能到 Admin。

- FAQ 分類
  - Controller: `Areas/Vendor/Controllers/FaqCategoriesController.cs` (Vendor 區)
  - Views: `Areas/Vendor/Views/FaqCategories/*`
  - Status: Exists (Vendor-side); Admin-side missing
  - Notes: 若總部需管理，新增 Admin 介面。

- FAQ 別名
  - Controller: `Areas/Admin/Controllers/FaqAliasesController.cs`
  - Views: `Areas/Admin/Views/FaqAliases/*`
  - Status: Exists
  - Notes: `Delete` 實作為實刪；建議改為切換 `IsActive`（需求為停用不刪除）。

- FAQ 查詢紀錄
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

- Embedding 任務
  - Controller: `Areas/Admin/Controllers/EmbeddingJobsController.cs`
  - Views: `Areas/Admin/Views/EmbeddingJobs/Index.cshtml`, `Details.cshtml`
  - Status: Exists (Index/Details/ExportCsv/Retry/BulkRetry/TriggerManual/SetActiveVector 已實作)
  - Notes: 支援手動觸發與批次重試。

- 重建進度
  - Controller: Partially provided by `EmbeddingJobsController` (Details / logs)
  - Views: `Areas/Admin/Views/EmbeddingJobs/Details.cshtml`
  - Status: Partial
  - Notes: 若需即時進度（已處理/總數/失敗數），需前端輪詢或 WebSocket/SignalR 支援與相對 endpoint。

- Embedding 設定
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

- 訊息結果
  - Controller: `Areas/Admin/Controllers/MessageResultsController.cs`
  - Views: `Areas/Admin/Views/MessageResults/Index.cshtml`, `Details.cshtml`
  - Status: Exists
  - Notes: Details 顯示 payload、複製按鈕；包含 ExportCsv。

- 訊息路由
  - Controller: `Areas/Admin/Controllers/MessageRoutesController.cs`
  - Views: `Areas/Admin/Views/MessageRoutes/*`
  - Status: Exists
  - Notes: 支援篩選與 ExportCsv。

- 會話狀態
  - Controller: `Areas/Admin/Controllers/ConversationStatesController.cs`
  - Views: `Areas/Admin/Views/ConversationStates/*`
  - Status: Exists
  - Notes: 支援狀態切換與管理。

- API 設定
  - Controller: `Areas/Admin/Controllers/MessageApiController.cs`
  - Views: `Areas/Admin/Views/MessageApi/Index.cshtml`
  - Status: Exists
  - Notes: Webhook 測試、遮罩、儲存設定位於此；支援 Ajax 測試。

## 系統管理

- 廠商管理
  - Controller: `Areas/Admin/Controllers/VendorsController.cs`
  - Views: `Areas/Admin/Views/Vendors/*`
  - Status: Exists
  - Notes: Create/Edit 支援 OpenAI Key 欄位（建議改成安全存儲/遮罩）。

- 廠商員工
  - Controller: `Areas/Admin/Controllers/VendorStaffUsersController.cs`
  - Views: `Areas/Admin/Views/VendorStaffUsers/*`
  - Status: Exists
  - Notes: 支援啟用/停用與角色設定。

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

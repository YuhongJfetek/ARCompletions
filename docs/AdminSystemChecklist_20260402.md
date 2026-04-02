# Admin 系統版本確認表

- 系統網址：https://arcompletions.onrender.com/Admin  
- 確認日期：2026-04-02  
- 確認人員：_______________

---

## 一、系統基本資訊

| 項目 | 確認內容 | 狀態 |
| --- | --- | --- |
| 部署平台 | Render（onrender.com） | ✅ 已確認 |
| 後端框架 | .NET Core（Kestrel） | ✅ 已確認（Swagger header 顯示） |
| API 文件 | `/swagger/index.html` | ✅ 可存取 |
| 管理員帳號 | `admin@jfetek.com` / 總部管理員 | ✅ 已確認登入 |
| 角色 | `Administrator` / `Engineering` | ✅ 已確認 |

## 二、主選單模組確認

> 註：以下實際路徑以目前 Program / Routing 及 View 為準

| 模組 | 路徑 | 畫面截圖 | 功能可用 |
| --- | --- | --- | --- |
| 首頁 | `/Admin` | ☐ | ☐ |
| Bot 管理 > FAQ 管理 | `/Admin/BotFaqItems` | ✅ 有截圖 | ✅ |
| Bot 管理 > FAQ Alias | `/Admin/BotFaqAliases` | ✅ 有截圖 | ✅ |
| Bot 管理 > FAQ Embeddings | `/Admin/BotEmbeddings` | ✅ 有截圖 | ✅ |
| Bot 管理 > 會話設定 / 狀態 | `/Admin/BotConversations` | ☐ | ☐ |
| Bot 管理 > 訊息路由紀錄 | `/Admin/BotMessages/Routes` | ☐ | ☐ |
| Bot 管理 > Incoming Events | `/Admin/BotMessages/Events` | ☐ | ☐ |
| Bot 管理 > 訊息 API 設定 | `/Admin/MessageApi` | ☐ | ☐ |
| 系統管理 > 平台帳號 | `/Admin/PlatformUsers` | ✅ 有截圖 | ✅ |
| 系統管理 > 系統設定 | `/Admin/SystemSettings` | ☐ | ☐ |
| 場地 | `/Venues` | ☐ | ☐ |

## 三、FAQ 管理功能確認

頁面：`/Admin/BotFaqItems`

| 功能規格要求 | 實際狀態 | 備註 |
| --- | --- | --- |
| 列表顯示 `FaqId / Question / CategoryKey / Enabled / CreatedAt / UpdatedAt` | ✅ | 欄位正確 |
| 關鍵字搜尋 問題 / 答案 / 類別 | ✅ | 有搜尋框 |
| `CategoryKey` 篩選下拉選單 | ✅ | 有下拉選單 |
| 分頁每頁筆數可選（25） | ✅ | 顯示 25 |
| 搜尋按鈕點擊執行搜尋 | ✅ | 有搜尋按鈕 |
| 新增 FAQ 按鈕 | ✅ | 有綠色按鈕 |
| 每筆有「編輯」按鈕 | ✅ | 有編輯按鈕 |
| 每筆有「檢視」按鈕 | ✅ | 有檢視按鈕 |
| FAQ 詳細欄位完整（規格內各欄位） | ✅ | 全部欄位正確 |
| 總筆數 157 筆 | ✅ | 已 SQL 驗證 |

## 四、FAQ Alias 功能確認

頁面：`/Admin/BotFaqAliases`

| 功能規格要求 | 實際狀態 | 備註 |
| --- | --- | --- |
| 列表顯示 `AliasId / Term / Mode / Enabled` | ✅ |  |
| 詳細頁欄位完整 | ✅ | 全部正確 |
| `Mode` 欄位值 `direct / disambiguation` | ✅ | SBIR 為 `disambiguation` |
| `Synonyms` 多語支援（中英文） | ✅ | 已確認 |
| `FaqIds` 對應正確 FAQ | ✅ | `faq_subsidy_001~007` |
| 編輯按鈕可編輯 | ✅ | 有「編輯」按鈕 |
| 回列表按鈕可返回 | ✅ | 有「回列表」按鈕 |
| 總筆數 14 筆 | ✅ | 已 SQL 驗證 |

## 五、FAQ Embeddings 功能確認

頁面：`/Admin/BotEmbeddings`

| 功能規格要求 | 實際狀態 | 備註 |
| --- | --- | --- |
| 詳細頁欄位 `EmbeddingId / FaqId / Provider / Model / VectorDim / IsActive / CreatedAt / RebuiltAt` | ✅ | 全部正確 |
| `Provider` 值 `local_hash` | ✅ | 已確認 |
| `Model` 值 `legacy_hash64` | ✅ | 已確認 |
| `VectorDim` 值 `64` | ✅ | 已確認 |
| `IsActive` 值 `是` | ✅ | 已確認 |
| 總筆數 157 筆 | ✅ | 已 SQL 驗證 |

## 六、系統管理功能確認

頁面：`/Admin/PlatformUsers`

| 功能規格要求 | 實際狀態 | 備註 |
| --- | --- | --- |
| 列表欄位 姓名 / Email / 部門 / 職稱 / 啟用 | ✅ | 欄位正確 |
| 新增使用者按鈕可新增 | ✅ | 有藍色按鈕 |
| 每筆有「編輯」按鈕 | ✅ |  |
| 預設管理員 `admin@jfetek.com / Administrator` | ✅ | 已確認 |

## 七、Swagger API 確認

頁面：`/swagger/index.html`

| API 方法 | 狀態 |
| --- | --- |
| `POST /internal/v1/bot/query` | ✅ 已顯示 |
| `POST /internal/v1/events` | ✅ 已顯示 |
| `POST /internal/v1/routes` | ✅ 已顯示 |
| `POST /internal/v1/llm-logs` | ✅ 已顯示 |
| Schemas（`BotQueryRequest` 等） | ✅ 已顯示 |

## 八、待確認項目（尚未截圖驗證）

| 頁面 / 功能 | 待確認內容 | 優先級 |
| --- | --- | --- |
| 會話設定 / 狀態 | 群組 BOT enabled 狀態顯示、`handoff_until` 查看 | 🔴 高 |
| 訊息路由紀錄 | 條件篩選、`route/reason` 顯示 | 🔴 高 |
| Incoming Events | 事件紀錄列表與詳細 | 🔴 高 |
| 訊息 API 設定 | `BACKEND_API_KEY` 管理 | 🟡 中 |
| 系統設定 | 常數門檻調整（`FAQ_HIGH_CONFIDENCE` 等） | 🟡 中 |
| FAQ 管理 > 新增 FAQ | 新增表單欄位完整性 | 🟡 中 |
| FAQ 管理 > 編輯 FAQ | 編輯後 cache 是否失效 | 🟡 中 |
| FAQ 管理 > 批次匯入 | 匯入按鈕是否存在 | 🟡 中 |
| Embedding > 重建按鈕 | 單筆 / 全量重建是否可用 | 🟡 中 |
| 場地 | 功能用途確認 | 🟢 低 |

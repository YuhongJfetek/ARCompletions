Embedding（專案內部實作）

- 方法：採工程式 hash-based n-gram 混合 term 權重的向量化（非訓練模型）。
  - 步驟：文字正規化 → 產生 1~3-gram 與詞項 tokens → 對每個 token 做 FNV-like hash 得到索引與符號（+/-）→ 依權重把值累加到固定維度陣列（目前 64 維）→ L2 正規化。
  - 說明：這是工程式 hash feature → 向量的方式，不靠神經網路訓練，成本低且不依賴外部服務。

- 分數計算：
  - 基底分數：cosine(queryVec, faqVec)
  - 額外加權：token overlap（乘以係數 0.025，並設上限）與 category bonus（例如 +0.08）
  - 最終處理：將 finalScore cap 到 0.99；只有 finalScore > 0 的候選會回傳。

- 影響分數的設計決定：向量維度、token 權重（ngram vs term）、hash 分派導致稀疏分布、token-overlap 權重與 category bonus，以及後端閾值，這些共同決定接受哪些候選。

- 優點與限制：
  - 優點：實作簡單、無需外部 ML 依賴、成本低。
  - 限制：語意表現有限、分數尺度偏小（常見為 0.0x）、不如訓練型 embedding 適合語義推理。

- 可調參數（可修改）：
  - 向量維度（預設 64）
  - 詞項與 ngram 權重
  - tokenOverlap 係數與上限
  - category bonus 值
  - 回傳與直接回覆閾值（direct thresholds）

- 建議改進：
  - 若要更貼近語意且分數尺度正常化：採用訓練過的語意 embedding（如 OpenAI Embeddings、Sentence-BERT／SBERT）或提高 hash 向量維度並重新調整權重與閾值。
  - 若保留 hash 方法：以大量樣本做分布分析，並把閾值設為分位數驅動（percentile-driven thresholds）。


LLM（專案整合現況）

- 是否使用：是，LLM 為可選功能，由環境變數控制。若設定 `OPENAI_API_KEY` 與 `OPENAI_MODEL`，系統會呼叫 OpenAI Responses API。

- 預設／範例 model：範例設定為 `gpt-4o-mini`（或透過環境變數指定其他 model）。呼叫使用 Responses API，並送入 instructions 與 input 結構。

- 用途：
  - FAQ 回覆的「重寫／潤飾」
  - staff fallback（當 FAQ 未命中或需要擴充回覆時）
  - 規則：限制回覆長度、套用 grounding 檢查，以避免產生未授權或不實資訊。

- 工程與保護措施：
  - 有超時控制、錯誤紀錄、grounding 檢查（例如 token overlap / jaccard 相似度）與 fallback 策略。
  - 若未設定金鑰則不會呼叫 OpenAI。

- 建議：
  - 若大量使用，加入呼叫費用監控、速率限制、response content 檢查，以及明確的 feature 開關（環境變數 + feature-flag）。


流程總結（建議採用的分析流程）

1. 詞彙處理與向量化：對使用者 query 做文字正規化，產生 tokens（1–3 gram 與詞項），依專案 hash-based 方法輸出 64 維向量並 L2 正規化。
2. 候選檢索：以 cosine(queryVec, faqVec) 計算基礎相似度，加入 tokenOverlap 與 category bonus，計算 finalScore，過濾 finalScore > 0 的候選。
3. 分類決策：以 configurable 閾值判定直接回覆（faq）或回傳候選（candidates / disambiguation）。閾值可由分位數分析或線上調整得出。
4. 若啟用 LLM：在 FAQ 命中或作為 fallback 時，依需要呼叫 OpenAI Responses API 進行重寫或補足，套用 grounding checks 與長度限制，並在失敗時回退到原始 FAQ 答案或 staff 路徑。
5. 監控與迭代：收集召回候選與最終回覆的分數分布，定期評估並調整向量化權重與閾值；若引入訓練型 embedding，做 A/B 比較來驗證改進效果。


要點摘要：
- 專案目前使用的是工程式 hash-based embedding（非 ML 模型），分數尺度偏小為預期行為。
- 若要顯著改善語意與分數範圍，建議導入訓練型 embeddings 或調整 hash 設計（維度、權重、閾值）。
- LLM 為選用功能，僅在設定金鑰與 model 時啟用；需注意呼叫安全、防濫用與費用監控。
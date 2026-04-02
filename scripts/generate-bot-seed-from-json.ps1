param(
    [string]$DataRoot = "C:\\Users\\jamie\\Downloads\\linebot-jfetek-bot(1)\\app\\data",
    [string]$OutFile = "C:\\Users\\jamie\\Desktop\\bot_seed_from_json.sql"
)

# Make JSON / IO errors fail fast
$ErrorActionPreference = 'Stop'

function Escape-SqlLiteral {
    param([string]$s)
    if ($null -eq $s) { return $null }
    return $s -replace '''', ''''''  # escape single quotes
}

function To-JsonLiteral {
    param([object]$value)
    if ($null -eq $value) { return '[]' }
    $json = $value | ConvertTo-Json -Compress
    if ([string]::IsNullOrWhiteSpace($json)) { $json = '[]' }
    return $json
}

$sb = New-Object System.Text.StringBuilder

$null = $sb.AppendLine('-- Seed script generated from JFETEK JSON data')
$null = $sb.AppendLine('BEGIN;')
$null = $sb.AppendLine('')
$null = $sb.AppendLine('-- Defaults for CreatedAt and UUID PKs')
$null = $sb.AppendLine('ALTER TABLE bot_faq_items      ALTER COLUMN "CreatedAt" SET DEFAULT NOW();')
$null = $sb.AppendLine('ALTER TABLE bot_faq_aliases    ALTER COLUMN "CreatedAt" SET DEFAULT NOW();')
$null = $sb.AppendLine('ALTER TABLE bot_faq_embeddings ALTER COLUMN "CreatedAt" SET DEFAULT NOW();')
$null = $sb.AppendLine('ALTER TABLE bot_staff_users    ALTER COLUMN "CreatedAt" SET DEFAULT NOW();')
$null = $sb.AppendLine('ALTER TABLE bot_faq_aliases    ALTER COLUMN "AliasId"    SET DEFAULT gen_random_uuid();')
$null = $sb.AppendLine('ALTER TABLE bot_faq_embeddings ALTER COLUMN "EmbeddingId" SET DEFAULT gen_random_uuid();')
$null = $sb.AppendLine('')

# 1) faq.json -> bot_faq_items
$faqPath = Join-Path $DataRoot 'faq.json'
Write-Host "Loading faq.json from" $faqPath
$faqData = Get-Content -LiteralPath $faqPath -Raw | ConvertFrom-Json

$null = $sb.AppendLine('-- faq.json -> bot_faq_items')
$null = $sb.AppendLine('INSERT INTO bot_faq_items ("FaqId", "Question", "Answer", "Category", "CategoryKey", "Subcategory", "Keywords", "QueryExamples", "AliasTerms", "Sources", "NeedsHumanHandoff", "Enabled") VALUES')

$faqRows = @()
foreach ($r in $faqData) {
    $id          = Escape-SqlLiteral $r.id
    $question    = Escape-SqlLiteral $r.question
    $answer      = Escape-SqlLiteral $r.answer
    $category    = if ($r.category)    { '''' + (Escape-SqlLiteral $r.category)    + '''' } else { 'NULL' }
    $categoryKey = if ($r.categoryKey) { '''' + (Escape-SqlLiteral $r.categoryKey) + '''' } else { 'NULL' }
    $subcategory = if ($r.subcategory) { '''' + (Escape-SqlLiteral $r.subcategory) + '''' } else { 'NULL' }

    $keywordsJson       = To-JsonLiteral $r.keywords
    $queryExamplesJson  = To-JsonLiteral $r.queryExamples
    $aliasTermsJson     = To-JsonLiteral $r.aliasTerms
    $sourcesJson        = To-JsonLiteral $r.sources

    $keywords      = '''' + (Escape-SqlLiteral $keywordsJson)      + ''''
    $queryExamples = '''' + (Escape-SqlLiteral $queryExamplesJson) + ''''
    $aliasTerms    = '''' + (Escape-SqlLiteral $aliasTermsJson)    + ''''
    $sources       = '''' + (Escape-SqlLiteral $sourcesJson)       + ''''

    $needsHuman = if ($r.needsHumanHandoff) { 'true' } else { 'false' }
    $enabled    = if ($r.enabled)           { 'true' } else { 'false' }

    $row = "('$id','$question','$answer',$category,$categoryKey,$subcategory,$keywords,$queryExamples,$aliasTerms,$sources,$needsHuman,$enabled)"
    $faqRows += $row
}

$null = $sb.AppendLine(($faqRows -join ",`n") + ';')
$null = $sb.AppendLine("-- debug: faq.json rows = $($faqRows.Count)")
$null = $sb.AppendLine('')

# 2) faq-aliases.json -> bot_faq_aliases
$aliasPath = Join-Path $DataRoot 'faq-aliases.json'
Write-Host "Loading faq-aliases.json from" $aliasPath
$aliasData = Get-Content -LiteralPath $aliasPath -Raw | ConvertFrom-Json

$null = $sb.AppendLine('-- faq-aliases.json -> bot_faq_aliases')
$null = $sb.AppendLine('INSERT INTO bot_faq_aliases ("Term", "Synonyms", "Mode", "FaqIds", "Enabled") VALUES')

$aliasRows = @()
foreach ($a in $aliasData) {
    $term   = Escape-SqlLiteral $a.term
    $mode   = Escape-SqlLiteral $a.mode
    $syn    = To-JsonLiteral $a.synonyms
    $faqIds = To-JsonLiteral $a.faqIds

    $synLit    = '''' + (Escape-SqlLiteral $syn)    + ''''
    $faqIdsLit = '''' + (Escape-SqlLiteral $faqIds) + ''''
    $enabled   = 'true'  # 原始檔沒有 enabled 欄位，一律啟用

    $row = "('$term',$synLit,'$mode',$faqIdsLit,$enabled)"
    $aliasRows += $row
}

$null = $sb.AppendLine(($aliasRows -join ",`n") + ' ON CONFLICT ("Term") DO UPDATE SET "Synonyms" = EXCLUDED."Synonyms", "Mode" = EXCLUDED."Mode", "FaqIds" = EXCLUDED."FaqIds", "Enabled" = EXCLUDED."Enabled", "UpdatedAt" = NOW();')
$null = $sb.AppendLine("-- debug: faq-aliases.json rows = $($aliasRows.Count)")
$null = $sb.AppendLine('')

# 3) embeddings.json -> bot_faq_embeddings
$embPath = Join-Path $DataRoot 'embeddings.json'
Write-Host "Loading embeddings.json from" $embPath
$embData = Get-Content -LiteralPath $embPath -Raw | ConvertFrom-Json

$null = $sb.AppendLine('-- embeddings.json -> bot_faq_embeddings')
$null = $sb.AppendLine('INSERT INTO bot_faq_embeddings ("FaqId","Question","SearchText","CategoryKey","EmbeddingProvider","EmbeddingModel","VectorDim","Embedding","IsActive") VALUES')

$embRows = @()
foreach ($e in $embData) {
    $id          = Escape-SqlLiteral $e.id
    $question    = if ($e.question)    { '''' + (Escape-SqlLiteral $e.question)    + '''' } else { 'NULL' }
    $searchText  = if ($e.text)        { '''' + (Escape-SqlLiteral $e.text)        + '''' } else { 'NULL' }
    $categoryKey = if ($e.categoryKey) { '''' + (Escape-SqlLiteral $e.categoryKey) + '''' } else { 'NULL' }

    $vec = [string]::Join(',', $e.embedding)
    $row = "('$id',$question,$searchText,$categoryKey,'local_hash','legacy_hash64',64,ARRAY[$vec]::float8[],true)"
    $embRows += $row
}

$null = $sb.AppendLine(($embRows -join ",`n") + ';')
$null = $sb.AppendLine("-- debug: embeddings.json rows = $($embRows.Count)")
$null = $sb.AppendLine('')

# 4) staff-users.json -> bot_staff_users
$staffPath = Join-Path $DataRoot 'staff-users.json'
Write-Host "Loading staff-users.json from" $staffPath
$staffData = Get-Content -LiteralPath $staffPath -Raw | ConvertFrom-Json

$null = $sb.AppendLine('-- staff-users.json -> bot_staff_users')
$null = $sb.AppendLine('INSERT INTO bot_staff_users ("UserId","Name","Role","Enabled") VALUES')

$staffRows = @()
foreach ($s in $staffData) {
    $userId = Escape-SqlLiteral $s.userId
    $name   = Escape-SqlLiteral $s.name
    $role   = Escape-SqlLiteral $s.role
    $enabled = if ($s.enabled) { 'true' } else { 'false' }

    $row = "('$userId','$name','$role',$enabled)"
    $staffRows += $row
}

$null = $sb.AppendLine(($staffRows -join ",`n") + ' ON CONFLICT ("UserId") DO UPDATE SET "Name" = EXCLUDED."Name", "Role" = EXCLUDED."Role", "Enabled" = EXCLUDED."Enabled", "UpdatedAt" = NOW();')
$null = $sb.AppendLine("-- debug: staff-users.json rows = $($staffRows.Count)")
$null = $sb.AppendLine('')

# 5) system-prompts.json -> bot_system_prompts
$sysPath = Join-Path $DataRoot 'system-prompts.json'
Write-Host "Loading system-prompts.json from" $sysPath
$sysData = Get-Content -LiteralPath $sysPath -Raw | ConvertFrom-Json

$null = $sb.AppendLine('-- system-prompts.json -> bot_system_prompts')
$null = $sb.AppendLine('INSERT INTO bot_system_prompts ("PromptKey","PromptText","UpdatedAt","UpdatedBy") VALUES')

$sysRows = @()
foreach ($prop in $sysData.PSObject.Properties) {
    $key = Escape-SqlLiteral $prop.Name
    $text = Escape-SqlLiteral [string]$prop.Value
    $row = "('$key','$text',NOW(),'seed')"
    $sysRows += $row
}

$null = $sb.AppendLine(($sysRows -join ",`n") + ' ON CONFLICT ("PromptKey") DO UPDATE SET "PromptText" = EXCLUDED."PromptText", "UpdatedAt" = EXCLUDED."UpdatedAt", "UpdatedBy" = EXCLUDED."UpdatedBy";')
$null = $sb.AppendLine("-- debug: system-prompts.json rows = $($sysRows.Count)")
$null = $sb.AppendLine('')

$null = $sb.AppendLine('COMMIT;')

$directory = [System.IO.Path]::GetDirectoryName($OutFile)
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

$sb.ToString() | Set-Content -LiteralPath $OutFile -Encoding UTF8

Write-Host "Generated seed SQL:" $OutFile

#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Populates a BookStack instance with AI-generated content about a given topic.

.DESCRIPTION
    Calls an Ollama LLM to generate a book outline (chapters + pages) for a topic,
    then creates the content in BookStack via its REST API.

    The script generates:
      - 1 book
      - 3–5 chapters, each with 2–4 pages of markdown content

.PARAMETER Topic
    The subject matter for the generated book. E.g. "Kubernetes", "Medieval History", "Sourdough Bread".

.PARAMETER BookStackBaseUrl
    Base URL of the BookStack instance. Defaults to $env:BOOKSTACK_BASE_URL or http://localhost:6875.

.PARAMETER TokenId
    BookStack API token ID. Defaults to $env:BOOKSTACK_TOKEN_ID.

.PARAMETER TokenSecret
    BookStack API token secret. Defaults to $env:BOOKSTACK_TOKEN_SECRET.

.PARAMETER OllamaBaseUrl
    Base URL of the Ollama instance. Defaults to $env:OLLAMA_BASE_URL or http://localhost:11434.

.PARAMETER OllamaModel
    Ollama model to use for generation. Defaults to $env:OLLAMA_MODEL or llama3.2.

.PARAMETER DryRun
    When set, prints what would be created without making any API calls.

.EXAMPLE
    ./Seed-BookStack.ps1 -Topic "Docker Networking"

.EXAMPLE
    ./Seed-BookStack.ps1 -Topic "Rust Programming" -OllamaModel "mistral" -DryRun

.EXAMPLE
    # Using host Ollama from WSL2 (Ollama on Windows listening on 0.0.0.0):
    ./Seed-BookStack.ps1 -Topic "Rust Programming" -OllamaBaseUrl "http://host-gateway:11434"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "Topic for the generated book")]
    [ValidateNotNullOrEmpty()]
    [string] $Topic,

    [string] $BookStackBaseUrl = ($env:BOOKSTACK_BASE_URL ?? 'http://localhost:6875'),
    [string] $TokenId         = ($env:BOOKSTACK_TOKEN_ID ?? ''),
    [string] $TokenSecret     = ($env:BOOKSTACK_TOKEN_SECRET ?? ''),
    [string] $OllamaBaseUrl   = ($env:OLLAMA_BASE_URL ?? 'http://localhost:11434'),
    [string] $OllamaModel     = ($env:OLLAMA_MODEL ?? 'phi4-mini-reasoning'),

    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-Step([string]$Message) {
    Write-Host "  --> $Message" -ForegroundColor Cyan
}

function Write-Success([string]$Message) {
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Warn([string]$Message) {
    Write-Host "  [!!] $Message" -ForegroundColor Yellow
}

function Get-BookStackAuthHeader {
    if ([string]::IsNullOrWhiteSpace($TokenId) -or [string]::IsNullOrWhiteSpace($TokenSecret)) {
        throw "BookStack API credentials not set. Provide -TokenId / -TokenSecret or set BOOKSTACK_TOKEN_ID / BOOKSTACK_TOKEN_SECRET."
    }
    return @{ Authorization = "Token ${TokenId}:${TokenSecret}" }
}

function Invoke-BookStackApi {
    param(
        [string] $Method,
        [string] $Path,
        [hashtable] $Body = $null
    )

    $url     = "$($BookStackBaseUrl.TrimEnd('/'))/api/$Path"
    $headers = Get-BookStackAuthHeader
    $headers['Content-Type'] = 'application/json'
    $headers['Accept']       = 'application/json'

    $params = @{
        Method  = $Method
        Uri     = $url
        Headers = $headers
    }

    if ($null -ne $Body) {
        $params['Body'] = ($Body | ConvertTo-Json -Depth 10 -Compress)
    }

    try {
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        $statusCode = $_.Exception.Response?.StatusCode?.value__ ?? 'unknown'
        throw "BookStack API call failed [$Method $Path] — HTTP $statusCode : $($_.Exception.Message)"
    }
}

function Invoke-Ollama {
    param(
        [string] $Prompt,
        [string] $SystemPrompt = ''
    )

    $url  = "$($OllamaBaseUrl.TrimEnd('/'))/api/generate"
    $body = @{
        model  = $OllamaModel
        prompt = $Prompt
        stream = $false
    }
    if ($SystemPrompt) {
        $body['system'] = $SystemPrompt
    }

    Write-Step "Calling Ollama ($OllamaModel)..."

    try {
        $response = Invoke-RestMethod -Method Post -Uri $url `
            -ContentType 'application/json' `
            -Body ($body | ConvertTo-Json -Compress)
        return $response.response
    }
    catch {
        $statusCode = $_.Exception.Response?.StatusCode?.value__ ?? 'unknown'
        throw "Ollama API call failed — HTTP $statusCode : $($_.Exception.Message)"
    }
}

function ConvertFrom-OllamaJson {
    param([string] $RawText)

    # Strip markdown code fences if the model wrapped the JSON
    $cleaned = $RawText -replace '(?s)^```(?:json)?\s*', '' -replace '(?s)\s*```\s*$', ''
    $cleaned = $cleaned.Trim()

    try {
        return $cleaned | ConvertFrom-Json -Depth 20
    }
    catch {
        throw "Could not parse JSON from Ollama response. Raw text:`n$RawText"
    }
}

# ---------------------------------------------------------------------------
# Step 1 — Generate book outline
# ---------------------------------------------------------------------------

$outlineSystem = @'
You are a technical writing assistant. Respond ONLY with valid JSON — no markdown fences,
no explanation, no preamble. The JSON must exactly match this schema:

{
  "title": "string — book title",
  "description": "string — 1–2 sentence book description",
  "chapters": [
    {
      "title": "string — chapter title",
      "description": "string — 1 sentence chapter description",
      "pages": [
        { "title": "string — page title" }
      ]
    }
  ]
}

Produce 3 to 5 chapters. Each chapter must have 2 to 4 pages.
'@

$outlinePrompt = "Create a detailed technical book outline about: $Topic"

Write-Host ""
Write-Host "BookStack Seed Script" -ForegroundColor White
Write-Host "Topic : $Topic" -ForegroundColor White
Write-Host "Model : $OllamaModel @ $OllamaBaseUrl" -ForegroundColor White
Write-Host "Target: $BookStackBaseUrl" -ForegroundColor White
if ($DryRun) { Write-Warn "DRY RUN — no BookStack API calls will be made" }
Write-Host ""

Write-Host "[1/3] Generating book outline..." -ForegroundColor Magenta
$outlineRaw = Invoke-Ollama -Prompt $outlinePrompt -SystemPrompt $outlineSystem
$outline    = ConvertFrom-OllamaJson -RawText $outlineRaw

Write-Success "Book: $($outline.title)"
Write-Host    "      $($outline.description)"
Write-Host ""
foreach ($ch in $outline.chapters) {
    Write-Host "  Chapter: $($ch.title)" -ForegroundColor Yellow
    foreach ($pg in $ch.pages) {
        Write-Host "    Page : $($pg.title)"
    }
}
Write-Host ""

# ---------------------------------------------------------------------------
# Step 2 — Generate page content for every page
# ---------------------------------------------------------------------------

$pageSystem = @'
You are a technical writing assistant producing wiki documentation.
Write clear, well-structured markdown with headings, bullet points, and code examples where relevant.
Do not include a top-level H1 heading — the page title is supplied separately.
Aim for 300–500 words of genuinely useful content.
'@

Write-Host "[2/3] Generating page content..." -ForegroundColor Magenta

# Build a flat list of (chapter, page, content) so we can report progress
$allPages = [System.Collections.Generic.List[hashtable]]::new()

$chapterIndex = 0
foreach ($chapter in $outline.chapters) {
    $chapterIndex++
    foreach ($page in $chapter.pages) {
        $pagePrompt = "Book: $($outline.title)`nChapter: $($chapter.title)`nPage title: $($page.title)`n`nWrite the wiki page content."
        Write-Step "[$chapterIndex] $($chapter.title) / $($page.title)"
        $content = Invoke-Ollama -Prompt $pagePrompt -SystemPrompt $pageSystem
        $allPages.Add(@{
            ChapterTitle = $chapter.title
            ChapterDesc  = $chapter.description
            PageTitle    = $page.title
            Markdown     = $content
        })
    }
}

Write-Success "Generated $($allPages.Count) pages"
Write-Host ""

# ---------------------------------------------------------------------------
# Step 3 — Create content in BookStack
# ---------------------------------------------------------------------------

Write-Host "[3/3] Creating content in BookStack..." -ForegroundColor Magenta

if ($DryRun) {
    Write-Warn "DRY RUN — skipping all BookStack API calls"
    Write-Host ""
    Write-Host "Would create:" -ForegroundColor White
    Write-Host "  Book    : $($outline.title)"
    $chapterTitles = $outline.chapters | ForEach-Object { $_.title } | Select-Object -Unique
    foreach ($ct in $chapterTitles) {
        Write-Host "  Chapter : $ct"
    }
    foreach ($p in $allPages) {
        Write-Host "  Page    : [$($p.ChapterTitle)] $($p.PageTitle)"
    }
    Write-Host ""
    Write-Success "Dry run complete"
    exit 0
}

# Create book
Write-Step "Creating book: $($outline.title)"
$book = Invoke-BookStackApi -Method POST -Path 'books' -Body @{
    name        = $outline.title
    description = $outline.description
}
$bookId = $book.id
Write-Success "Book created (id=$bookId)"

# Create chapters, deduplicating by title (chapters repeat across pages list)
$chapterIds = @{}
$chapterIndex = 0
foreach ($chapter in $outline.chapters) {
    if ($chapterIds.ContainsKey($chapter.title)) { continue }
    $chapterIndex++
    Write-Step "Creating chapter $chapterIndex : $($chapter.title)"
    $ch = Invoke-BookStackApi -Method POST -Path 'chapters' -Body @{
        book_id     = $bookId
        name        = $chapter.title
        description = $chapter.description
    }
    $chapterIds[$chapter.title] = $ch.id
    Write-Success "Chapter created (id=$($ch.id))"
}

# Create pages
$pageCount = 0
foreach ($p in $allPages) {
    $pageCount++
    $chId = $chapterIds[$p.ChapterTitle]
    Write-Step "Creating page $pageCount/$($allPages.Count) : $($p.PageTitle)"
    $null = Invoke-BookStackApi -Method POST -Path 'pages' -Body @{
        chapter_id = $chId
        name       = $p.PageTitle
        markdown   = $p.Markdown
    }
}
Write-Success "All $pageCount pages created"

Write-Host ""
Write-Host "Done! Open $($BookStackBaseUrl.TrimEnd('/'))/books to see your new book." -ForegroundColor Green
Write-Host ""

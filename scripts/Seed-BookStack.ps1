#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Populates a BookStack instance with AI-generated content and/or fixed golden-dataset evaluation pages.

.DESCRIPTION
    Calls an LLM to generate a book outline (chapters + pages) for a topic and creates the content
    in BookStack via its REST API. Optionally seeds a fixed developer-knowledge book used as the
    golden dataset for FEAT-0060 semantic search quality evaluation (v2: ASP.NET Core + .NET topics
    sourced from Microsoft Learn, CC BY 4.0).

    When -Topic is supplied the script generates:
      - 1 LLM-authored book
      - 3–5 chapters, each with 2–4 pages of Markdown content

    When -GoldenDataset is supplied the script additionally creates:
      - 1 evaluation book ("Developer Knowledge Evaluation Dataset")
      - 4 chapters with 15 fixed-content pages (11 Markdown fetched from MS Learn, 4 WYSIWYG)

    Either -Topic or -GoldenDataset (or both) must be specified.

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

.PARAMETER GoldenDataset
    When set, creates a fixed "Developer Knowledge Evaluation Dataset" book (v2: ASP.NET Core +
    .NET topics from Microsoft Learn) used as the golden dataset for FEAT-0060. Can be combined
    with -Topic.

.PARAMETER DryRun
    When set, prints what would be created without making any API calls.

.EXAMPLE
    ./Seed-BookStack.ps1 -Topic "Docker Networking"

.EXAMPLE
    ./Seed-BookStack.ps1 -Topic "Rust Programming" -OllamaModel "mistral" -DryRun

.EXAMPLE
    # Seed only the golden-dataset evaluation pages (no LLM required):
    ./Seed-BookStack.ps1 -GoldenDataset

.EXAMPLE
    # Seed both an LLM-generated book and the golden-dataset evaluation pages:
    ./Seed-BookStack.ps1 -Topic "Docker Networking" -GoldenDataset

.EXAMPLE
    # Using host Ollama from WSL2 (Ollama on Windows listening on 0.0.0.0):
    ./Seed-BookStack.ps1 -Topic "Rust Programming" -OllamaBaseUrl "http://host-gateway:11434"
#>

[CmdletBinding()]
param(
    [Parameter(HelpMessage = "Topic for the generated book. Required unless -GoldenDataset is specified.")]
    [string] $Topic = '',

    [string] $BookStackBaseUrl = ($env:BOOKSTACK_BASE_URL ?? 'http://localhost:6875'),
    [string] $TokenId         = ($env:BOOKSTACK_TOKEN_ID ?? ''),
    [string] $TokenSecret     = ($env:BOOKSTACK_TOKEN_SECRET ?? ''),

    # LLM provider: ollama | groq | mistral
    [ValidateSet('ollama','groq','mistral')]
    [string] $Provider        = ($env:LLM_PROVIDER ?? 'ollama'),
    [string] $LlmModel        = ($env:LLM_MODEL ?? ''),
    [string] $ApiKey          = ($env:LLM_API_KEY ?? ''),
    # Ollama-specific (ignored for groq/mistral)
    [string] $OllamaBaseUrl   = ($env:OLLAMA_BASE_URL ?? 'http://localhost:11434'),

    [switch] $GoldenDataset,
    [switch] $DryRun
)

# Resolve default model per provider if not overridden
if (-not $LlmModel) {
    $LlmModel = switch ($Provider) {
        'groq'    { 'llama-3.3-70b-versatile' }
        'mistral' { 'mistral-small-latest' }
        default   { $env:OLLAMA_MODEL ?? 'phi4-mini-reasoning' }
    }
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $GoldenDataset -and [string]::IsNullOrWhiteSpace($Topic)) {
    throw "Provide -Topic <string>, -GoldenDataset, or both."
}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Get-Timestamp { return (Get-Date -Format 'HH:mm:ss') }

function Write-Step([string]$Message) {
    Write-Host "  [$(Get-Timestamp)] --> $Message" -ForegroundColor Cyan
}

function Write-Success([string]$Message) {
    Write-Host "  [$(Get-Timestamp)]  OK  $Message" -ForegroundColor Green
}

function Write-Warn([string]$Message) {
    Write-Host "  [$(Get-Timestamp)]  !!  $Message" -ForegroundColor Yellow
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

function Invoke-Llm {
    param(
        [string] $Prompt,
        [string] $SystemPrompt = ''
    )

    $messages = [System.Collections.Generic.List[hashtable]]::new()
    if ($SystemPrompt) { $messages.Add(@{ role = 'system'; content = $SystemPrompt }) }
    $messages.Add(@{ role = 'user'; content = $Prompt })

    Write-Step "Calling $Provider ($LlmModel)..."

    if ($Provider -eq 'ollama') {
        $url  = "$($OllamaBaseUrl.TrimEnd('/'))/api/chat"
        $body = @{
            model    = $LlmModel
            messages = $messages.ToArray()
            stream   = $false
            think    = $false
        }
        try {
            $response = Invoke-RestMethod -Method Post -Uri $url `
                -ContentType 'application/json' `
                -Body ($body | ConvertTo-Json -Depth 10 -Compress)
            return $response.message.content
        }
        catch {
            $statusCode = $_.Exception.Response?.StatusCode?.value__ ?? 'unknown'
            throw "Ollama API call failed — HTTP $statusCode : $($_.Exception.Message)"
        }
    }
    else {
        # Groq and Mistral both expose an OpenAI-compatible chat completions endpoint
        if ([string]::IsNullOrWhiteSpace($ApiKey)) {
            throw "$Provider requires an API key. Pass -ApiKey or set LLM_API_KEY."
        }
        $url = switch ($Provider) {
            'groq'    { 'https://api.groq.com/openai/v1/chat/completions' }
            'mistral' { 'https://api.mistral.ai/v1/chat/completions' }
        }
        $body = @{
            model    = $LlmModel
            messages = $messages.ToArray()
        }
        $headers = @{
            Authorization  = "Bearer $ApiKey"
            'Content-Type' = 'application/json'
        }
        try {
            $response = Invoke-RestMethod -Method Post -Uri $url `
                -Headers $headers `
                -Body ($body | ConvertTo-Json -Depth 10 -Compress)
            return $response.choices[0].message.content
        }
        catch {
            $statusCode = $_.Exception.Response?.StatusCode?.value__ ?? 'unknown'
            throw "$Provider API call failed — HTTP $statusCode : $($_.Exception.Message)"
        }
    }
}

function ConvertFrom-OllamaJson {
    param([string] $RawText)

    # Strip <think>...</think> reasoning blocks emitted by chain-of-thought models.
    # Uses greedy match so it handles multiple blocks or unclosed tags.
    $cleaned = [regex]::Replace($RawText, '(?s)<think>.*?</think>', '')
    # Also strip any remaining unclosed <think> block (model truncated mid-thought)
    $cleaned = [regex]::Replace($cleaned, '(?s)<think>.*$', '')
    # Strip markdown code fences
    $cleaned = [regex]::Replace($cleaned, '(?s)^```(?:json)?\s*', '')
    $cleaned = [regex]::Replace($cleaned, '(?s)\s*```\s*$', '')
    $cleaned = $cleaned.Trim()

    # Extract the first JSON object or array in case of extra surrounding text
    $jsonMatch = [regex]::Match($cleaned, '(?s)(\{.*\}|\[.*\])')
    if ($jsonMatch.Success) {
        $cleaned = $jsonMatch.Value.Trim()
    }

    try {
        return $cleaned | ConvertFrom-Json -Depth 20
    }
    catch {
        throw "Could not parse JSON from Ollama response. Raw text:`n$RawText"
    }
}

# ---------------------------------------------------------------------------
# Banner
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "BookStack Seed Script" -ForegroundColor White
Write-Host "Target: $BookStackBaseUrl" -ForegroundColor White
if (-not [string]::IsNullOrWhiteSpace($Topic)) {
    Write-Host "Topic : $Topic" -ForegroundColor White
    Write-Host "Model : $LlmModel ($Provider)" -ForegroundColor White
}
if ($GoldenDataset) { Write-Host "Mode  : Golden-dataset pages will be seeded" -ForegroundColor White }
if ($DryRun) { Write-Warn "DRY RUN — no BookStack API calls will be made" }
Write-Host ""

# ---------------------------------------------------------------------------
# Steps 1–3 — LLM-generated book (only when -Topic is provided)
# ---------------------------------------------------------------------------

if (-not [string]::IsNullOrWhiteSpace($Topic)) {

    # -----------------------------------------------------------------------
    # Step 1 — Generate book outline
    # -----------------------------------------------------------------------

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

    Write-Host "[1/3] Generating book outline..." -ForegroundColor Magenta
    $outlineRaw = Invoke-Llm -Prompt $outlinePrompt -SystemPrompt $outlineSystem
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

    # -----------------------------------------------------------------------
    # Step 2 — Generate page content for every page
    # -----------------------------------------------------------------------

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
            $content = Invoke-Llm -Prompt $pagePrompt -SystemPrompt $pageSystem
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

    # -----------------------------------------------------------------------
    # Step 3 — Create content in BookStack
    # -----------------------------------------------------------------------

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
    }
    else {
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
    }

} # end -Topic block

# ---------------------------------------------------------------------------
# Step 4 — Seed golden-dataset evaluation pages (only when -GoldenDataset)
# ---------------------------------------------------------------------------

if ($GoldenDataset) {

    Write-Host "[Golden] Seeding v2 evaluation dataset pages (ASP.NET Core + .NET developer knowledge)..." -ForegroundColor Magenta

    # Helper: fetch a page from Microsoft Learn GitHub (CC BY 4.0), strip YAML frontmatter,
    # and return the first $MaxLines lines of Markdown.
    # Licence: https://github.com/dotnet/AspNetCore.Docs/blob/main/LICENSE
    function Get-MsLearnPage {
        param([string]$RawUrl, [int]$MaxLines = 300)
        Write-Step "Fetching $([System.IO.Path]::GetFileName($RawUrl))"
        try {
            $raw = (Invoke-WebRequest -Uri $RawUrl -UseBasicParsing -TimeoutSec 30).Content
        }
        catch { throw "Failed to fetch MS Learn content from $RawUrl : $_" }
        # Strip YAML frontmatter (--- ... ---)
        if ($raw.StartsWith('---')) {
            $end = $raw.IndexOf("`n---", 3)
            if ($end -gt 0) { $raw = $raw.Substring($end + 4).TrimStart("`r`n") }
        }
        $lines = $raw -split "`r?`n"
        if ($lines.Count -gt $MaxLines) { $raw = ($lines[0..($MaxLines - 1)]) -join "`n" }
        return $raw
    }

    $aspRaw = 'https://raw.githubusercontent.com/dotnet/AspNetCore.Docs/main'
    $dnRaw  = 'https://raw.githubusercontent.com/dotnet/docs/main'

    # v2 golden-dataset: ASP.NET Core + .NET developer knowledge pages.
    # Chapter and ChapterDescription drive chapter creation (deduplicated on first occurrence).
    # Editor 'wysiwyg' → html field; 'markdown' → markdown field.
    # Expected BookStack slugs are derived from Name via Str::slug() — see golden-dataset.json.
    $gdPages = @(
        @{
            Name               = 'Dependency Injection in ASP.NET Core'
            Chapter            = 'ASP.NET Core Fundamentals'
            ChapterDescription = 'Core ASP.NET Core concepts: dependency injection, middleware, configuration, and authentication.'
            Editor             = 'markdown'
            Content            = (Get-MsLearnPage "$aspRaw/aspnetcore/fundamentals/dependency-injection.md")
        },
        @{
            Name               = 'Service Lifetimes in ASP.NET Core'
            Chapter            = 'ASP.NET Core Fundamentals'
            ChapterDescription = 'Core ASP.NET Core concepts: dependency injection, middleware, configuration, and authentication.'
            Editor             = 'wysiwyg'
            Content            = @'
<p>ASP.NET Core dependency injection supports three service lifetimes that control how long a
registered service instance lives and how it is shared across requests and consumers.</p>
<h2>Singleton</h2>
<p>A single instance is created and shared for the entire application lifetime. Created on first
request and reused for all subsequent requests.</p>
<pre><code>services.AddSingleton&lt;IMyService, MyService&gt;();</code></pre>
<p><strong>Use when</strong>: the service is stateless, thread-safe, and expensive to create
(e.g., configuration objects, caches, HTTP clients).</p>
<h2>Scoped</h2>
<p>One instance per request (or DI scope). All consumers within the same request receive the same
instance.</p>
<pre><code>services.AddScoped&lt;IMyService, MyService&gt;();</code></pre>
<p><strong>Use when</strong>: the service holds per-request state (e.g., EF Core
<code>DbContext</code>, unit-of-work objects).</p>
<h2>Transient</h2>
<p>A new instance is created every time the service is requested from the container.</p>
<pre><code>services.AddTransient&lt;IMyService, MyService&gt;();</code></pre>
<p><strong>Use when</strong>: the service is lightweight and stateless and sharing an instance is
not desirable.</p>
<h2>Captive Dependency Pitfall</h2>
<p>Never inject a scoped or transient service into a singleton. The singleton holds a stale or
incorrectly-scoped reference for the full application lifetime. The built-in container validates
this at startup when <code>ValidateScopes = true</code>.</p>
<table>
  <thead><tr><th>Lifetime</th><th>Created</th><th>Shared across</th><th>Disposed</th></tr></thead>
  <tbody>
    <tr><td>Singleton</td><td>First request</td><td>All consumers, all requests</td><td>App shutdown</td></tr>
    <tr><td>Scoped</td><td>Each request</td><td>All consumers in same request</td><td>End of request</td></tr>
    <tr><td>Transient</td><td>Every resolution</td><td>Not shared</td><td>End of scope</td></tr>
  </tbody>
</table>
'@
        },
        @{
            Name               = 'Middleware Pipeline in ASP.NET Core'
            Chapter            = 'ASP.NET Core Fundamentals'
            ChapterDescription = 'Core ASP.NET Core concepts: dependency injection, middleware, configuration, and authentication.'
            Editor             = 'markdown'
            Content            = (Get-MsLearnPage "$aspRaw/aspnetcore/fundamentals/middleware/index.md")
        },
        @{
            Name               = 'JWT Bearer Authentication in ASP.NET Core'
            Chapter            = 'ASP.NET Core Fundamentals'
            ChapterDescription = 'Core ASP.NET Core concepts: dependency injection, middleware, configuration, and authentication.'
            Editor             = 'markdown'
            Content            = (Get-MsLearnPage "$aspRaw/aspnetcore/security/authentication/jwt-authn.md")
        },
        @{
            Name               = 'Policy-Based Authorization in ASP.NET Core'
            Chapter            = 'ASP.NET Core Fundamentals'
            ChapterDescription = 'Core ASP.NET Core concepts: dependency injection, middleware, configuration, and authentication.'
            Editor             = 'markdown'
            Content            = (Get-MsLearnPage "$aspRaw/aspnetcore/security/authorization/policies.md")
        },
        @{
            Name               = 'Options Pattern in ASP.NET Core'
            Chapter            = 'ASP.NET Core Fundamentals'
            ChapterDescription = 'Core ASP.NET Core concepts: dependency injection, middleware, configuration, and authentication.'
            Editor             = 'markdown'
            Content            = (Get-MsLearnPage "$aspRaw/aspnetcore/fundamentals/configuration/options.md")
        },
        @{
            Name               = 'App Secrets and Key Vault in ASP.NET Core'
            Chapter            = 'ASP.NET Core Fundamentals'
            ChapterDescription = 'Core ASP.NET Core concepts: dependency injection, middleware, configuration, and authentication.'
            Editor             = 'markdown'
            Content            = (Get-MsLearnPage "$aspRaw/aspnetcore/security/app-secrets.md")
        },
        @{
            Name               = 'Async Await Patterns in dotnet'
            Chapter            = 'dotnet Fundamentals'
            ChapterDescription = 'Core .NET concepts: asynchronous programming, cancellation, LINQ, and collections.'
            Editor             = 'markdown'
            Content            = (Get-MsLearnPage "$dnRaw/docs/csharp/asynchronous-programming/async-scenarios.md")
        },
        @{
            Name               = 'CancellationToken in dotnet'
            Chapter            = 'dotnet Fundamentals'
            ChapterDescription = 'Core .NET concepts: asynchronous programming, cancellation, LINQ, and collections.'
            Editor             = 'markdown'
            Content            = (Get-MsLearnPage "$dnRaw/docs/standard/threading/cancellation-in-managed-threads.md")
        },
        @{
            Name               = 'LINQ Fundamentals'
            Chapter            = 'dotnet Fundamentals'
            ChapterDescription = 'Core .NET concepts: asynchronous programming, cancellation, LINQ, and collections.'
            Editor             = 'markdown'
            Content            = (Get-MsLearnPage "$dnRaw/docs/csharp/linq/get-started/introduction-to-linq-queries.md")
        },
        @{
            Name               = 'IEnumerable vs IQueryable'
            Chapter            = 'dotnet Fundamentals'
            ChapterDescription = 'Core .NET concepts: asynchronous programming, cancellation, LINQ, and collections.'
            Editor             = 'wysiwyg'
            Content            = @'
<p><code>IEnumerable&lt;T&gt;</code> and <code>IQueryable&lt;T&gt;</code> are both used to represent
sequences of objects, but they differ fundamentally in where query logic executes.</p>
<h2>IEnumerable&lt;T&gt;</h2>
<p>Defined in <code>System.Collections.Generic</code>. Supports forward-only iteration over an
in-memory collection. LINQ operators on <code>IEnumerable</code> use delegates and execute in the
.NET process (<strong>LINQ to Objects</strong>).</p>
<pre><code>IEnumerable&lt;User&gt; users = context.Users.ToList(); // loads all rows first
var result = users.Where(u =&gt; u.IsActive);          // filters in memory</code></pre>
<h2>IQueryable&lt;T&gt;</h2>
<p>Defined in <code>System.Linq</code>. Extends <code>IEnumerable&lt;T&gt;</code> with an expression
tree and a query provider. LINQ operators build an expression tree that the provider (e.g., EF Core)
translates to SQL and executes on the database server (<strong>LINQ to Entities</strong>).</p>
<pre><code>IQueryable&lt;User&gt; users = context.Users;      // no query yet
var result = users.Where(u =&gt; u.IsActive);   // builds SQL WHERE clause
var list   = result.ToList();                 // executes the query</code></pre>
<h2>Comparison</h2>
<table>
  <thead><tr><th></th><th>IEnumerable&lt;T&gt;</th><th>IQueryable&lt;T&gt;</th></tr></thead>
  <tbody>
    <tr><td>Execution</td><td>In-memory (.NET process)</td><td>Data source (SQL server)</td></tr>
    <tr><td>Best for</td><td>In-memory collections, already loaded data</td><td>EF Core, remote data sources</td></tr>
    <tr><td>Filtering</td><td>Fetches all rows, filters in .NET</td><td>Pushes WHERE clause to the database</td></tr>
    <tr><td>Expression trees</td><td>No — uses delegates</td><td>Yes</td></tr>
  </tbody>
</table>
<p><strong>Rule of thumb</strong>: use <code>IQueryable</code> in the data access layer to push
filtering to the database; switch to <code>IEnumerable</code> once data is materialised in memory.</p>
'@
        },
        @{
            Name               = 'EF Core Migrations'
            Chapter            = 'Entity Framework Core'
            ChapterDescription = 'Entity Framework Core: migrations, querying, and connection resiliency.'
            Editor             = 'markdown'
            Content            = (Get-MsLearnPage "$aspRaw/aspnetcore/data/ef-mvc/migrations.md")
        },
        @{
            Name               = 'EF Core Querying and Tracking'
            Chapter            = 'Entity Framework Core'
            ChapterDescription = 'Entity Framework Core: migrations, querying, and connection resiliency.'
            Editor             = 'markdown'
            Content            = (Get-MsLearnPage "$aspRaw/aspnetcore/data/ef-mvc/read-related-data.md")
        },
        @{
            Name               = 'EF Core Connection Resiliency'
            Chapter            = 'Entity Framework Core'
            ChapterDescription = 'Entity Framework Core: migrations, querying, and connection resiliency.'
            Editor             = 'wysiwyg'
            Content            = @'
<p>Transient database errors — connection drops, temporary server overload — are common in cloud
and containerised deployments. EF Core provides built-in connection resiliency via
<strong>execution strategies</strong> that automatically retry failed operations.</p>
<h2>Enabling Retry on Failure</h2>
<p>For SQL Server and Azure SQL, enable retry with <code>EnableRetryOnFailure</code>:</p>
<pre><code>services.AddDbContext&lt;AppDbContext&gt;(options =&gt;
    options.UseSqlServer(connectionString, sqlOptions =&gt;
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));</code></pre>
<h2>Custom Execution Strategy</h2>
<p>For other providers or custom retry logic, use <code>CreateExecutionStrategy</code>:</p>
<pre><code>var strategy = dbContext.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =&gt;
{
    using var tx = await dbContext.Database.BeginTransactionAsync();
    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync();
    await tx.CommitAsync();
});</code></pre>
<h2>Which Errors Are Retried</h2>
<p>The default SQL Server strategy retries on transient errors including connection timeouts,
transport-level errors, deadlock victims, and Azure SQL transient fault codes.</p>
<h2>Limitations</h2>
<ul>
  <li>Retries are not applied automatically inside explicit transactions — wrap in a custom strategy.</li>
  <li>Set appropriate <code>CommandTimeout</code> values to avoid spurious timeouts triggering retries.</li>
</ul>
'@
        },
        @{
            # DrawIO page: contains a drawio-diagram div with an embedded placeholder PNG.
            # The HTML structure mirrors what BookStack stores when a diagram is inserted via
            # the WYSIWYG DrawIO integration.
            Name               = 'ASP.NET Core Request Pipeline'
            Chapter            = 'Architecture Diagrams'
            ChapterDescription = 'Architecture overview pages with DrawIO diagrams for visual reference.'
            Editor             = 'wysiwyg'
            Content            = @'
<p>The ASP.NET Core request pipeline is a chain of middleware components. Each middleware can
execute code before and after the next middleware in the chain. The diagram below shows the typical
execution order for a production web application.</p>
<div drawio-diagram="aspnetcore-pipeline-001"><img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8z8BQDwADhQGAWjR9awAAAABJRU5ErkJggg==" alt="ASP.NET Core request pipeline diagram" /></div>
<h2>Middleware Execution Order</h2>
<ol>
  <li><strong>Exception Handler / HSTS</strong> — catches unhandled exceptions; adds HTTPS security headers.</li>
  <li><strong>HTTPS Redirection</strong> — redirects HTTP to HTTPS.</li>
  <li><strong>Static Files</strong> — serves static assets without entering the rest of the pipeline.</li>
  <li><strong>Routing</strong> — matches the request URL to a registered endpoint.</li>
  <li><strong>CORS</strong> — handles Cross-Origin Resource Sharing preflight requests.</li>
  <li><strong>Authentication</strong> — identifies the caller from the token or cookie.</li>
  <li><strong>Authorization</strong> — enforces access policies for the identified caller.</li>
  <li><strong>Endpoint</strong> — executes the matched controller action or Razor Page handler.</li>
</ol>
<h2>Writing Custom Middleware</h2>
<pre><code>app.Use(async (context, next) =&gt;
{
    // Code before the downstream pipeline
    await next(context);
    // Code after the downstream pipeline
});</code></pre>
<p>Use <code>app.Run</code> to add terminal middleware that does not call <code>next</code>.
Use <code>app.UseWhen</code> to branch the pipeline conditionally without rejoining it.</p>
<h2>Order Matters</h2>
<p><code>UseAuthentication</code> must come before <code>UseAuthorization</code>. Static files
middleware should come before routing to short-circuit requests early and avoid unnecessary
route-matching work.</p>
'@
        }
    )

    if ($DryRun) {
        Write-Warn "DRY RUN — skipping golden-dataset BookStack API calls"
        Write-Host ""
        Write-Host "Would create golden-dataset book 'Developer Knowledge Evaluation Dataset' with:" -ForegroundColor White
        foreach ($gdPg in $gdPages) {
            $editorLabel = if ($gdPg.Editor -eq 'wysiwyg') { '[WYSIWYG]' } else { '[Markdown]' }
            Write-Host "  Page : $editorLabel $($gdPg.Name)"
        }
        Write-Host ""
        Write-Success "Dry run of golden-dataset complete"
    }
    else {
        Write-Step "Creating golden-dataset book"
        $gdBookResult = Invoke-BookStackApi -Method POST -Path 'books' -Body @{
            name        = 'Developer Knowledge Evaluation Dataset'
            description = 'v2 golden dataset for FEAT-0060 semantic search evaluation. ASP.NET Core + .NET content from Microsoft Learn (CC BY 4.0). Do not modify.'
        }
        $gdBookId = $gdBookResult.id
        Write-Success "Book created (id=$gdBookId)"

        $gdChapterIds = @{}
        foreach ($gdPg in $gdPages) {
            if (-not $gdChapterIds.ContainsKey($gdPg.Chapter)) {
                Write-Step "Creating chapter: $($gdPg.Chapter)"
                $gdChResult = Invoke-BookStackApi -Method POST -Path 'chapters' -Body @{
                    book_id     = $gdBookId
                    name        = $gdPg.Chapter
                    description = $gdPg.ChapterDescription
                }
                $gdChapterIds[$gdPg.Chapter] = $gdChResult.id
                Write-Success "Chapter created (id=$($gdChResult.id))"
            }

            $gdChId      = $gdChapterIds[$gdPg.Chapter]
            $editorLabel = if ($gdPg.Editor -eq 'wysiwyg') { '[WYSIWYG]' } else { '[Markdown]' }
            Write-Step "Creating page $editorLabel : $($gdPg.Name)"
            $pageBody = @{
                chapter_id = $gdChId
                name       = $gdPg.Name
            }
            if ($gdPg.Editor -eq 'wysiwyg') {
                $pageBody['html'] = $gdPg.Content
            }
            else {
                $pageBody['markdown'] = $gdPg.Content
            }
            $gdPgResult = Invoke-BookStackApi -Method POST -Path 'pages' -Body $pageBody
            Write-Success "Page created (slug=$($gdPgResult.slug))"
        }
        Write-Success "Golden-dataset pages seeded successfully"
        Write-Host ""
    }

} # end -GoldenDataset block

Write-Host ""
Write-Host "Done! Open $($BookStackBaseUrl.TrimEnd('/'))/books to review seeded content." -ForegroundColor Green
Write-Host ""

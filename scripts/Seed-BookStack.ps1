#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Populates a BookStack instance with AI-generated content and/or fixed golden-dataset evaluation pages.

.DESCRIPTION
    Calls an LLM to generate a book outline (chapters + pages) for a topic and creates the content
    in BookStack via its REST API. Optionally seeds a fixed "BookStack Evaluation Dataset" book
    containing WYSIWYG, Markdown, and DrawIO pages used as the golden dataset for FEAT-0060
    semantic search quality evaluation.

    When -Topic is supplied the script generates:
      - 1 LLM-authored book
      - 3–5 chapters, each with 2–4 pages of Markdown content

    When -GoldenDataset is supplied the script additionally creates:
      - 1 evaluation book ("BookStack Evaluation Dataset")
      - 4 chapters with 12 fixed-content pages (6 WYSIWYG, 6 Markdown, 1 DrawIO diagram)

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
    When set, creates a fixed "BookStack Evaluation Dataset" book with WYSIWYG, Markdown, and DrawIO
    pages used as the golden dataset for FEAT-0060. Can be combined with -Topic.

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

    Write-Host "[Golden] Seeding evaluation dataset pages..." -ForegroundColor Magenta

    # Flat list of golden-dataset pages. Chapter and ChapterDescription drive chapter creation
    # (chapters are deduplicated on first occurrence). Editor is 'wysiwyg' (uses html field) or
    # 'markdown' (uses markdown field). Page slugs are derived from Name by BookStack.
    $gdPages = @(
        @{
            Name               = 'Introduction to BookStack'
            Chapter            = 'Getting Started'
            ChapterDescription = 'Short summary pages for quick reference, seeded as WYSIWYG pages.'
            Editor             = 'wysiwyg'
            Content            = @'
<p>BookStack is a free, open-source, self-hosted knowledge management platform designed to make
documentation simple. It organises content into a hierarchy of shelves, books, chapters, and pages,
making it easy to structure and navigate large collections of information.</p>
<p>Originally built on top of the Laravel PHP framework, BookStack provides a clean WYSIWYG editor
for non-technical writers alongside a Markdown editor for developers. Content can be quickly
searched, tagged, and linked across the platform.</p>
<h2>Key Concepts</h2>
<ul>
  <li><strong>Shelves</strong> — top-level groupings of related books.</li>
  <li><strong>Books</strong> — containers for chapters and pages about a specific subject.</li>
  <li><strong>Chapters</strong> — optional sub-groupings within a book.</li>
  <li><strong>Pages</strong> — individual documentation documents with rich-text or Markdown content.</li>
</ul>
<p>BookStack is ideal for internal wikis, runbooks, developer portals, and project documentation
where self-hosting and data ownership are important.</p>
'@
        },
        @{
            Name               = 'Quick Start Guide'
            Chapter            = 'Getting Started'
            ChapterDescription = 'Short summary pages for quick reference, seeded as WYSIWYG pages.'
            Editor             = 'wysiwyg'
            Content            = @'
<p>This guide walks you through getting BookStack running in under five minutes using Docker Compose.</p>
<h2>Prerequisites</h2>
<ul>
  <li>Docker 24+ and Docker Compose V2</li>
  <li>A free TCP port (default: 6875)</li>
</ul>
<h2>Steps</h2>
<ol>
  <li>Clone the repository: <code>git clone https://github.com/BookStackApp/BookStack</code></li>
  <li>Copy the example environment file: <code>cp .env.example .env</code></li>
  <li>Start the stack: <code>docker compose up -d</code></li>
  <li>Open <code>http://localhost:6875</code> in your browser.</li>
  <li>Log in with the default credentials: <code>admin@admin.com</code> / <code>password</code>.</li>
</ol>
<p>Change the default password immediately after first login. The default credentials are publicly
known and must not be used in production.</p>
<h2>Next Steps</h2>
<p>After the initial login, configure your application URL, SMTP settings, and create your first
shelf to start organising content.</p>
'@
        },
        @{
            Name               = 'BookStack Feature Overview'
            Chapter            = 'Getting Started'
            ChapterDescription = 'Short summary pages for quick reference, seeded as WYSIWYG pages.'
            Editor             = 'wysiwyg'
            Content            = @'
<p>BookStack provides a comprehensive set of features for teams that need a self-hosted knowledge base.</p>
<h2>Content Authoring</h2>
<ul>
  <li>WYSIWYG editor powered by TinyMCE for rich formatting without technical knowledge.</li>
  <li>Markdown editor with live preview for developers who prefer plain text.</li>
  <li>Inline image uploads, file attachments, and embedded videos.</li>
  <li>DrawIO diagram integration for architecture and flow diagrams.</li>
</ul>
<h2>Organisation and Navigation</h2>
<ul>
  <li>Hierarchical structure: shelves, books, chapters, and pages.</li>
  <li>Page tagging with custom key-value pairs for flexible metadata.</li>
  <li>Full-text and semantic search across all content.</li>
  <li>Cross-page linking with automatic link-updating on page rename.</li>
</ul>
<h2>Access Control</h2>
<ul>
  <li>Role-based permissions (admin, editor, viewer) with per-entity overrides.</li>
  <li>LDAP, SAML 2.0, and OpenID Connect authentication.</li>
  <li>Multi-factor authentication support.</li>
</ul>
<h2>Integration and API</h2>
<ul>
  <li>RESTful JSON API covering all content types and administrative actions.</li>
  <li>Webhooks for create/update/delete events.</li>
  <li>Export to PDF, HTML, plain text, and Markdown.</li>
</ul>
'@
        },
        @{
            Name               = 'Installing BookStack on Ubuntu'
            Chapter            = 'Installation and Configuration'
            ChapterDescription = 'Step-by-step procedural guides for deploying and configuring BookStack, seeded as Markdown pages.'
            Editor             = 'markdown'
            Content            = @'
## Requirements

Before installing BookStack, ensure your Ubuntu server meets these minimum requirements:

- Ubuntu 22.04 LTS or later
- PHP 8.1 or later with extensions: `mbstring`, `gd`, `curl`, `xml`, `zip`, `mysql`
- MySQL 8.0+ or MariaDB 10.3+
- Nginx or Apache web server
- Composer (PHP dependency manager)
- Git

## Installation Steps

### 1. Update system packages

```bash
sudo apt update && sudo apt upgrade -y
```

### 2. Install PHP and required extensions

```bash
sudo apt install -y php8.2 php8.2-fpm php8.2-mysql php8.2-mbstring \
    php8.2-gd php8.2-curl php8.2-xml php8.2-zip php8.2-cli
```

### 3. Install MySQL

```bash
sudo apt install -y mysql-server
sudo mysql_secure_installation
```

### 4. Create a BookStack database

```sql
CREATE DATABASE bookstack CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'bookstack'@'localhost' IDENTIFIED BY 'strong-password-here';
GRANT ALL PRIVILEGES ON bookstack.* TO 'bookstack'@'localhost';
FLUSH PRIVILEGES;
```

### 5. Clone BookStack

```bash
cd /var/www
sudo git clone https://github.com/BookStackApp/BookStack.git \
    --branch release --single-branch bookstack
sudo chown -R www-data:www-data bookstack
```

### 6. Install PHP dependencies

```bash
cd /var/www/bookstack
sudo -u www-data composer install --no-dev
```

### 7. Configure environment

```bash
sudo cp .env.example .env
sudo nano .env
```

Set `APP_URL`, `DB_DATABASE`, `DB_USERNAME`, `DB_PASSWORD`, and `APP_KEY`.

### 8. Run migrations

```bash
sudo -u www-data php artisan migrate --force
```

### 9. Configure Nginx

Create `/etc/nginx/sites-available/bookstack` with a server block pointing `root` to
`/var/www/bookstack/public` and enabling `try_files` for Laravel routing. Then enable and reload Nginx.

The default admin credentials after installation are `admin@admin.com` / `password`. Change them immediately.
'@
        },
        @{
            Name               = 'Configuring LDAP Authentication'
            Chapter            = 'Installation and Configuration'
            ChapterDescription = 'Step-by-step procedural guides for deploying and configuring BookStack, seeded as Markdown pages.'
            Editor             = 'markdown'
            Content            = @'
## Overview

BookStack supports LDAP authentication, allowing users to log in with their existing Active
Directory or OpenLDAP credentials. Configuration is done entirely through environment variables
in the `.env` file.

## Environment Variables

Add the following to your `.env` file:

```env
AUTH_METHOD=ldap
LDAP_SERVER=ldap://your-domain-controller:389
LDAP_BASE_DN=DC=example,DC=com
LDAP_DN=CN=bookstack-bind,OU=ServiceAccounts,DC=example,DC=com
LDAP_PASS=your-bind-password
LDAP_USER_FILTER=(&(objectClass=user)(sAMAccountName={user}))
LDAP_VERSION=3
LDAP_TLS_INSECURE=false
LDAP_START_TLS=false
LDAP_ID_ATTRIBUTE=objectguid
LDAP_EMAIL_ATTRIBUTE=mail
LDAP_DISPLAY_NAME_ATTRIBUTE=cn
LDAP_FOLLOW_REFERRALS=false
```

## Group Synchronisation

To synchronise LDAP groups to BookStack roles, add:

```env
LDAP_USER_TO_GROUPS=true
LDAP_GROUP_ATTRIBUTE=memberOf
LDAP_REMOVE_FROM_GROUPS=true
```

BookStack will create roles matching LDAP group names and assign them automatically on login.

## Testing the Connection

Use `ldapsearch` to verify connectivity before configuring BookStack:

```bash
ldapsearch -H ldap://your-dc:389 \
  -D "CN=bookstack-bind,OU=ServiceAccounts,DC=example,DC=com" \
  -w your-bind-password \
  -b "DC=example,DC=com" \
  "(sAMAccountName=testuser)"
```

## Troubleshooting

- **Cannot connect to LDAP server**: check firewall rules on port 389 (or 636 for LDAPS).
- **Invalid credentials**: verify the bind DN and password, and that the service account is not locked.
- **Users not found**: test the `LDAP_USER_FILTER` pattern with `ldapsearch` and check the base DN.
- **Groups not syncing**: confirm the user object has a `memberOf` attribute and that
  `LDAP_GROUP_ATTRIBUTE` matches your directory schema.
'@
        },
        @{
            Name               = 'Setting Up Email Notifications'
            Chapter            = 'Installation and Configuration'
            ChapterDescription = 'Step-by-step procedural guides for deploying and configuring BookStack, seeded as Markdown pages.'
            Editor             = 'markdown'
            Content            = @'
## Overview

BookStack sends email notifications for page watches, user invitations, password resets, and comment
mentions. Email is configured via SMTP using environment variables.

## Required Environment Variables

```env
MAIL_DRIVER=smtp
MAIL_HOST=smtp.example.com
MAIL_PORT=587
MAIL_ENCRYPTION=tls
MAIL_USERNAME=bookstack@example.com
MAIL_PASSWORD=your-smtp-password
MAIL_FROM_NAME="BookStack"
MAIL_FROM_ADDRESS=bookstack@example.com
```

## Using Gmail as SMTP

1. Enable 2-Factor Authentication on the Gmail account.
2. Generate an App Password under **Security > 2-Step Verification > App passwords**.
3. Configure:

```env
MAIL_HOST=smtp.gmail.com
MAIL_PORT=587
MAIL_ENCRYPTION=tls
MAIL_USERNAME=your-account@gmail.com
MAIL_PASSWORD=your-16-character-app-password
MAIL_FROM_ADDRESS=your-account@gmail.com
```

## Testing Email Delivery

From the BookStack admin panel, navigate to **Settings > Maintenance > Send a test email** to verify
the configuration. You can also test from the command line:

```bash
php artisan bookstack:test-email --to=test@example.com
```

## Notification Types

| Event | Recipients |
|-------|-----------|
| Page created/updated | Users watching the page or its parent book/chapter |
| Comment mention | Mentioned user |
| Password reset | Requesting user |
| User invitation | Invited user |

## Disabling Email

Set `MAIL_DRIVER=log` to write emails to the Laravel log file instead of sending them.
'@
        },
        @{
            Name               = 'PHP Configuration for BookStack'
            Chapter            = 'Advanced Configuration'
            ChapterDescription = 'Code-heavy reference pages for PHP, Nginx, and MySQL configuration, seeded as Markdown pages.'
            Editor             = 'markdown'
            Content            = @'
## Recommended php.ini Settings

The following PHP settings are recommended for a production BookStack deployment.

```ini
; Memory and execution time
memory_limit = 512M
max_execution_time = 120
max_input_time = 60

; Upload limits
upload_max_filesize = 50M
post_max_size = 50M
max_file_uploads = 20

; Session security
session.cookie_secure = 1
session.cookie_httponly = 1
session.cookie_samesite = Lax
session.use_strict_mode = 1
session.gc_maxlifetime = 7200

; OPcache — strongly recommended for production
opcache.enable = 1
opcache.memory_consumption = 256
opcache.interned_strings_buffer = 16
opcache.max_accelerated_files = 10000
opcache.revalidate_freq = 0
opcache.validate_timestamps = 0

; Timezone
date.timezone = UTC
```

## PHP-FPM Pool Configuration

For Nginx + PHP-FPM deployments, tune the worker pool in
`/etc/php/8.2/fpm/pool.d/bookstack.conf`:

```ini
[bookstack]
user  = www-data
group = www-data
listen = /run/php/php8.2-fpm-bookstack.sock
listen.owner = www-data
listen.group = www-data
pm = dynamic
pm.max_children      = 20
pm.start_servers     = 5
pm.min_spare_servers = 3
pm.max_spare_servers = 8
pm.max_requests      = 500
php_admin_value[memory_limit]       = 512M
php_admin_value[upload_max_filesize] = 50M
php_admin_value[post_max_size]       = 50M
```

## Required PHP Extensions

Verify all required extensions are loaded:

```bash
php -m | grep -E 'mbstring|gd|curl|xml|zip|pdo_mysql|tokenizer|ctype|json|bcmath|openssl'
```

If any extension is missing, install it:

```bash
sudo apt install php8.2-{mbstring,gd,curl,xml,zip,mysql,bcmath}
sudo systemctl reload php8.2-fpm
```
'@
        },
        @{
            Name               = 'Nginx Reverse Proxy Configuration'
            Chapter            = 'Advanced Configuration'
            ChapterDescription = 'Code-heavy reference pages for PHP, Nginx, and MySQL configuration, seeded as Markdown pages.'
            Editor             = 'markdown'
            Content            = @'
## Basic Nginx Server Block

Create `/etc/nginx/sites-available/bookstack`:

```nginx
server {
    listen 80;
    server_name wiki.example.com;
    root /var/www/bookstack/public;
    index index.php;

    add_header X-Frame-Options "SAMEORIGIN";
    add_header X-Content-Type-Options "nosniff";
    add_header Referrer-Policy "strict-origin-when-cross-origin";

    client_max_body_size 50M;

    location / {
        try_files $uri $uri/ /index.php?$query_string;
    }

    location ~ \.php$ {
        include        fastcgi_params;
        fastcgi_pass   unix:/run/php/php8.2-fpm-bookstack.sock;
        fastcgi_index  index.php;
        fastcgi_param  SCRIPT_FILENAME $realpath_root$fastcgi_script_name;
    }

    location ~ /\.(?!well-known).* {
        deny all;
    }
}
```

## HTTPS with Let''s Encrypt

```bash
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d wiki.example.com
```

## Nginx as a Reverse Proxy

If SSL is terminated upstream, pass the scheme header:

```nginx
server {
    listen 80;
    server_name wiki.example.com;

    location / {
        proxy_pass         http://127.0.0.1:6875;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 120s;
        client_max_body_size 50M;
    }
}
```

Also set `APP_URL=https://wiki.example.com` and `PROXY_ALWAYS_TRUST=true` in BookStack''s `.env`.

## Caching Static Assets

```nginx
location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff2?)$ {
    expires 1y;
    add_header Cache-Control "public, immutable";
}
```
'@
        },
        @{
            Name               = 'MySQL Database Setup'
            Chapter            = 'Advanced Configuration'
            ChapterDescription = 'Code-heavy reference pages for PHP, Nginx, and MySQL configuration, seeded as Markdown pages.'
            Editor             = 'markdown'
            Content            = @'
## Creating the BookStack Database

```sql
CREATE DATABASE bookstack
    CHARACTER SET  utf8mb4
    COLLATE        utf8mb4_unicode_ci;

CREATE USER 'bookstack'@'localhost'
    IDENTIFIED BY 'strong-random-password';

GRANT ALL PRIVILEGES ON bookstack.* TO 'bookstack'@'localhost';
FLUSH PRIVILEGES;
```

Verify the grant:

```sql
SHOW GRANTS FOR 'bookstack'@'localhost';
```

## Recommended MySQL Configuration

Add the following to `/etc/mysql/conf.d/bookstack.cnf`:

```ini
[mysqld]
character-set-server  = utf8mb4
collation-server      = utf8mb4_unicode_ci
innodb_buffer_pool_size        = 512M
innodb_buffer_pool_instances   = 1
innodb_log_file_size           = 128M
innodb_flush_log_at_trx_commit = 2
innodb_flush_method            = O_DIRECT
max_connections     = 100
wait_timeout        = 28800
interactive_timeout = 28800
slow_query_log        = 1
slow_query_log_file   = /var/log/mysql/slow.log
long_query_time       = 2
```

Restart MySQL after changes: `sudo systemctl restart mysql`

## Backup and Restore

### Backup

```bash
mysqldump \
  --single-transaction \
  --routines \
  --triggers \
  -u bookstack -p bookstack > bookstack-$(date +%Y%m%d).sql
```

### Restore

```bash
mysql -u bookstack -p bookstack < bookstack-20260512.sql
```

## Checking Database Size

```sql
SELECT
    table_schema AS 'Database',
    ROUND(SUM(data_length + index_length) / 1024 / 1024, 2) AS 'Size (MB)'
FROM information_schema.tables
WHERE table_schema = 'bookstack'
GROUP BY table_schema;
```
'@
        },
        @{
            # DrawIO page: contains a drawio-diagram div with an embedded placeholder PNG.
            # The HTML structure used here mirrors what BookStack stores in its database when
            # a diagram is inserted via the WYSIWYG editor's DrawIO integration.
            # See docs/features/semantic-search-chunking/drawio-html-notes.md for a detailed
            # breakdown of the raw Html API response structure and the regex patterns needed to
            # strip diagram blocks during text extraction for embedding.
            Name               = 'System Architecture Overview'
            Chapter            = 'Architecture and API Reference'
            ChapterDescription = 'Multi-topic overview and architecture pages, seeded as WYSIWYG pages. Includes one DrawIO diagram page.'
            Editor             = 'wysiwyg'
            Content            = @'
<p>BookStack follows a standard LAMP-stack architecture with a PHP Laravel application at its core.
The diagram below illustrates the major components and data flows in a typical self-hosted
deployment.</p>
<div drawio-diagram="eval-arch-001"><img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8z8BQDwADhQGAWjR9awAAAABJRU5ErkJggg==" alt="BookStack system architecture diagram" /></div>
<h2>Core Components</h2>
<ul>
  <li><strong>Web Server (Nginx/Apache)</strong> — serves PHP via FastCGI; terminates TLS.</li>
  <li><strong>PHP-FPM (Laravel)</strong> — application server handling authentication, authorisation,
    content rendering, and API requests.</li>
  <li><strong>MySQL/MariaDB</strong> — relational data store for pages, books, shelves, users,
    roles, and audit logs.</li>
  <li><strong>File Storage</strong> — local filesystem or S3-compatible object storage for
    attachments and uploaded images.</li>
  <li><strong>Cache (optional)</strong> — Redis or Memcached for session and query cache.</li>
</ul>
<h2>Request Flow</h2>
<ol>
  <li>The browser or API client sends an HTTPS request to the web server.</li>
  <li>The web server forwards the request to PHP-FPM via FastCGI.</li>
  <li>Laravel resolves the route, checks permissions, and delegates to the appropriate controller.</li>
  <li>The controller reads or writes data via Eloquent ORM to MySQL.</li>
  <li>The response is rendered (Blade template or JSON) and returned through the web server.</li>
</ol>
<h2>MCP Server Integration</h2>
<p>The <strong>bookstack-mcp-server</strong> sits outside the BookStack process. It communicates
with BookStack exclusively through the public REST API using an API token, and exposes BookStack
content to AI assistants via the Model Context Protocol.</p>
'@
        },
        @{
            Name               = 'API Authentication Methods'
            Chapter            = 'Architecture and API Reference'
            ChapterDescription = 'Multi-topic overview and architecture pages, seeded as WYSIWYG pages. Includes one DrawIO diagram page.'
            Editor             = 'wysiwyg'
            Content            = @'
<p>The BookStack REST API supports two authentication methods. All API requests must be
authenticated; unauthenticated requests return HTTP 401.</p>
<h2>Token Authentication (Recommended)</h2>
<p>Token authentication uses a static API token issued per user. Tokens consist of a token ID and
a token secret, concatenated with a colon and passed in the <code>Authorization</code> header.</p>
<h3>Generating a Token</h3>
<ol>
  <li>Log in to BookStack as the user account that will own the token.</li>
  <li>Navigate to <strong>Profile &gt; API Tokens</strong>.</li>
  <li>Click <strong>Create Token</strong>, give it a name and optional expiry date.</li>
  <li>Copy both the <strong>Token ID</strong> and <strong>Token Secret</strong> — the secret is
    only shown once.</li>
</ol>
<h3>Using the Token</h3>
<pre><code>GET /api/pages HTTP/1.1
Host: wiki.example.com
Authorization: Token YOUR_TOKEN_ID:YOUR_TOKEN_SECRET
Accept: application/json</code></pre>
<h2>Session Cookie Authentication</h2>
<p>Authenticated browser sessions can make API requests using the session cookie. This is suitable
for in-browser JavaScript but not recommended for server-side integrations due to CSRF
requirements.</p>
<h2>Permissions and Scopes</h2>
<p>API tokens inherit all permissions of the owning user account. To limit API access, create a
dedicated BookStack user with the minimum required role permissions and generate the token under
that account.</p>
<h2>Rate Limiting</h2>
<p>BookStack does not enforce API rate limits by default. Implement rate limiting at the web server
or reverse proxy layer if required.</p>
'@
        },
        @{
            Name               = 'User Roles and Permissions'
            Chapter            = 'Architecture and API Reference'
            ChapterDescription = 'Multi-topic overview and architecture pages, seeded as WYSIWYG pages. Includes one DrawIO diagram page.'
            Editor             = 'wysiwyg'
            Content            = @'
<p>BookStack uses a role-based access control (RBAC) model. Users are assigned one or more roles,
and roles carry system-level permissions. Individual content items can have additional per-entity
permissions that override the role defaults.</p>
<h2>Built-in Roles</h2>
<table>
  <thead>
    <tr><th>Role</th><th>Description</th></tr>
  </thead>
  <tbody>
    <tr><td>Admin</td><td>Full access to all content and settings. Can manage users, roles, and
      system configuration.</td></tr>
    <tr><td>Editor</td><td>Can create, edit, and delete all content. Cannot manage users or system
      settings.</td></tr>
    <tr><td>Viewer</td><td>Read-only access to all public content. Cannot create or edit
      pages.</td></tr>
    <tr><td>Public</td><td>Permissions granted to unauthenticated visitors when public access is
      enabled.</td></tr>
  </tbody>
</table>
<h2>Custom Roles</h2>
<p>Administrators can create custom roles with granular permissions covering content access, image
and attachment management, user management, and system configuration access.</p>
<h2>Per-Entity Permissions</h2>
<p>On any shelf, book, chapter, or page, admins can configure permission overrides that restrict
or expand access for specific roles. For example, a book can be made visible only to the
Engineering role even though the Editor role would normally have access to all content.</p>
<h2>Inheritance</h2>
<p>Permissions follow the content hierarchy. A book''s permissions apply to all chapters and pages
within it unless overridden at a lower level.</p>
'@
        }
    )

    if ($DryRun) {
        Write-Warn "DRY RUN — skipping golden-dataset BookStack API calls"
        Write-Host ""
        Write-Host "Would create golden-dataset book 'BookStack Evaluation Dataset' with:" -ForegroundColor White
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
            name        = 'BookStack Evaluation Dataset'
            description = 'Fixed-content pages for FEAT-0060 semantic search quality evaluation. Do not modify.'
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

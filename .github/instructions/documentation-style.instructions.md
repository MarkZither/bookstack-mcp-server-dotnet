# Documentation Style Instructions

## applyTo

`docs/**/*.md`, `.github/**/*.md`, `README.md`

## Formatting Rules

1. **Headings**: use ATX-style (`#`, `##`, `###`); no more than three levels deep in a single document.
2. **Line length**: wrap prose at 120 characters; code blocks are exempt.
3. **Lists**: use `-` for unordered lists; use `1.` for ordered lists. Do not mix styles within a list.
4. **Code blocks**: always specify a language identifier (` ```csharp`, ` ```bash`, etc.).
5. **Tables**: use standard Markdown tables; align columns with spaces for readability.
6. **Links**: use inline links `[text](url)`; avoid bare URLs in prose.
7. **Bold / Italic**: use `**bold**` for key terms; use `*italic*` sparingly for emphasis.
8. **Admonitions**: use `> **Note**`, `> **Warning**`, `> **Tip**` block-quote conventions.
9. **No trailing whitespace** on any line.
10. **End each file with a single newline.**

## Content Rules

1. **Voice**: use active voice; second person ("you") for guides, third person for reference docs.
2. **Tense**: present tense for facts; future tense only for planned work.
3. **Acronyms**: spell out on first use followed by the acronym in parentheses, e.g., "Model Context Protocol (MCP)".
4. **Dates**: ISO 8601 format (`YYYY-MM-DD`).
5. **File paths**: wrap in backticks; use forward slashes.
6. **Sensitive data**: never include real credentials, tokens, or personal data in documentation.
7. **TODO markers**: use `[TODO: description]` for placeholder content; resolve before marking a spec `Approved`.

## Markdownlint

All documents must pass `markdownlint` with the rules defined in `.markdownlint.yaml`.

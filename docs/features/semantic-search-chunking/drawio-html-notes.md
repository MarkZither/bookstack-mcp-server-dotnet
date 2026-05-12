# DrawIO HTML Structure in BookStack API Responses

**Feature**: FEAT-0060 — Semantic Search Quality / Golden-Dataset Evaluation
**Relates to**: `scripts/Seed-BookStack.ps1` (System Architecture Overview page), Phase 2 chunking

---

## Purpose

This document records the HTML structure that BookStack stores (and returns via the REST API)
when a WYSIWYG page contains one or more DrawIO diagrams. The pattern is needed in Phase 2 to
strip diagram markup before passing page content to the embedding model.

---

## Structure in the Raw `Html` API Response

When a BookStack page is retrieved via `GET /api/pages/{id}`, the `html` field contains the
full rendered page HTML. A DrawIO diagram appears as a `<div>` with the custom `drawio-diagram`
attribute wrapping an `<img>` tag whose `src` is a `data:image/png;base64,…` URI:

```html
<div drawio-diagram="DIAGRAM-ID">
    <img src="data:image/png;base64,BASE64_PNG_DATA" alt="optional alt text" />
</div>
```

### Field details

| Attribute / Element | Description |
|---------------------|-------------|
| `drawio-diagram` attribute | String identifier unique within the page. BookStack assigns this when the diagram is saved. Typically a short UUID or integer string (e.g. `"1"`, `"eval-arch-001"`). |
| `<img src="data:image/png;base64,…">` | Inline PNG preview of the diagram. Can be very large (tens of kilobytes of base64) for complex diagrams. |
| `alt` attribute | Optional. BookStack does not automatically populate it; authors may add it manually. |

### Notes

- The raw DrawIO XML (`.drawio` / `mxGraphModel`) is **not** stored in the `html` field. It is
  stored separately in BookStack's `drawio_diagrams` table and is not exposed in the pages API
  response.
- BookStack versions ≥ 23.x no longer embed the `mxGraphModel` XML inline; older versions may
  include it in a hidden `<div>` inside the `drawio-diagram` container. The stripping regex
  should handle both forms.
- A single page may contain multiple `drawio-diagram` blocks.

---

## Stripping Pattern for Phase 2

To remove DrawIO blocks during HTML-to-text conversion before embedding, apply the following
regex **before** the general HTML tag stripper:

```csharp
// Remove DrawIO diagram blocks (including embedded base64 PNG).
// The (?s) flag makes . match newlines.
private static readonly Regex DrawioDiagramPattern =
    new(@"<div[^>]+drawio-diagram[^>]*>.*?</div>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
```

This pattern removes the entire `<div drawio-diagram="…">…</div>` block, including the
base64 PNG data that would otherwise bloat the token count without contributing semantic value.

**Important**: Any surrounding caption text (e.g. a `<p>` paragraph immediately before or after
the diagram) is **preserved** — only the diagram container itself is stripped.

---

## Seed Script Usage

The `System Architecture Overview` page created by `scripts/Seed-BookStack.ps1 -GoldenDataset`
contains one DrawIO block using a 1×1 placeholder PNG:

```html
<div drawio-diagram="eval-arch-001">
    <img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8z8BQDwADhQGAWjR9awAAAABJRU5ErkJggg==" alt="BookStack system architecture diagram" />
</div>
```

After seeding, retrieve the page via the API and compare the returned `html` field against this
structure to confirm the stripping regex works correctly on a live instance before Phase 2 ships.

```bash
# Retrieve the seeded page (replace PAGE_ID with the id printed during seeding):
curl -s -H "Authorization: Token TOKEN_ID:TOKEN_SECRET" \
     http://localhost:6875/api/pages/PAGE_ID \
  | jq '.html' | grep -o '<div[^>]*drawio-diagram[^>]*>.*</div>'
```

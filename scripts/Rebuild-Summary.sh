#!/usr/bin/env bash
# Rebuild-Summary.sh
# Regenerates docs/features/semantic-search-chunking/runs/summary.md
# from whatever eval-*.md files already exist in that directory.
#
# Usage:
#   cd /home/mark/github/bookstack-mcp-server-dotnet
#   bash scripts/Rebuild-Summary.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNS_DIR="$REPO_ROOT/docs/features/semantic-search-chunking/runs"
SUMMARY="$RUNS_DIR/summary.md"

extract_metric() {
    local file="$1" metric="$2"
    grep "| $metric " "$file" | awk -F'|' '{gsub(/ /,"", $3); print $3}' | head -1
}

extract_latency() {
    local file="$1" pct="$2"
    grep "| $pct " "$file" | awk -F'|' '{gsub(/ /,"", $3); print $3}' | head -1
}

extract_storage() {
    local file="$1" metric="$2"
    # Matches rows in the ## Storage table: | Chunks indexed | 123 |
    grep "| ${metric} |" "$file" | awk -F'|' '{gsub(/ /,"", $3); print $3}' | head -1
}

# Reverse-map safe_model → display model name
display_model() {
    case "$1" in
        qllama_bge-large-en-v1.5) echo "qllama/bge-large-en-v1.5" ;;
        mxbai-embed-large)         echo "mxbai-embed-large" ;;
        nomic-embed-text)          echo "nomic-embed-text" ;;
        *)                         echo "$1" ;;  # unknown: pass through as-is
    esac
}

# Sort order: nomic first, then bge, then mxbai
model_order() {
    case "$1" in
        nomic-embed-text)          echo "1" ;;
        qllama_bge-large-en-v1.5)  echo "2" ;;
        mxbai-embed-large)         echo "3" ;;
        *)                         echo "9" ;;
    esac
}

printf '# Chunk Tuning Evaluation Summary\n\n' > "$SUMMARY"
printf '| Model | ChunkSize | ChunkOverlap | Recall@1 | Recall@3 | MRR | p50 | p95 | Chunks | DbMB |\n' >> "$SUMMARY"
printf '|-------|-----------|-------------|---------|---------|-----|-----|-----|--------|------|\n' >> "$SUMMARY"

# Collect rows into a temp file for sorting
tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT

for f in "$RUNS_DIR"/eval-*.md; do
    [[ -f "$f" ]] || continue
    base=$(basename "$f" .md)               # eval-{safe_model}__cs{N}_co{N}
    rest="${base#eval-}"                    # {safe_model}__cs{N}_co{N}
    safe_model="${rest%%__*}"               # {safe_model}
    coords="${rest##*__}"                   # cs{N}_co{N}
    cs="${coords#cs}"; cs="${cs%%_*}"       # N
    co="${coords#*_co}"                     # N

    model=$(display_model "$safe_model")
    order=$(model_order "$safe_model")

    r1=$(extract_metric "$f" "Recall@1")
    r3=$(extract_metric "$f" "Recall@3")
    mrr=$(extract_metric "$f" "MRR")
    p50=$(extract_latency "$f" "p50")
    p95=$(extract_latency "$f" "p95")
    chunks=$(extract_storage "$f" "Chunks indexed")
    db_mb=$(extract_storage "$f" "DB size")

    # Sort key: model-order, chunk_size numeric, chunk_overlap numeric
    printf '%s\t%05d\t%05d\t| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |\n' \
        "$order" "$cs" "$co" \
        "$model" "$cs" "$co" \
        "${r1:--}" "${r3:--}" "${mrr:--}" "${p50:--}" "${p95:--}" \
        "${chunks:--}" "${db_mb:--}" >> "$tmp"
done

# Sort and strip the sort key columns before appending
sort -t$'\t' -k1,1 -k2,2n -k3,3n "$tmp" | cut -f4- >> "$SUMMARY"

echo "Rebuilt: $SUMMARY  ($(grep -c '^|' "$SUMMARY" || true) data rows)"
cat "$SUMMARY"

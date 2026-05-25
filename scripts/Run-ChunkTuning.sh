#!/usr/bin/env bash
# Run-ChunkTuning.sh
# Batch chunk-size/overlap evaluation across mxbai-embed-large and nomic-embed-text.
# Results are saved to docs/features/semantic-search-chunking/runs/ and a summary
# table is printed at the end.
#
# Prerequisites:
#   - BookStack running at http://localhost:6875 with the v2 seed data
#   - Ollama running with all three models pulled:
#       ollama pull nomic-embed-text
#       ollama pull mxbai-embed-large
#       ollama pull qllama/bge-large-en-v1.5
#   - dotnet 10 SDK on PATH
#
# Usage:
#   cd /home/mark/github/bookstack-mcp-server-dotnet
#   bash scripts/Run-ChunkTuning.sh
#
# Each run takes ~10 min (mxbai/bge indexing) or ~5 min (nomic indexing) + ~1 min eval.
# 3 models × 7 configs = 21 runs. Total estimated time: ~3-4 hours.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESULTS_DIR="$REPO_ROOT/docs/features/semantic-search-chunking/runs"
REPORT_PATH="$REPO_ROOT/docs/features/semantic-search-chunking/evaluation-report.md"
BOOKSTACK_TOKEN="6sNZBFZeTgnItw9A1PkpsVLMUeRsivtk:HZPiv4bj4lFTyt5qN83WvGlSK09ni2IQ"
MCP_PORT=3000
ADMIN_PORT=5175

# Chunk configs: "ChunkSize:ChunkOverlap"
CHUNK_CONFIGS=(
    "0:0"
    "256:32"
    "256:64"
    "512:64"
    "512:128"
    "512:256"
    "1024:256"
)

mkdir -p "$RESULTS_DIR"
SUMMARY="$RESULTS_DIR/summary.md"

# Write summary header
cat > "$SUMMARY" << 'EOF'
# Chunk Tuning Evaluation Summary

| Model | ChunkSize | ChunkOverlap | Recall@1 | Recall@3 | MRR | p50 | p95 |
|-------|-----------|-------------|---------|---------|-----|-----|-----|
EOF

# ──────────────────────────────────────────────────────────────────────────────
# Helper functions
# ──────────────────────────────────────────────────────────────────────────────

stop_server() {
    # .dll suffix — matches both 'dotnet ...BookStack.Mcp.Server.dll' and direct exe
    pkill -f "BookStack.Mcp.Server" 2>/dev/null || true
    sleep 3
}

# Returns 0 if sync completed, 1 if process died before completing.
wait_for_sync() {
    local log_file="$1"
    local server_pid="$2"
    local timeout_secs=900  # 15 min max
    local elapsed=0
    echo "  Waiting for vector index sync (server PID $server_pid)..."
    while true; do
        # Success
        if grep -q "Vector index sync complete" "$log_file" 2>/dev/null; then
            echo "  Sync complete."
            return 0
        fi
        # Crash — process no longer alive
        if ! kill -0 "$server_pid" 2>/dev/null; then
            echo "  ERROR: server process $server_pid died before sync completed."
            echo "  Last 20 lines of $log_file:"
            tail -20 "$log_file" 2>/dev/null || true
            return 1
        fi
        sleep 10
        elapsed=$((elapsed + 10))
        if [[ $elapsed -ge $timeout_secs ]]; then
            echo "  ERROR: sync timed out after ${timeout_secs}s — killing server"
            kill "$server_pid" 2>/dev/null || true
            return 1
        fi
        echo "  ...still syncing (${elapsed}s elapsed)"
    done
}

extract_metric() {
    local file="$1"
    local metric="$2"
    grep "| $metric " "$file" | awk -F'|' '{gsub(/ /, "", $3); print $3}' | head -1
}

extract_latency() {
    local file="$1"
    local percentile="$2"
    grep "| $percentile " "$file" | awk -F'|' '{gsub(/ /, "", $3); print $3}' | head -1
}

set_dimension() {
    local dim="$1"
    sed -i "s/public const int EmbeddingDimensions = [0-9]*/public const int EmbeddingDimensions = ${dim}/" \
        "$REPO_ROOT/src/BookStack.Mcp.Server/config/VectorSearchOptions.cs"
    sed -i "s/\[VectorStoreVector(Dimensions: [0-9]*/[VectorStoreVector(Dimensions: ${dim}/" \
        "$REPO_ROOT/src/BookStack.Mcp.Server.Data.Sqlite/VectorPageRecord.cs"
    sed -i "s/HasColumnType(\"vector([0-9]*)\"/HasColumnType(\"vector(${dim})\"/" \
        "$REPO_ROOT/src/BookStack.Mcp.Server.Data.Postgres/VectorPageRecord.cs"
}

build_server() {
    echo "  Building..."
    dotnet build "$REPO_ROOT/src/BookStack.Mcp.Server/BookStack.Mcp.Server.csproj" \
        --configuration Release -v q 2>&1 | tail -3
}

run_one() {
    local model="$1"
    local dimension="$2"
    local query_prefix="$3"
    local chunk_size="$4"
    local chunk_overlap="$5"
    local db_path="/tmp/bookstack-vectors-${model//:/}-cs${chunk_size}-co${chunk_overlap}.db"
    local log_file="/tmp/mcp-server-${model//:/}-cs${chunk_size}-co${chunk_overlap}.log"
    local label="${model}__cs${chunk_size}_co${chunk_overlap}"
    local out_report="$RESULTS_DIR/eval-${label}.md"

    echo ""
    echo "═══════════════════════════════════════════════════════"
    echo "  Model: $model | ChunkSize: $chunk_size | Overlap: $chunk_overlap"
    echo "═══════════════════════════════════════════════════════"

    stop_server
    rm -f "$db_path"

    BOOKSTACK_BASE_URL="http://localhost:6875" \
    BOOKSTACK_TOKEN_SECRET="$BOOKSTACK_TOKEN" \
    BOOKSTACK_VECTOR_ENABLED="true" \
    BOOKSTACK_VECTOR_DATABASE="sqlite" \
    BOOKSTACK_VECTOR_OLLAMA_URL="http://localhost:11434" \
    BOOKSTACK_VECTOR_OLLAMA_MODEL="$model" \
    BOOKSTACK_VECTOR_CONNECTION="Data Source=${db_path}" \
    VectorSearch__Ollama__QueryPrefix="$query_prefix" \
    VectorSearch__Chunking__ChunkSize="$chunk_size" \
    VectorSearch__Chunking__ChunkOverlap="$chunk_overlap" \
    BOOKSTACK_MCP_TRANSPORT="http" \
    BOOKSTACK_MCP_HTTP_PORT="$MCP_PORT" \
    BOOKSTACK_ADMIN_PORT="$ADMIN_PORT" \
    nohup dotnet "$REPO_ROOT/src/BookStack.Mcp.Server/bin/Release/net10.0/BookStack.Mcp.Server.dll" \
        > "$log_file" 2>&1 &
    local server_pid=$!

    if ! wait_for_sync "$log_file" "$server_pid"; then
        echo "  SKIPPED — server failed to start. Appending FAILED to summary."
        echo "| $model | $chunk_size | $chunk_overlap | FAILED | FAILED | FAILED | — | — |" >> "$SUMMARY"
        return 0  # continue to next run
    fi

    echo "  Running evaluation..."
    MCP_BASE_URL="http://localhost:${MCP_PORT}" \
    dotnet test "$REPO_ROOT/tests/BookStack.Mcp.Server.Evaluation/" \
        --configuration Release 2>&1 | grep -E "passed|failed|succeeded" || true

    cp "$REPORT_PATH" "$out_report"
    echo "  Report saved: $out_report"

    local r1 r3 mrr p50 p95
    r1=$(extract_metric "$out_report" "Recall@1")
    r3=$(extract_metric "$out_report" "Recall@3")
    mrr=$(extract_metric "$out_report" "MRR")
    p50=$(extract_latency "$out_report" "p50")
    p95=$(extract_latency "$out_report" "p95")

    echo "| $model | $chunk_size | $chunk_overlap | $r1 | $r3 | $mrr | $p50 | $p95 |" >> "$SUMMARY"
    echo "  Recall@1=$r1  Recall@3=$r3  MRR=$mrr  p50=$p50  p95=$p95"
}

# ──────────────────────────────────────────────────────────────────────────────
# PHASE 1 — nomic-embed-text (768-dim)
# ──────────────────────────────────────────────────────────────────────────────

echo ""
echo "▶▶ PHASE 1: nomic-embed-text (setting dimension to 768 and rebuilding)"
set_dimension 768
build_server

for cfg in "${CHUNK_CONFIGS[@]}"; do
    IFS=: read -r cs co <<< "$cfg"
    run_one "nomic-embed-text" 768 "" "$cs" "$co"
done

# ──────────────────────────────────────────────────────────────────────────────
# PHASE 2 — bge-large-en-v1.5 (1024-dim — same as mxbai, no rebuild needed)
# Pull first: ollama pull qllama/bge-large-en-v1.5
# ──────────────────────────────────────────────────────────────────────────────

echo ""
echo "▶▶ PHASE 2: bge-large-en-v1.5 (1024-dim — reusing nomic binary? No, rebuild to 1024)"
set_dimension 1024
build_server

for cfg in "${CHUNK_CONFIGS[@]}"; do
    IFS=: read -r cs co <<< "$cfg"
    run_one "qllama/bge-large-en-v1.5" 1024 "" "$cs" "$co"
done

# ──────────────────────────────────────────────────────────────────────────────
# PHASE 3 — mxbai-embed-large (1024-dim — same binary, no rebuild)
# ──────────────────────────────────────────────────────────────────────────────

echo ""
echo "▶▶ PHASE 3: mxbai-embed-large (1024-dim)"

MXBAI_PREFIX="Represent this sentence for searching relevant passages: "
for cfg in "${CHUNK_CONFIGS[@]}"; do
    IFS=: read -r cs co <<< "$cfg"
    run_one "mxbai-embed-large" 1024 "$MXBAI_PREFIX" "$cs" "$co"
done

# ──────────────────────────────────────────────────────────────────────────────
# Done — restore mxbai as default and print summary
# ──────────────────────────────────────────────────────────────────────────────

stop_server
set_dimension 1024
echo ""
echo "▶▶ All runs complete. Summary:"
echo ""
cat "$SUMMARY"
echo ""
echo "Full per-run reports: $RESULTS_DIR/"
echo ""
echo "Next steps:"
echo "  1. Review $SUMMARY"
echo "  2. Copy the best-performing chunk config row into evaluation-report.md"
echo "  3. Update VectorSearch__Chunking__ChunkSize / ChunkOverlap defaults"
echo "  4. Commit and rebuild"

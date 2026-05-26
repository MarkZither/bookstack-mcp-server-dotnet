﻿# Semantic Search Quality Evaluation Report

**Generated**: 2026-05-26 08:15:18 UTC

## Overall Verdict
**PHASE 2 REQUIRED**

## Metrics

| Metric | Value | Pass Threshold | Investigate | Verdict |
|--------|-------|----------------|-------------|---------|
| Recall@1 | 0.3333 | ≥ 0.60 | ≥ 0.45 | **FAIL** |
| Recall@3 | 0.6333 | ≥ 0.75 | ≥ 0.60 | **INVESTIGATE** |
| MRR | 0.5022 | ≥ 0.65 | ≥ 0.50 | **INVESTIGATE** |

## Score Distribution Histogram

| Score Range | Correct | Incorrect |
|-------------|---------|-----------|
| 0.0-0.1 | 0 | 0 |
| 0.1-0.2 | 1 | 0 |
| 0.2-0.3 | 8 | 5 |
| 0.3-0.4 | 12 | 32 |
| 0.4-0.5 | 4 | 26 |
| 0.5-0.6 | 0 | 0 |
| 0.6-0.7 | 0 | 0 |
| 0.7-0.8 | 0 | 0 |
| 0.8-0.9 | 0 | 0 |
| 0.9-1.0 | 0 | 0 |

## Summary

- **Queries evaluated**: 30
- **Verdict**: Phase 2 required

## Query Latency (end-to-end, including embedding)

| Percentile | Latency |
|------------|---------|
| p50 | 113 ms |
| p95 | 138 ms |

## Storage

| Metric | Value |
|--------|-------|
| Chunks indexed | 56 |
| DB size | 3.3 MB |

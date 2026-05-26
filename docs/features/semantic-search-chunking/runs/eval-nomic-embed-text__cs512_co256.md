﻿# Semantic Search Quality Evaluation Report

**Generated**: 2026-05-26 08:05:02 UTC

## Overall Verdict
**PHASE 2 REQUIRED**

## Metrics

| Metric | Value | Pass Threshold | Investigate | Verdict |
|--------|-------|----------------|-------------|---------|
| Recall@1 | 0.4000 | ≥ 0.60 | ≥ 0.45 | **FAIL** |
| Recall@3 | 0.8333 | ≥ 0.75 | ≥ 0.60 | **PASS** |
| MRR | 0.6039 | ≥ 0.65 | ≥ 0.50 | **INVESTIGATE** |

## Score Distribution Histogram

| Score Range | Correct | Incorrect |
|-------------|---------|-----------|
| 0.0-0.1 | 0 | 0 |
| 0.1-0.2 | 1 | 0 |
| 0.2-0.3 | 7 | 4 |
| 0.3-0.4 | 17 | 27 |
| 0.4-0.5 | 2 | 15 |
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
| p50 | 99 ms |
| p95 | 154 ms |

## Storage

| Metric | Value |
|--------|-------|
| Chunks indexed | 56 |
| DB size | 3.3 MB |

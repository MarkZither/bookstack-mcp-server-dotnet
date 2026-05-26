﻿# Semantic Search Quality Evaluation Report

**Generated**: 2026-05-26 10:23:38 UTC

## Overall Verdict
**PHASE 2 REQUIRED**

## Metrics

| Metric | Value | Pass Threshold | Investigate | Verdict |
|--------|-------|----------------|-------------|---------|
| Recall@1 | 0.4000 | ≥ 0.60 | ≥ 0.45 | **FAIL** |
| Recall@3 | 0.8333 | ≥ 0.75 | ≥ 0.60 | **PASS** |
| MRR | 0.6094 | ≥ 0.65 | ≥ 0.50 | **INVESTIGATE** |

## Score Distribution Histogram

| Score Range | Correct | Incorrect |
|-------------|---------|-----------|
| 0.0-0.1 | 1 | 0 |
| 0.1-0.2 | 4 | 1 |
| 0.2-0.3 | 13 | 11 |
| 0.3-0.4 | 9 | 25 |
| 0.4-0.5 | 0 | 8 |
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
| p50 | 272 ms |
| p95 | 346 ms |

## Storage

| Metric | Value |
|--------|-------|
| Chunks indexed | 44 |
| DB size | 4.1 MB |

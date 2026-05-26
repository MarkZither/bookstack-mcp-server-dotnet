﻿# Semantic Search Quality Evaluation Report

**Generated**: 2026-05-26 07:36:09 UTC

## Overall Verdict
**PHASE 2 REQUIRED**

## Metrics

| Metric | Value | Pass Threshold | Investigate | Verdict |
|--------|-------|----------------|-------------|---------|
| Recall@1 | 0.4333 | ≥ 0.60 | ≥ 0.45 | **FAIL** |
| Recall@3 | 0.8000 | ≥ 0.75 | ≥ 0.60 | **PASS** |
| MRR | 0.6067 | ≥ 0.65 | ≥ 0.50 | **INVESTIGATE** |

## Score Distribution Histogram

| Score Range | Correct | Incorrect |
|-------------|---------|-----------|
| 0.0-0.1 | 0 | 0 |
| 0.1-0.2 | 1 | 0 |
| 0.2-0.3 | 8 | 4 |
| 0.3-0.4 | 16 | 32 |
| 0.4-0.5 | 2 | 13 |
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
| p50 | 93 ms |
| p95 | 115 ms |

## Storage

| Metric | Value |
|--------|-------|
| Chunks indexed | 56 |
| DB size | 3.3 MB |

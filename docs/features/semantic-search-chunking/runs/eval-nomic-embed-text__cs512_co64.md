﻿# Semantic Search Quality Evaluation Report

**Generated**: 2026-05-26 07:48:10 UTC

## Overall Verdict
**PHASE 2 REQUIRED**

## Metrics

| Metric | Value | Pass Threshold | Investigate | Verdict |
|--------|-------|----------------|-------------|---------|
| Recall@1 | 0.3667 | ≥ 0.60 | ≥ 0.45 | **FAIL** |
| Recall@3 | 0.6667 | ≥ 0.75 | ≥ 0.60 | **INVESTIGATE** |
| MRR | 0.5550 | ≥ 0.65 | ≥ 0.50 | **INVESTIGATE** |

## Score Distribution Histogram

| Score Range | Correct | Incorrect |
|-------------|---------|-----------|
| 0.0-0.1 | 0 | 0 |
| 0.1-0.2 | 1 | 0 |
| 0.2-0.3 | 6 | 4 |
| 0.3-0.4 | 18 | 28 |
| 0.4-0.5 | 2 | 21 |
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
| p50 | 97 ms |
| p95 | 117 ms |

## Storage

| Metric | Value |
|--------|-------|
| Chunks indexed | 56 |
| DB size | 3.3 MB |

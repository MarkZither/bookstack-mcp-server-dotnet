﻿# Semantic Search Quality Evaluation Report

**Generated**: 2026-05-26 09:30:26 UTC

## Overall Verdict
**INVESTIGATE**

## Metrics

| Metric | Value | Pass Threshold | Investigate | Verdict |
|--------|-------|----------------|-------------|---------|
| Recall@1 | 0.4667 | ≥ 0.60 | ≥ 0.45 | **INVESTIGATE** |
| Recall@3 | 0.7667 | ≥ 0.75 | ≥ 0.60 | **PASS** |
| MRR | 0.6500 | ≥ 0.65 | ≥ 0.50 | **PASS** |

## Score Distribution Histogram

| Score Range | Correct | Incorrect |
|-------------|---------|-----------|
| 0.0-0.1 | 0 | 0 |
| 0.1-0.2 | 3 | 0 |
| 0.2-0.3 | 18 | 9 |
| 0.3-0.4 | 6 | 22 |
| 0.4-0.5 | 0 | 6 |
| 0.5-0.6 | 0 | 0 |
| 0.6-0.7 | 0 | 0 |
| 0.7-0.8 | 0 | 0 |
| 0.8-0.9 | 0 | 0 |
| 0.9-1.0 | 0 | 0 |

## Summary

- **Queries evaluated**: 30
- **Verdict**: investigate

## Query Latency (end-to-end, including embedding)

| Percentile | Latency |
|------------|---------|
| p50 | 215 ms |
| p95 | 284 ms |

## Storage

| Metric | Value |
|--------|-------|
| Chunks indexed | 43 |
| DB size | 4.3 MB |

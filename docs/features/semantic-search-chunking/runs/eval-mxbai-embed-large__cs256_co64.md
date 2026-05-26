﻿# Semantic Search Quality Evaluation Report

**Generated**: 2026-05-26 10:15:07 UTC

## Overall Verdict
**INVESTIGATE**

## Metrics

| Metric | Value | Pass Threshold | Investigate | Verdict |
|--------|-------|----------------|-------------|---------|
| Recall@1 | 0.5667 | ≥ 0.60 | ≥ 0.45 | **INVESTIGATE** |
| Recall@3 | 0.9333 | ≥ 0.75 | ≥ 0.60 | **PASS** |
| MRR | 0.7556 | ≥ 0.65 | ≥ 0.50 | **PASS** |

## Score Distribution Histogram

| Score Range | Correct | Incorrect |
|-------------|---------|-----------|
| 0.0-0.1 | 1 | 0 |
| 0.1-0.2 | 3 | 2 |
| 0.2-0.3 | 20 | 10 |
| 0.3-0.4 | 5 | 12 |
| 0.4-0.5 | 1 | 3 |
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
| p50 | 288 ms |
| p95 | 387 ms |

## Storage

| Metric | Value |
|--------|-------|
| Chunks indexed | 56 |
| DB size | 4.3 MB |

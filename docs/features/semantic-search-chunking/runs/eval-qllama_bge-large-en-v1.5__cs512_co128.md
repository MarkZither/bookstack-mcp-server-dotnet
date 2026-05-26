﻿# Semantic Search Quality Evaluation Report

**Generated**: 2026-05-26 09:10:03 UTC

## Overall Verdict
**INVESTIGATE**

## Metrics

| Metric | Value | Pass Threshold | Investigate | Verdict |
|--------|-------|----------------|-------------|---------|
| Recall@1 | 0.4667 | ≥ 0.60 | ≥ 0.45 | **INVESTIGATE** |
| Recall@3 | 0.8667 | ≥ 0.75 | ≥ 0.60 | **PASS** |
| MRR | 0.6344 | ≥ 0.65 | ≥ 0.50 | **INVESTIGATE** |

## Score Distribution Histogram

| Score Range | Correct | Incorrect |
|-------------|---------|-----------|
| 0.0-0.1 | 0 | 0 |
| 0.1-0.2 | 3 | 0 |
| 0.2-0.3 | 12 | 6 |
| 0.3-0.4 | 12 | 24 |
| 0.4-0.5 | 0 | 8 |
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
| p50 | 220 ms |
| p95 | 296 ms |

## Storage

| Metric | Value |
|--------|-------|
| Chunks indexed | 44 |
| DB size | 4.1 MB |

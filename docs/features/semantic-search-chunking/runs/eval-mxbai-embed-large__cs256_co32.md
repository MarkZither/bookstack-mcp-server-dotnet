﻿# Semantic Search Quality Evaluation Report

**Generated**: 2026-05-26 10:01:05 UTC

## Overall Verdict
**INVESTIGATE**

## Metrics

| Metric | Value | Pass Threshold | Investigate | Verdict |
|--------|-------|----------------|-------------|---------|
| Recall@1 | 0.5000 | ≥ 0.60 | ≥ 0.45 | **INVESTIGATE** |
| Recall@3 | 0.9333 | ≥ 0.75 | ≥ 0.60 | **PASS** |
| MRR | 0.7189 | ≥ 0.65 | ≥ 0.50 | **PASS** |

## Score Distribution Histogram

| Score Range | Correct | Incorrect |
|-------------|---------|-----------|
| 0.0-0.1 | 1 | 0 |
| 0.1-0.2 | 4 | 1 |
| 0.2-0.3 | 19 | 13 |
| 0.3-0.4 | 5 | 14 |
| 0.4-0.5 | 1 | 4 |
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
| p50 | 292 ms |
| p95 | 365 ms |

## Storage

| Metric | Value |
|--------|-------|
| Chunks indexed | 56 |
| DB size | 4.3 MB |

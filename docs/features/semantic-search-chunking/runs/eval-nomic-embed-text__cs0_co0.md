﻿# Semantic Search Quality Evaluation Report

**Generated**: 2026-05-26 07:30:48 UTC

## Overall Verdict
**PHASE 2 REQUIRED**

## Metrics

| Metric | Value | Pass Threshold | Investigate | Verdict |
|--------|-------|----------------|-------------|---------|
| Recall@1 | 0.0000 | ≥ 0.60 | ≥ 0.45 | **FAIL** |
| Recall@3 | 0.0000 | ≥ 0.75 | ≥ 0.60 | **FAIL** |
| MRR | 0.0483 | ≥ 0.65 | ≥ 0.50 | **FAIL** |

## Score Distribution Histogram

| Score Range | Correct | Incorrect |
|-------------|---------|-----------|
| 0.0-0.1 | 0 | 0 |
| 0.1-0.2 | 0 | 0 |
| 0.2-0.3 | 1 | 1 |
| 0.3-0.4 | 4 | 14 |
| 0.4-0.5 | 2 | 119 |
| 0.5-0.6 | 0 | 9 |
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
| p50 | 89 ms |
| p95 | 154 ms |

## Storage

| Metric | Value |
|--------|-------|
| Chunks indexed | 46 |
| DB size | 3.1 MB |

﻿# Semantic Search Quality Evaluation Report

**Generated**: 2026-05-25 13:32:10 UTC

## Overall Verdict
**INVESTIGATE**

## Metrics

| Metric | Value | Pass Threshold | Investigate | Verdict |
|--------|-------|----------------|-------------|---------|
| Recall@1 | 0.4667 | ≥ 0.60 | ≥ 0.45 | **INVESTIGATE** |
| Recall@3 | 0.8333 | ≥ 0.75 | ≥ 0.60 | **PASS** |
| MRR | 0.6428 | ≥ 0.65 | ≥ 0.50 | **INVESTIGATE** |

## Score Distribution Histogram

| Score Range | Correct | Incorrect |
|-------------|---------|-----------|
| 0.0-0.1 | 0 | 0 |
| 0.1-0.2 | 3 | 0 |
| 0.2-0.3 | 18 | 7 |
| 0.3-0.4 | 6 | 24 |
| 0.4-0.5 | 0 | 8 |
| 0.5-0.6 | 0 | 0 |
| 0.6-0.7 | 0 | 0 |
| 0.7-0.8 | 0 | 0 |
| 0.8-0.9 | 0 | 0 |
| 0.9-1.0 | 0 | 0 |

## Summary

- **Queries evaluated**: 30
- **Verdict**: investigate

---

## Model Comparison

Results across embedding models using the same golden dataset (30 queries, v2).

| Model | Dimensions | Recall@1 | Recall@3 | MRR | Verdict |
|-------|-----------|---------|---------|-----|---------|
| nomic-embed-text:latest (v1.5) | 768 | 0.4333 | 0.8000 | 0.6067 | INVESTIGATE |
| mxbai-embed-large:latest | 1024 | **0.4667** | **0.8333** | **0.6428** | **INVESTIGATE** |

`mxbai-embed-large` outperforms `nomic-embed-text` on all three metrics. Both models require investigation before production use, but mxbai is the recommended default.

> **Note:** Changing embedding models requires updating the compiled dimension constant and starting with a fresh vector database. See the README for guidance.

# Chunk Tuning Evaluation Summary

| Model | ChunkSize | ChunkOverlap | Recall@1 | Recall@3 | MRR | p50 | p95 | Chunks | DbMB |
|-------|-----------|-------------|---------|---------|-----|-----|-----|--------|------|
| nomic-embed-text | 0 | 0 | 0.0000 | 0.0000 | 0.0483 | 89ms | 154ms | 46 | 3.1MB |
| nomic-embed-text | 256 | 32 | 0.4333 | 0.8000 | 0.6067 | 93ms | 115ms | 56 | 3.3MB |
| nomic-embed-text | 256 | 64 | 0.3667 | 0.8000 | 0.5928 | 96ms | 146ms | 56 | 3.3MB |
| nomic-embed-text | 512 | 64 | 0.3667 | 0.6667 | 0.5550 | 97ms | 117ms | 56 | 3.3MB |
| nomic-embed-text | 512 | 128 | 0.4333 | 0.8000 | 0.6067 | 101ms | 132ms | 56 | 3.2MB |
| nomic-embed-text | 512 | 256 | 0.4000 | 0.8333 | 0.6039 | 99ms | 154ms | 56 | 3.3MB |
| nomic-embed-text | 1024 | 256 | 0.3333 | 0.6333 | 0.5022 | 113ms | 138ms | 56 | 3.3MB |
| qllama/bge-large-en-v1.5 | 0 | 0 | 0.0000 | 0.0333 | 0.1678 | 259ms | 337ms | 42 | 4.1MB |
| qllama/bge-large-en-v1.5 | 256 | 32 | 0.4667 | 0.8667 | 0.6650 | 273ms | 363ms | 56 | 4.3MB |
| qllama/bge-large-en-v1.5 | 256 | 64 | 0.6667 | 0.9000 | 0.7856 | 228ms | 303ms | 56 | 4.3MB |
| qllama/bge-large-en-v1.5 | 512 | 64 | 0.3667 | 0.8000 | 0.5750 | 220ms | 292ms | 44 | 4.1MB |
| qllama/bge-large-en-v1.5 | 512 | 128 | 0.4667 | 0.8667 | 0.6344 | 220ms | 296ms | 44 | 4.1MB |
| qllama/bge-large-en-v1.5 | 512 | 256 | 0.4667 | 0.7667 | 0.6500 | 215ms | 284ms | 43 | 4.3MB |
| qllama/bge-large-en-v1.5 | 1024 | 256 | 0.3667 | 0.7000 | 0.5650 | 285ms | 342ms | 44 | 4.2MB |
| mxbai-embed-large | 0 | 0 | 0.0333 | 0.0667 | 0.2028 | 284ms | 366ms | 42 | 4.1MB |
| mxbai-embed-large | 256 | 32 | 0.5000 | 0.9333 | 0.7189 | 292ms | 365ms | 56 | 4.3MB |
| mxbai-embed-large | 256 | 64 | 0.5667 | 0.9333 | 0.7556 | 288ms | 387ms | 56 | 4.3MB |
| mxbai-embed-large | 512 | 64 | 0.4000 | 0.8333 | 0.6094 | 272ms | 346ms | 44 | 4.1MB |
| mxbai-embed-large | 512 | 128 | 0.4333 | 0.8667 | 0.6361 | 300ms | 361ms | 44 | 4.1MB |
| mxbai-embed-large | 512 | 256 | 0.4333 | 0.8667 | 0.6400 | 287ms | 380ms | 43 | 4.3MB |
| mxbai-embed-large | 1024 | 256 | 0.4000 | 0.6667 | 0.5789 | 274ms | 319ms | 44 | 4.2MB |

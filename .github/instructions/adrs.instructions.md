# ADR Instructions

## applyTo

`docs/architecture/decisions/*.md`

## Rules for Architecture Decision Records

1. **Use the template** at `docs/architecture/decisions/ADR-TEMPLATE.md` for every new ADR.
2. **File naming**: `ADR-NNNN-short-title.md` with zero-padded four-digit number (e.g., `ADR-0001-use-dotnet10.md`).
3. **Status values**: `Proposed` → `Accepted` → `Deprecated` / `Superseded by ADR-NNNN`.
4. **Never delete** an ADR; mark it `Deprecated` or `Superseded` instead.
5. **Context section**: describe the problem without a preferred solution.
6. **Decision section**: state the decision clearly in active voice ("We will use…").
7. **Consequences section**: list both positive and negative consequences honestly.
8. **Link related ADRs** using relative Markdown links.
9. **Consult existing ADRs** before creating a new one to avoid duplicates.

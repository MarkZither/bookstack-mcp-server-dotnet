# Specs Instructions

## applyTo

`docs/features/*.md`, `docs/migrations/*.md`

## Rules for Feature and Migration Specs

1. **Use the template**: `docs/features/TEMPLATE.md` for features, `docs/migrations/TEMPLATE.md` for migrations.
2. **File naming**: `FEAT-NNNN-short-title.md` or `MIG-NNNN-short-title.md`.
3. **Status values**: `Draft` → `Review` → `Approved` → `Implemented`.
4. **Requirements**: use numbered lists for functional requirements; use `MUST`, `SHOULD`, `MAY` per RFC 2119.
5. **Acceptance Criteria**: each criterion must be independently verifiable; write in Given/When/Then or checklist format.
6. **Diagrams**: include a sequence or component diagram for any cross-service interaction.
7. **Security**: call out auth, data validation, and sensitive data handling explicitly.
8. **Migration specs**: include rollback plan and estimated downtime for every database or infrastructure change.
9. **Link to ADRs** for any architecture decisions made during spec authoring.
10. **Do not merge** a spec into the codebase while its status is `Draft`.

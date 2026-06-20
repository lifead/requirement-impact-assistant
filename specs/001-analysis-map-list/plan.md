# Implementation Plan: Compact Section List For Saved Analysis Map

**Feature Directory**: `specs/001-analysis-map-list`
**Date**: 2026-06-20
**Spec**: [spec.md](spec.md)

**Status**: Planning documentation only. Code is not implemented in this stage.

## Summary

Future UI-only work will replace the card-style `Карта сохраненного анализа` block on the saved analysis details screen with a compact vertical list. The list will present each saved-analysis section as one row with `Раздел`, short `Статус`, brief description, and existing actions.

The change is intended to make the overview block more compact and familiar without changing subject logic, data, routes, handlers, PageModel behavior, AI/RAG/export behavior, or storage.

## Technical Context

**Project Area**: MVP-4 UI extension (`mvp4_ext`)

**Existing UI Context**: Saved analysis details page currently contains a `Карта сохраненного анализа` block rendered from existing status summary items.

**Primary Boundary**: UI presentation only.

**Storage**: No storage changes.

**Routes/Handlers**: No route or handler changes.

**PageModel**: No PageModel changes.

**AI/RAG Boundary**: No AI/RAG/LLM changes. LLM/AI/RAG material remains preliminary analytical material.

**Export Boundary**: No export behavior or format changes. Export remains saved Markdown/JSON output without reanalysis.

**Testing/Verification**: Future implementation should use manual UI verification and relevant existing regression checks only if touched behavior requires them.

## Constitution / Project Rule Check

| Rule | Status | Notes |
|---|---|---|
| No code before explicit task command | PASS | This artifact is documentation only. |
| One task, one review, one commit | PASS | Future tasks are split in `tasks.md`. |
| AI is preliminary material | PASS | The list must preserve the AI/expert boundary. |
| Human expert decision remains separate | PASS | Expert evaluation and conclusion stay human actions. |
| No RAG/Jira/workflow expansion | PASS | The plan explicitly rejects workflow and platform expansion. |
| No domain/storage/API/DB/export changes | PASS | Scope is UI presentation only. |

## Project Structure

### Documentation (this feature)

```text
specs/001-analysis-map-list/
├── spec.md
├── research.md
├── plan.md
├── quickstart.md
├── tasks.md
└── checklists/
    └── requirements.md
```

### Project Summary Document

```text
docs/program/mvp4_ext/
└── analysis-map-list.md
```

### Future Implementation Surface

Future implementation is expected to be limited to the saved-analysis UI presentation. This plan does not authorize edits to source code; it only records the likely implementation boundary for a later explicit task.

## Phase 0: Research

Completed in [research.md](research.md).

Decision: list/table-of-contents representation is better than cards for this block because it supports compact orientation and avoids making each summary item look like a full content card.

## Phase 1: Design Notes

Future UI design should preserve the current meaning of summary items:

- section title remains the primary row label;
- short status remains close to the section;
- description remains brief;
- actions remain tied to the row;
- no new lifecycle or workflow status is introduced;
- no hidden change to AI, export, storage, or expert boundaries is introduced.

No data model or contracts are created for this feature because the change is presentational and does not add external interfaces or persisted entities.

## Phase 2: Future Implementation Gate

Implementation is blocked until the user explicitly starts a concrete implementation task.

Before future code work:

1. Re-check `git status --short --branch`.
2. Re-read this plan and `tasks.md`.
3. Confirm the task scope is still UI-only.
4. Implement one small task.
5. Run manual verification from `quickstart.md`.
6. Perform review.
7. Show `git diff --stat`.
8. Commit only after review `PASS` and explicit commit command.

## Risks

### Risk: The list becomes a workflow timeline

Mitigation: Use section/status/action language, not approval, task, sprint, backlog, or management decision language.

### Risk: AI result looks final

Mitigation: Preserve wording that AI/RAG/LLM material is preliminary and requires human review.

### Risk: Compactness hides important limitations

Mitigation: Keep status and brief descriptions explicit for incomplete, partial, metadata-only, unavailable, or not-yet-created sections.

### Risk: UI-only task leaks into PageModel or behavior

Mitigation: Review the diff before commit and reject changes to PageModel, handlers, routes, storage/API/DB/AI/export behavior unless a separate approved decision exists.

## Out Of Scope

- implementation during this documentation pass;
- PageModel changes;
- handlers or routes;
- domain model or storage changes;
- API, DB, migrations;
- AI/RAG/LLM provider changes;
- export behavior or export format changes;
- RAG pipeline, embeddings, rerank, vector DB;
- workflow/state-machine/task-board/dashboard behavior;
- Jira/Confluence/ALM-style expansion.

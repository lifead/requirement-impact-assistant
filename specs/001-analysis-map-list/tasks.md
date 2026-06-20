# Tasks: Compact Section List For Saved Analysis Map

**Input**: Design documents from `specs/001-analysis-map-list/`

**Prerequisites**: `spec.md`, `research.md`, `plan.md`, `quickstart.md`

**Status**: Future task breakdown only. Do not implement code from this document without an explicit user command for a concrete task.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel if assigned separately and files do not overlap.
- **[Story]**: Maps to user stories in `spec.md`.
- Every implementation task requires review after completion.
- Commit only after review `PASS` and explicit commit command.

## Phase 1: Documentation Gate

**Purpose**: Confirm documentation is ready before any implementation.

- [ ] T001 Review `specs/001-analysis-map-list/spec.md` against `specs/001-analysis-map-list/checklists/requirements.md`
- [ ] T002 Review `specs/001-analysis-map-list/research.md` and `specs/001-analysis-map-list/plan.md` for scope alignment
- [ ] T003 Review `docs/program/mvp4_ext/analysis-map-list.md` as the project SDD summary

**Checkpoint**: Documentation review complete.

## STOP Before Code

Stop here until the user explicitly starts a concrete implementation task.

No Razor, PageModel, test, project file, route, handler, storage/API/DB/AI/export, or CSS changes are authorized by this documentation pass.

## Phase 2: Future UI Implementation - Compact List (Priority: P1)

**Goal**: Replace the card-style saved analysis map with a compact vertical list while preserving existing section meanings and actions.

**Independent Test**: Use `specs/001-analysis-map-list/quickstart.md` on a saved analysis and confirm the map is compact, row-based, and behavior-preserving.

- [ ] T004 [US1] Re-check current branch and workspace status with `git status --short --branch`
- [ ] T005 [US1] Re-read `specs/001-analysis-map-list/plan.md` and confirm the task is still UI-only
- [ ] T006 [US1] Update the saved-analysis map presentation in `src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml`
- [ ] T007 [US1] Manually verify section row scanning and row actions using `specs/001-analysis-map-list/quickstart.md`
- [ ] T008 [US1] Run relevant existing checks if behavior outside markup was touched
- [ ] T009 [US1] Perform review for compactness, readability, and boundary preservation
- [ ] T010 [US1] Show `git diff --stat`; commit only after review `PASS` and explicit commit command

## Phase 3: Future Boundary Review (Priority: P2)

**Goal**: Confirm the UI-only change did not weaken MVP-4 AI/expert/export boundaries.

**Independent Test**: Reviewer can identify preliminary AI material, expert evaluation, expert conclusion, manual context, retrieved context, diagnostics, and export without semantic confusion.

- [ ] T011 [US2] Review row labels and descriptions for AI preliminary wording
- [ ] T012 [US2] Review manual context and retrieved context rows for origin/state distinction
- [ ] T013 [US2] Review expert evaluation and expert conclusion rows for human-action wording
- [ ] T014 [US2] Review export row/action wording for saved Markdown/JSON without reanalysis
- [ ] T015 [US2] Show `git diff --stat`; commit only after review `PASS` and explicit commit command if changes were made

## Phase 4: Future Scope Audit (Priority: P3)

**Goal**: Confirm no hidden behavior changes were introduced.

**Independent Test**: Diff is limited to approved UI presentation files for the future task.

- [ ] T016 [US3] Audit diff for forbidden changes to PageModel, handlers, routes, domain, storage/API/DB/AI/export behavior, tests, and project files
- [ ] T017 [US3] Confirm no new workflow, task-board, dashboard, RAG platform, export format, or management-decision language was introduced
- [ ] T018 [US3] Record review result before any commit

## Dependencies & Execution Order

- Phase 1 must complete before any implementation.
- Phase 2 is the MVP implementation slice.
- Phase 3 can occur as review/fix work after Phase 2.
- Phase 4 is the final scope audit before commit.

## Parallel Opportunities

- Documentation review tasks T001-T003 can be reviewed independently.
- Boundary review tasks T011-T014 can be split across reviewers after the future UI diff exists.
- Implementation task T006 should remain single-owner because it touches the central presentation file.

## Implementation Strategy

1. Complete documentation gate.
2. Stop until explicit user command for implementation.
3. Implement the compact list as one small UI-only task.
4. Verify with `quickstart.md`.
5. Review after the task.
6. Show `git diff --stat`.
7. Commit only after review `PASS` and explicit commit command.

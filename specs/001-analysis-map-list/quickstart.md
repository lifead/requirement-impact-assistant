# Quickstart: Future Manual Verification

**Feature**: `001-analysis-map-list`
**Status**: Verification guide for a future implementation; no code implemented now.

## Purpose

Use this guide after a future explicit implementation task replaces the saved analysis card map with a compact section list.

## Prerequisites

- The application can be run locally in the usual project development mode.
- A saved `analysis` exists for the MVP demo scenario.
- At least one case has preliminary AI material.
- Ideally one case also has expert evaluation, expert conclusion, manual context, retrieved context state, and export availability.

## Manual Check

1. Open the saved analysis details page.
2. Find the `Карта сохраненного анализа` block.
3. Confirm the block is a compact vertical list/оглавление, not a card grid.
4. Confirm every row has:
   - `Раздел` or an equivalent section title;
   - short `Статус` near the section or in a compact second column;
   - brief description;
   - actions tied to that row.
5. Confirm the list is easier to scan top-to-bottom than the previous card grid.
6. Use the row action that jumps to a section and confirm it targets the existing section.
7. Use any existing page/export actions only where they were already available.
8. Confirm no action suggests a new workflow, approval process, task assignment, or management decision.
9. Confirm preliminary AI material is still described as preliminary and requiring human review.
10. Confirm expert evaluation and expert conclusion are still human actions.
11. Confirm export is still presented as saved Markdown/JSON material without reanalysis.

## Scope Check

Review the future diff:

- no PageModel changes;
- no handler changes;
- no route changes;
- no domain/storage/API/DB/migration changes;
- no AI/RAG/provider changes;
- no export behavior or format changes;
- no tests/project files changed unless separately approved for that implementation task.

## PASS Criteria

The future implementation passes this guide if the saved analysis map is compact and understandable, existing behavior remains unchanged, and AI/expert/export boundaries are preserved.

If any check fails, stop and fix within the same small task before review. Commit only after review `PASS` and explicit commit command.

# Research: Compact Section List For Saved Analysis Map

**Feature**: `001-analysis-map-list`
**Date**: 2026-06-20
**Status**: Documentation artifact; no code implemented

## Context

MVP-4 already frames `Details` as the saved-analysis overview. The current `Карта сохраненного анализа` block uses a card grid for `StatusSummaryItems`. For the `mvp4_ext` UI extension, the goal is narrower: replace this card-style map with a compact vertical list/оглавление строк.

The change must keep the existing subject logic unchanged:

- no PageModel changes;
- no handler, route, data, storage/API/DB/AI/export behavior changes;
- no new workflow, approval state, task board, dashboard, RAG platform, export format, or management automation.

The project boundary remains: LLM/AI/RAG forms preliminary analytical material; expert evaluation and expert conclusion remain human actions.

## Decision

Use a compact vertical section list/table-of-contents representation for the saved analysis map.

Each row should carry:

- `Раздел`;
- short `Статус` near the section or in a second compact column;
- brief description;
- actions associated with the row.

## Rationale

The list better matches the block's real purpose: orientation and navigation. A saved analysis map is not a collection of independent content cards; it is a compact overview of where the analysis stands and what the user can open next.

The list is preferable because it:

- reduces vertical and visual weight compared with a grid of bordered cards;
- supports scanning one item per row in a familiar table-of-contents rhythm;
- keeps status close to the section name instead of making every item look like a full content card;
- makes actions feel attached to a section rather than promoted as separate workflow decisions;
- aligns with MVP-4's goal: `Details` is an overview of a saved `analysis`, not a raw data dump or task board.

## Alternatives Considered

### Keep the card grid

Rejected. Cards give every map item similar visual weight and make the overview larger than necessary. This works for rich content summaries, but the saved analysis map is mainly orientation.

### Use accordions

Rejected for this feature. Accordions would introduce a disclosure interaction and could make the map feel like a second content surface. The goal is compact orientation, not hiding and expanding full sections.

### Use tabs

Rejected. Tabs would imply a stronger navigation model and could look like a restructuring of the page. This feature must not change routes, PageModel, handlers, or the meaning of the existing sections.

### Use a stepper/workflow timeline

Rejected. A stepper risks implying process automation, approval state, or task progression. The project explicitly separates preliminary AI material from human expert work and does not introduce workflow management.

### Use a dense data table

Partially rejected. A table-like rhythm is useful, but the block should stay readable as a section list/оглавление, not become a technical data grid.

## Consequences

The future UI task should be small and reviewable. It should focus on presentation of the existing saved-analysis map items and preserve the section/action semantics already present in the current page.

Review should pay special attention to:

- AI preliminary material not looking like expert conclusion;
- manual context and retrieved context remaining distinct;
- export being presented as saved Markdown/JSON output without reanalysis;
- diagnostics staying secondary to the user's overview route.

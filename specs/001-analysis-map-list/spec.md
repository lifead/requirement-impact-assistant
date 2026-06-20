# Feature Specification: Compact Section List For Saved Analysis Map

**Feature Directory**: `specs/001-analysis-map-list`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User description: "Replace saved analysis card map with compact section list for MVP4 UI extension"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Быстро понять состояние сохраненного analysis (Priority: P1)

Пользователь открывает сохраненный `analysis` и видит компактное оглавление разделов вместо набора карточек. В каждой строке он сразу различает название раздела, короткий статус, краткое описание и доступные действия.

**Why this priority**: Это основной пользовательский выигрыш feature: блок "Карта сохраненного анализа" должен стать компактнее, привычнее и легче для первичной ориентации в MVP.

**Independent Test**: Можно проверить на одном сохраненном `analysis`: пользователь без пояснений разработчика находит нужный раздел, понимает его статус и выбирает действие из строки списка.

**Acceptance Scenarios**:

1. **Given** пользователь открыл сохраненный `analysis` с несколькими смысловыми разделами, **When** он смотрит на блок карты анализа, **Then** он видит вертикальный список строк с колонками или рядом расположенными областями `Раздел`, `Статус`, описание и действия.
2. **Given** у раздела есть короткий статус и действия, **When** пользователь просматривает строку, **Then** статус воспринимается как пояснение к разделу, а действия остаются связанными именно с этой строкой.
3. **Given** список содержит несколько разделов, **When** пользователь сканирует блок сверху вниз, **Then** блок занимает меньше визуального пространства, чем карточная сетка, и не выглядит как набор равнозначных крупных cards.

---

### User Story 2 - Сохранить смысловые границы MVP-4 (Priority: P2)

Пользователь видит, что изменение касается только формы представления карты анализа. Смысловые слои `input`, `manual context`, `retrieved context`, `preliminary AI material`, `expert evaluation`, `expert conclusion` и `export` не смешиваются и не получают новой предметной логики.

**Why this priority**: MVP-4 требует регистрационно безопасного разделения AI preliminary material и человеческого экспертного действия. UI-упрощение не должно менять значение результата.

**Independent Test**: Можно проверить на сохраненном анализе с preliminary result, expert evaluation, expert conclusion и export: список помогает ориентироваться, но не представляет AI-result как expert conclusion или управленческое решение.

**Acceptance Scenarios**:

1. **Given** preliminary AI material уже сформирован, **When** пользователь смотрит на список разделов, **Then** preliminary material остается обозначенным как предварительный аналитический материал, требующий человеческой проверки.
2. **Given** expert evaluation или expert conclusion присутствует или отсутствует, **When** пользователь видит соответствующую строку, **Then** он понимает, что это человеческое действие, а не результат AI/RAG/LLM.
3. **Given** export доступен, **When** пользователь видит действие export в строке или рядом с ней, **Then** export воспринимается как выгрузка сохраненного материала без повторного анализа.

---

### User Story 3 - Не изменить поведение сохраненного анализа (Priority: P3)

Демонстратор или reviewer убеждается, что UI-доработка не меняет PageModel, handlers, routes, данные, storage/API/DB/AI/export behavior и не добавляет новую платформенную функцию.

**Why this priority**: Feature является UI-only extension к MVP-4. Любое изменение предметной логики или boundary нарушит scope.

**Independent Test**: Можно сравнить доступные действия и переходы до и после будущей реализации: они остаются теми же, меняется только компактность и читаемость блока карты.

**Acceptance Scenarios**:

1. **Given** строка содержит действие перехода к разделу, **When** пользователь выбирает это действие, **Then** он попадает к тому же смысловому разделу сохраненного анализа, который был доступен до UI-доработки.
2. **Given** строка содержит действие на существующую страницу или export handler, **When** пользователь выбирает его, **Then** используется существующее поведение без нового маршрута, нового состояния или повторного AI/RAG/LLM-вызова.
3. **Given** у анализа нет части данных, например preliminary result или expert conclusion, **When** пользователь смотрит на соответствующую строку, **Then** список показывает отсутствие или незавершенность как статус, не подменяя данные и не создавая новые сущности.

### Edge Cases

- Если у строки несколько действий, они должны оставаться визуально связанными с этой строкой и не превращать список в панель workflow-actions.
- Если описание раздела длиннее ожидаемого, список должен сохранять компактность за счет краткого текста, а не раскрывать полный content раздела внутри оглавления.
- Если статус раздела отсутствует или нейтрален, строка должна сохранять понятность без вынужденного добавления нового доменного статуса.
- Если на узком экране `Раздел` и `Статус` не помещаются в одну строку, статус может располагаться рядом или вторым столбцом/рядом без потери связи с разделом.
- Если retrieved context отсутствует, частичен или представлен только metadata, список должен показывать это как ограничение, а не как найденный источник.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: UI MUST replace the saved analysis card map with a compact vertical section list/table-of-contents representation.
- **FR-002**: Each list row MUST include the section title (`Раздел`), a short status (`Статус`), a brief description, and available actions for that section.
- **FR-003**: The status MUST be concise and visually tied to the section, either adjacent to `Раздел` or in a second compact column.
- **FR-004**: The list MUST preserve the existing set of section meanings represented by the saved analysis map; it MUST NOT add new lifecycle states or workflow concepts.
- **FR-005**: The list MUST preserve existing actions and their meanings; it MUST NOT introduce new routes, handlers, export formats, AI calls, storage behavior, or management actions.
- **FR-006**: The list MUST support quick scanning from top to bottom and reduce visual weight compared with the current card grid.
- **FR-007**: The list MUST keep preliminary AI material visibly distinct from expert evaluation and expert conclusion.
- **FR-008**: The list MUST keep manual context and retrieved context distinguishable by origin and state.
- **FR-009**: The list MUST keep diagnostics and raw data secondary to the main saved-analysis overview.
- **FR-010**: The list MUST treat export as access to saved Markdown/JSON material without suggesting reanalysis or a new AI/RAG/LLM call.
- **FR-011**: The list MUST remain understandable when some analysis sections are incomplete, unavailable, partial, metadata-only, or not yet created.
- **FR-012**: The feature MUST be documented and reviewed before any code implementation starts.

### Key Entities

- **Saved analysis map item**: Existing conceptual item in the saved analysis overview. It has a section name, short status, brief description, anchor/action targets, and optional actions.
- **Section row**: Target presentation unit for one saved analysis map item. It is a compact row, not a new domain entity.
- **Action**: Existing user operation associated with a section, such as navigating to a section, opening an existing page, or using an existing export handler.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A reviewer can identify the section, status, description, and available actions for at least 90% of list rows within 30 seconds on a representative saved analysis.
- **SC-002**: The saved analysis map block uses fewer large visual containers than the current card grid and can be scanned as one vertical list.
- **SC-003**: In manual review, no row suggests that AI/RAG/LLM material is an expert conclusion or management decision.
- **SC-004**: In manual review, existing navigation and export actions remain semantically unchanged.
- **SC-005**: In scope review, changed implementation files for the future task are limited to UI presentation and do not include PageModel, handlers, routes, domain, storage/API/DB/AI/export behavior unless a separate approved decision exists.

## Assumptions

- The current source of rows is the existing saved analysis status summary data used by `Details`.
- The feature is an MVP-4 extension (`mvp4_ext`) and not a new product area.
- The preferred representation is a vertical list or table-of-contents-like block, not another card grid.
- Future implementation may adjust markup and UI text, but this documentation stage does not implement code.
- The feature keeps existing project visual conventions unless a later approved implementation task finds a smaller project-consistent option.
- Expert evaluation and expert conclusion remain human actions; LLM/AI/RAG material remains preliminary analytical material.

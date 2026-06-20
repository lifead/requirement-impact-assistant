# MVP-4 UI Technical Design

## Статус

Этот документ является technical design artifact для MVP-4 UI.

Документ не является implementation plan, task breakdown или решением о начале реализации.

Документ не создает новых требований и не фиксирует изменение domain model, storage, API, routes, DB/migrations, state machine, AI/RAG boundary, provider-specific DTO, export format или PDF generation.

Следующий допустимый SDD-шаг после этого документа: `mvp4-ui-technical-design-review.md`.

## Goals

MVP-4 UI должен упростить работу пользователя с одним сохраненным `analysis` за счет более ясной смысловой организации существующих экранов.

Цели technical design:

- сохранить один сохраненный `analysis` как центральный рабочий артефакт;
- сохранить существующие Razor Pages и PageModel boundaries;
- уточнить целевую смысловую роль экранов `Analyses Index`, `Review`, `Details`, `ExpertEvaluation`, `ExpertConclusion` и export;
- отделить `input`, `manual context`, `retrieved context`, `preliminary AI material`, `grounds/limitations`, `expert evaluation`, `expert conclusion` и export;
- сделать `Details` обзорной точкой сохраненного анализа, а не равноправным raw data dump;
- сохранить регистрационно безопасное разделение: AI/RAG/LLM формирует preliminary analytical material, человек выполняет expert evaluation и expert conclusion;
- обеспечить воспроизводимый demo-сценарий без обязательной сети, secrets, real Dify или DeepSeek.

## Non-Goals

В рамках MVP-4 technical design не предусматриваются:

- новая domain model;
- изменение storage, DB schema или migrations;
- новые API endpoints;
- новые routes;
- новый workflow, approval process или state machine;
- task board, backlog, sprint management, ALM/Jira/Confluence-аналог;
- новая RAG-платформа;
- embeddings, vector DB, rerank или новый retrieval pipeline;
- расширение Dify adapter;
- provider-specific DTO в UI или export;
- новый export format;
- PDF generation;
- автоматическое принятие управленческих решений;
- прямые AI/provider вызовы из UI, Razor Pages или PageModels;
- изменение существующего AI/export boundary.

## Current Implementation Map

### `Analyses Index`

Существующая страница:

- route: `/Analyses`;
- PageModel: `IndexModel(ApplicationDbContext)`;
- handler: `OnGetAsync()`;
- модель списка: `AnalysisListItem` с `Id`, `Title`, `Status`, `UpdatedAt`;
- действия: `Review`, `Details`, `Create`.

Текущая смысловая роль: список сохраненных анализов и точка выбора дальнейшего действия.

### `Review`

Существующая страница:

- route: `/Analyses/{id:guid}/Review`;
- PageModel: `ReviewModel(ApplicationDbContext, IAnalysisExecutionService?)`;
- handlers: `OnGetAsync(Guid id)`, `OnPostRunAnalysisAsync(Guid id)`;
- input: `RunAnalysisInput` с `AnalysisMode`;
- loads: `Analysis` with `ContextFragments`;
- renders: readiness через `HasMinimumInput`, режим `DirectLlm` / `ExternalRag`, запуск preliminary analysis, input fields, manual context.

Текущая смысловая роль: preflight перед запуском preliminary AI material.

### `Details`

Существующая страница:

- route: `/Analyses/{id:guid}`;
- PageModel: `DetailsModel(ApplicationDbContext, IWebHostEnvironment?, IAnalysisMarkdownExportService?, IAnalysisJsonExportService?)`;
- handlers: `OnGetAsync`, `OnGetExportMarkdownAsync`, `OnGetExportJsonAsync`, `OnPostAddContextFragmentAsync`, `OnPostDeleteContextFragmentAsync`;
- loads: `ContextFragments`, `AiAnalysisResult.Metadata.RetrievedContextItems`, `ExpertEvaluation`, `ExpertConclusion`;
- renders: status summary, `analysis-input`, `expert-conclusion`, `preliminary-result`, `grounds-limitations`, `impact-map`, `retrieved-context`, `expert-evaluation`, `export`, `manual-context`, diagnostics/raw data.

Текущая смысловая роль: обзор сохраненного `analysis`, но сейчас экран перегружен и содержит почти весь жизненный цикл анализа.

### `ExpertEvaluation`

Существующая страница:

- route: `/Analyses/{id:guid}/ExpertEvaluation`;
- PageModel: `ExpertEvaluationModel(ApplicationDbContext)`;
- handlers: `OnGetAsync(Guid id)`, `OnPostAsync(Guid id)`;
- can evaluate only when `AiAnalysisResult.ImpactMap` exists and status is `Completed` / `CompletedWithWarnings`;
- loads: AI result metadata/retrieved context, existing expert evaluation, evaluated items, missed items, corrections;
- renders: context summary, input data, grounds/limitations, preliminary impact map, retrieved context summary, warnings/limitations, evaluation tables, missed items, corrections, sufficiency/usefulness/general comment.

Текущая смысловая роль: человеческая проверка preliminary AI material.

### `ExpertConclusion`

Существующая роль: фиксация человеческого экспертного заключения.

Design исходит из того, что expert conclusion является отдельным человеческим действием и не является AI-result, workflow approval или автоматической командой к реализации.

### Shared UI

Существующие shared элементы:

- `AnalysisUiText` централизует labels/select items для statuses, modes, retrieved context, expert marks, impact types/severity;
- Bootstrap layout уже подключен;
- `site.css` минимален;
- `_Layout.cshtml` задает общую навигацию.

Technical design не требует новой UI framework, новой shared component system или изменения frontend architecture.

## Design Principles

### Existing Boundaries First

UI должен использовать существующие PageModels, services и domain entities.

Запуск анализа остается только через `IAnalysisExecutionService` и далее через `IAiAnalysisEngineSelector`.

UI не вызывает LLM, RAG adapter, DeepSeek, Dify или provider напрямую.

### One Saved `analysis` As Working Object

Все экраны должны поддерживать понимание, что пользователь работает с одним сохраненным `analysis`.

`analysis` не трактуется как ticket, approval item, task или workflow item.

### Semantic Separation Before Visual Simplification

Упрощение UI начинается с смыслового разделения данных:

- `input`;
- `manual context`;
- `retrieved context`;
- `preliminary AI material`;
- `grounds/limitations`;
- `expert evaluation`;
- `expert conclusion`;
- `export`;
- diagnostics.

Конкретный layout не фиксируется этим документом.

### AI Is Preliminary, Human Is Final

Любое представление AI/RAG/LLM-result должно сохранять его предварительный характер.

Expert evaluation и expert conclusion должны оставаться человеческими действиями.

### Diagnostics Do Not Drive The Main Scenario

Raw response, provider metadata, adapter diagnostics, internal identifiers и low-level retrieval metadata могут оставаться доступными для проверки MVP, но не должны определять основной пользовательский маршрут.

### Provider Neutrality

UI должен показывать режим анализа на provider-neutral уровне: `Direct LLM` или `External AI/RAG`.

UI не должен раскрывать secrets, private endpoints, bearer values, cookies, CSRF или raw provider payload как часть основного пользовательского сценария.

## Semantic Grouping Strategy

Для design используется словарь `primary / secondary / diagnostic`.

Эти термины являются design vocabulary, а не пользовательскими labels, не UI architecture и не требованием к tabs, panels, routes, components или display order.

### Primary Information

Primary information нужна для понимания сути анализа и человеческого решения:

- title;
- request type;
- current state;
- proposed change;
- situation/reason;
- source of change;
- manual context;
- retrieved context state;
- preliminary AI material;
- preliminary impact map;
- grounds/limitations;
- expert evaluation;
- expert conclusion;
- exportable result.

### Secondary Information

Secondary information помогает ориентации и воспроизводимости:

- timestamps;
- analysis status;
- provider-neutral analysis mode;
- retrieved context summary;
- warnings summary;
- expert evaluation status;
- export availability;
- input snapshot.

### Diagnostic Information

Diagnostic information нужна для troubleshooting, review и проверки корректности MVP:

- raw response;
- detailed provider metadata;
- adapter diagnostics;
- technical payload fragments;
- detailed warnings;
- internal identifiers;
- low-level retrieval metadata;
- JSON-like technical fragments.

Diagnostic information не должна быть визуально или смыслово равна primary information.

## Per-Screen Design Direction

## `Analyses Index`

Целевая роль: список сохраненных анализов и точка входа в работу с конкретным `analysis`.

Design direction:

- сохранить существующий route `/Analyses`;
- сохранить существующую модель списка `Id`, `Title`, `Status`, `UpdatedAt`;
- смыслово различать действия `Review` и `Details`;
- `Review` должен восприниматься как preflight/подготовка к preliminary analysis;
- `Details` должен восприниматься как обзор сохраненного состояния анализа;
- статус анализа должен помогать понять, какое человеческое действие логически ожидается, без введения нового workflow/state machine.

Ограничение: design не требует новых routes, новых status entities или новой navigation model.

## `Review`

Целевая роль: preflight перед запуском preliminary AI material.

Design direction:

- сохранить запуск preliminary analysis через `IAnalysisExecutionService`;
- сохранить выбор только между существующими режимами `DirectLlm` и `ExternalRag`;
- сделать смысловой фокус экрана: готовность input и manual context к запуску анализа;
- показывать input fields как объект проверки до анализа;
- показывать manual context как данные, добавленные человеком;
- показывать analysis mode provider-neutral образом;
- не смешивать `Review` с expert evaluation;
- не превращать `Review` в полный дубликат `Details`.

Ограничение: design не добавляет новых modes, provider calls, AI boundary или state machine.

## `Details`

Целевая роль: обзорная точка сохраненного `analysis`.

Design direction:

- сохранить существующий route `/Analyses/{id:guid}`;
- сохранить загрузку существующих связанных данных: context fragments, AI result metadata/retrieved context, expert evaluation, expert conclusion;
- организовать смысловую подачу вокруг вопроса: что уже известно по анализу и какое человеческое действие логически остается следующим;
- отделить primary information от diagnostics на уровне смысловой приоритетности;
- preliminary AI material должен быть явно отличим от expert conclusion;
- grounds/limitations должны объяснять границы preliminary result, а не выглядеть как чистая техническая диагностика;
- retrieved context должен отличаться от manual context по происхождению;
- отсутствие, частичность или metadata-only состояние retrieved context должно быть видно как limitation;
- expert evaluation status должен помогать понять, была ли человеческая проверка;
- expert conclusion должен отображаться как человеческое заключение;
- export должен восприниматься как сохраненный результат без повторного анализа.

Ограничение: design не фиксирует конкретный layout, tabs, panels, accordions, component structure или CSS.

## `ExpertEvaluation`

Целевая роль: человеческая проверка preliminary AI material.

Design direction:

- сохранить условие доступности evaluation: impact map exists and status is `Completed` / `CompletedWithWarnings`;
- сохранить отсутствие AI/provider вызовов из PageModel;
- preliminary impact map должна быть представлена как материал для человеческой оценки;
- expert marks, comments, corrections, missed items, context sufficiency и usefulness должны восприниматься как действия человека;
- retrieved context summary и grounds/limitations должны помогать эксперту оценить достаточность основания;
- warnings/limitations должны быть отделены от экспертного вывода;
- экран не должен создавать впечатление повторного AI анализа.

Ограничение: design не добавляет новые expert workflow states, approval actions или task assignment.

## `ExpertConclusion`

Целевая роль: фиксация итогового человеческого экспертного заключения.

Design direction:

- expert conclusion должен быть отделен от preliminary AI material;
- expert conclusion должен быть отделен от expert evaluation;
- expert conclusion должен восприниматься как человеческое действие;
- expert conclusion не должен выглядеть как AI-generated conclusion, workflow approval или автоматическое решение программы;
- при наличии expert conclusion `Details` и export должны сохранять это разделение.

Ограничение: design не добавляет approval workflow, status machine или управленческую автоматизацию.

## Export

Целевая роль: получение сохраненного Markdown/JSON результата без повторного анализа.

Design direction:

- сохранить существующие export services;
- export должен читать сохраненное состояние из DB;
- export не должен вызывать `IAnalysisExecutionService`, `IAiAnalysisEngine`, LLM provider или RAG adapter;
- export должен сохранять различимость:
  - input;
  - manual context;
  - retrieved context;
  - preliminary AI material;
  - grounds/limitations;
  - expert evaluation;
  - expert conclusion;
  - decision boundary;
- export должен оставаться Markdown/JSON, без нового формата и без PDF.

Ограничение: design не расширяет export format и не меняет export boundary.

## Data And Boundary Impact

Data impact: none.

Storage impact: none.

DB/migrations impact: none.

API impact: none.

Routes impact: none.

State machine impact: none.

Domain model impact: none.

AI/RAG boundary impact: none.

Provider-specific DTO impact: none.

Export format impact: none.

PDF impact: none.

MVP-4 UI technical design должен использовать только существующие boundaries:

- `IAnalysisExecutionService` для запуска анализа;
- `IAiAnalysisEngineSelector` для выбора `DirectLlm` / `ExternalRag`;
- `IAiAnalysisEngine` как application-level AI boundary;
- `ILlmProvider` внутри Direct LLM boundary;
- `IExternalRagAdapter` внутри External AI/RAG boundary;
- existing `ExpertEvaluation` / `ExpertConclusion` domain entities and PageModels;
- existing Markdown/JSON export services.

## Offline Demo Safety Design

Offline demo safety должна опираться на существующие механизмы.

### Direct LLM

Для воспроизводимого demo-сценария `Direct LLM` должен использовать existing `DemoLlmProvider` в development/demo configuration.

`DemoLlmProvider` является deterministic, не требует сети и не использует secrets.

Technical design не требует изменения `DemoLlmProvider`.

### External AI/RAG

Для воспроизводимого demo-сценария `External AI/RAG` должен использовать existing `MockExternalRagAdapter`, если real Dify adapter не настроен.

`MockExternalRagAdapter` уже поддерживает состояния:

- happy-path;
- metadata-only;
- unavailable;
- partial;
- failed.

Technical design не требует расширения adapter-а или добавления нового RAG pipeline.

### Configuration Boundary

`appsettings.Development.json` использует `AiAnalysis:Provider = Demo`.

Production/default `appsettings.json` может указывать `DeepSeek`, поэтому offline demo safety не должна описываться как production default.

Design decision: воспроизводимый demo должен ссылаться на development/demo configuration и existing mock/demo providers, а не на real provider configuration.

## Testing And Verification Approach

Testing/verification на уровне technical design должен подтвердить, что будущие UI-изменения сохраняют требования и boundaries.

Проверка должна покрывать:

- пользователь различает `Review` как preflight и `Details` как saved analysis overview;
- `Details` не воспринимается как raw data dump без смысловой приоритетности;
- preliminary AI material не выглядит как expert conclusion;
- expert evaluation выглядит как человеческая проверка preliminary AI material;
- expert conclusion выглядит как отдельное человеческое заключение;
- manual context и retrieved context различимы по происхождению;
- retrieved context state visible: available, partial, metadata-only или unavailable;
- отсутствие retrieved context не маскируется выдуманными источниками;
- grounds/limitations не исчезают и не превращаются только в provider diagnostics;
- diagnostic information не управляет основным пользовательским маршрутом;
- export не запускает повторный AI/RAG/LLM analysis;
- Direct LLM demo remains offline-safe through existing demo provider;
- External AI/RAG demo remains offline-safe through existing mock adapter when real adapter is not configured.

Существующие regression tests для export boundary должны оставаться релевантными: export читает сохраненное состояние и не зависит от engines/providers/network.

Новые проверки на этапе реализации должны соответствовать будущему implementation plan. Этот документ не формирует task breakdown.

## Risks

### Risk: `Details` Remains Too Dense

Если `Details` продолжит показывать all information as equal, пользователь не отличит primary information от diagnostics.

Mitigation: на design level закрепить `Details` как overview, где diagnostics имеют вспомогательную роль.

### Risk: AI Result Looks Final

Если preliminary AI material визуально или текстово выглядит как итоговое заключение, нарушается project boundary.

Mitigation: все design directions должны сохранять wording preliminary material и отделять его от expert evaluation/conclusion.

### Risk: Expert Evaluation And Expert Conclusion Mixed

Если expert evaluation и expert conclusion подаются как один слой, пользователь может не понять разницу между проверкой элементов и итоговой человеческой позицией.

Mitigation: сохранить разные смысловые роли этих действий.

### Risk: Retrieved Context Treated As Proof

Retrieved context может быть ошибочно воспринят как доказательство правильности AI-result.

Mitigation: показывать retrieved context как источник/metadata/контекст с состоянием доступности и ограничениями.

### Risk: Offline Demo Depends On Production Config

Production/default config может быть связан с real provider.

Mitigation: offline demo safety должна ссылаться на development/demo configuration, `DemoLlmProvider` и `MockExternalRagAdapter`.

### Risk: Technical Design Becomes Implementation Plan

Слишком конкретное описание UI может превратиться в layout/component/task list.

Mitigation: этот документ фиксирует screen roles, semantic grouping и boundary usage, но не задает layout, component structure, CSS или code tasks.

## Traceability To Requirements

| Requirement | Design Coverage |
|---|---|
| FR-001 Central Analysis Artifact | Один сохраненный `analysis` закреплен как центральный рабочий объект. |
| FR-002 Saved Analyses Orientation | `Analyses Index` определен как список и точка выбора действия. |
| FR-003 Review As Preflight | `Review` спроектирован как preflight перед preliminary analysis. |
| FR-004 Input Readiness | `Review` фокусируется на input и manual context readiness. |
| FR-005 Analysis Mode Clarity | Режимы остаются provider-neutral: `DirectLlm` / `ExternalRag`. |
| FR-006 Details As Saved Analysis Overview | `Details` закреплен как overview сохраненного анализа. |
| FR-007 Details Must Not Equal Raw Data Dump | Введена semantic priority и separation of diagnostics. |
| FR-008 Preliminary AI Material Visibility | Preliminary AI material отделен от expert conclusion. |
| FR-009 Expert Evaluation Role | `ExpertEvaluation` описан как человеческая проверка preliminary material. |
| FR-010 Expert Conclusion Separation | Expert conclusion описан как отдельное человеческое действие. |
| FR-011 Manual Context Origin | Manual context определяется как добавленный человеком контекст. |
| FR-012 Retrieved Context Origin And State | Retrieved context определяется через adapter/mock adapter state. |
| FR-013 No Fabricated Retrieved Context | Отсутствие retrieved context должно отображаться как limitation. |
| FR-014 Grounds And Limitations | Grounds/limitations объясняют границы preliminary result. |
| FR-015 Diagnostics Are Not Main Scenario | Diagnostic information не управляет основным маршрутом. |
| FR-016 Export Without Reanalysis | Export читает сохраненное DB state и не вызывает providers. |
| FR-017 Export Layer Separation | Export сохраняет разделение смысловых слоев. |
| FR-018 Demo Scenario Completeness | Design поддерживает путь input -> preliminary result -> expert evaluation -> expert conclusion -> export. |
| FR-019 Offline Default Demo Safety | Offline demo опирается на `DemoLlmProvider` и `MockExternalRagAdapter` в development/demo config. |
| FR-020 No Automatic Management Decision | AI/RAG/LLM сохраняется как preliminary analytical material. |
| IH-001..IH-006 | `primary / secondary / diagnostic` используется только как semantic design vocabulary. |
| AE-001..AE-006 | AI boundary и human expert boundary сохранены. |
| QR-001..QR-006 | Traceability, provider neutrality, phase gate и boundary preservation сохранены. |

## Next SDD Step

Следующий допустимый SDD-шаг: подготовить `mvp4-ui-technical-design-review.md`.

Technical design review должен проверить:

- не появились ли скрытые implementation tasks;
- не зафиксированы ли layout/components/routes/PageModel/CSS;
- не затронуты ли domain/storage/API/DB/migrations/state-machine boundaries;
- не расширен ли AI/RAG/export boundary;
- сохраняется ли разделение preliminary AI material, expert evaluation и expert conclusion;
- достаточно ли design связан с approved requirements draft.

После technical design review переход к implementation plan допустим только при статусе `PASS`.

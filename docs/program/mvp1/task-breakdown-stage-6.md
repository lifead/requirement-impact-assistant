# Task breakdown MVP-1. Stage 6

## Назначение

Документ фиксирует маленькие implementation tasks только для этапа 6 из `docs/program/mvp1/implementation-plan.md`: "UI для выбора режима и просмотра retrieved context".

Это именно breakdown Stage 6, а не начало реализации. Реализация Stage 6 должна начаться только после отдельного review/approval этого breakdown. Каждая task должна проходить отдельный цикл:

```text
task review -> implementation -> code review -> commit -> next task
```

Цель Stage 6 - расширить существующий UI MVP-0/MVP-1 так, чтобы пользователь мог выбрать режим анализа перед запуском и увидеть сохраненное основание external AI/RAG результата: mode, metadata, warnings/limitations и retrieved context, если он был сохранен.

## Основание

- `docs/program/mvp1/implementation-plan.md`
- `docs/program/mvp1/technical-design.md`
- `docs/program/mvp1/requirements-draft.md`
- `docs/program/mvp1/stage-5-summary.md`
- `docs/program/mvp1/task-breakdown-stage-4.md`
- `docs/program/mvp1/task-breakdown-stage-5.md`

## Реальная точка старта

По текущей структуре проекта Stage 1-5 уже подготовили application/data/export foundation:

- `AnalysisMode.DirectLlm` и `AnalysisMode.ExternalRag`;
- `IAiAnalysisEngineSelector` и `AnalysisExecutionService.RunAsync(..., AnalysisMode, ...)`;
- `DirectLlmAnalysisEngine` как default path;
- `ExternalRagAnalysisEngine` как единственную application-level точку обращения к external adapter;
- `MockExternalRagAdapter` для воспроизводимого local external режима;
- optional `DifyExternalRagAdapter` behind configuration;
- сохранение `AiAnalysisResultMetadata` и `RetrievedContextItem`;
- Markdown/JSON export сохраненных metadata и retrieved context без вызова engine/adapter/provider;
- architecture regression checks Stage 5, запрещающие Dify leakage в UI/domain/export/direct LLM.

На старте Stage 6 UI еще не дает пользователю выбрать режим анализа. `Review` page запускает анализ через default `RunAsync(id, cancellationToken)`, а `Details` page показывает базовые поля результата без отдельного блока mode metadata/retrieved context. Это и есть scope Stage 6.

## Границы Stage 6

Входит:

- PageModel/input boundary для выбора `AnalysisMode` перед запуском анализа;
- сохранение default behavior: если пользователь не выбрал режим явно, используется `DirectLlm`;
- UI controls для выбора Direct LLM / External AI/RAG на странице запуска анализа;
- отображение доступности external режима и понятных warnings/limitations;
- отдельный UI choice, передавать ли manual context во внешний AI/RAG-контур, только если такая возможность уже поддержана application layer;
- запуск external режима только через существующий application-level boundary/service selector;
- отображение mode, engine/provider/adapter metadata сохраненного результата;
- отображение warnings/limitations adapter/engine/result;
- отображение retrieved context или retrieved context metadata, сохраненных в result;
- явное сообщение, если retrieved context unavailable или partial;
- UI/export review только над сохраненным result/context, если task Stage 6 требует проверки export;
- page/UI tests и architecture checks, фиксирующие boundary.

Не входит:

- direct dependency UI/PageModels на `DifyExternalRagAdapter`, Dify DTO, provider payload, endpoint/API key или Dify options;
- прямые external calls из UI/Razor Pages/PageModels;
- вызов `IAiAnalysisEngine`, adapter или provider из export;
- own RAG/retrieval pipeline, embeddings, rerank, vector DB, retrieval trace или agentic workflow;
- UI для настройки endpoint, workflow, API key или Dify knowledge base;
- новые secrets/configuration;
- migrations/persistence changes;
- изменения MVP-0 docs;
- dashboard, PDF, workflow согласования, автоматическое создание задач;
- изменение production code до отдельного approval конкретной task.

## Task 1. Добавить PageModel/input boundary для выбора AnalysisMode

**Цель:** подготовить server-side input boundary на странице запуска анализа, чтобы `ReviewModel` принимал выбранный `AnalysisMode` и передавал его в `IAnalysisExecutionService.RunAsync(id, mode, ...)`, сохраняя `DirectLlm` как default.

**Зависимости:** завершенная Stage 5; существующие `Review.cshtml.cs`, `IAnalysisExecutionService`, `AnalysisMode`, `AnalysisPagesTests`.

**Входит:**

- bind/input model для выбора режима анализа на `ReviewModel` или рядом с ним;
- валидация допустимых значений `AnalysisMode`;
- default `DirectLlm`, если POST пришел без явного значения или со старой формой;
- вызов `analysisExecutionService.RunAsync(id, selectedMode, cancellationToken)` через application service;
- page model tests, что:
  - default POST использует `DirectLlm`;
  - explicit `ExternalRag` передается в service;
  - invalid mode не запускает анализ и возвращает validation error;
  - not found / invalid input behavior остается прежним;
- отсутствие dependency PageModel на `IAiAnalysisEngineSelector`, `IExternalRagAdapter`, Dify adapter/options/DTO.

**Не входит:**

- Razor UI controls;
- отображение availability external режима;
- изменения `AnalysisExecutionService`;
- Dify/provider configuration;
- export changes;
- retrieved context rendering.

**Ожидаемый diff:**

- narrow update `src/RequirementImpactAssistant.Web/Pages/Analyses/Review.cshtml.cs`;
- tests in `tests/RequirementImpactAssistant.Tests/Pages/AnalysisPagesTests.cs`;
- possible small helper in `AnalysisUiText` only if needed for labels;
- no changes in application engines, adapters, export services, migrations.

**Проверки:**

- `dotnet test`;
- targeted `AnalysisPagesTests`;
- manual diff review, что UI/PageModel не получил direct Dify/external adapter dependency.

**Критерии Done:**

- `DirectLlm` остается default path;
- external mode can be requested from PageModel only as `AnalysisMode.ExternalRag`;
- PageModel calls only `IAnalysisExecutionService`;
- invalid input cannot force arbitrary provider/adapter behavior.

**Red flags для review:**

- PageModel injects `IAiAnalysisEngineSelector`, `IExternalRagAdapter`, `DifyExternalRagAdapter` or options;
- default mode changes to external;
- invalid mode silently falls through to external;
- task одновременно добавляет Razor controls или result rendering.

## Task 2. Добавить UI controls выбора режима и unavailable external state

**Цель:** дать пользователю понятный выбор Direct LLM / External AI/RAG перед запуском анализа и показать, что external режим может быть недоступен или ограничен, не превращая UI в настройку provider.

**Зависимости:** Task 1; существующий `Review.cshtml`; project UI style на Razor Pages/Bootstrap.

**Входит:**

- radio/select controls для `AnalysisMode.DirectLlm` и `AnalysisMode.ExternalRag`;
- visible default Direct LLM selection;
- text warning, что external AI/RAG может передавать данные во внешний контур;
- disabled/unavailable state для external режима, если application-level availability уже можно определить без обращения к provider;
- если application-level availability еще не выделена, зафиксировать минимальный UI fallback: external mode selectable only when service can handle it locally/mock/configured, а unavailable/failure показывается через outcome/result;
- optional checkbox "передавать manual context во внешний AI/RAG-контур" только если Stage 1-5 уже поддерживают это на application boundary;
- UI tests/snapshot-style assertions through page tests, что controls rendered and POST names match PageModel input.

**Не входит:**

- UI для Dify endpoint/workflow/API key;
- provider-specific availability probe из Razor;
- real network check;
- изменение DI/configuration;
- новый application service для availability без отдельного решения в task review.

**Ожидаемый diff:**

- update `src/RequirementImpactAssistant.Web/Pages/Analyses/Review.cshtml`;
- small additions to `AnalysisUiText` if labels need centralized wording;
- page tests for rendered form and disabled/warning states;
- no adapter/provider/export/persistence changes.

**Проверки:**

- `dotnet test`;
- targeted page tests;
- manual browser smoke may be useful after implementation, but Stage 6 не требует Playwright framework без отдельного решения.

**Критерии Done:**

- пользователь видит выбранный режим до запуска;
- Direct LLM clearly remains default/fallback;
- external mode warning/limitation is visible before запуск;
- UI does not expose endpoint/API key/workflow/provider payload.

**Red flags для review:**

- Razor page reads Dify options directly;
- UI performs HTTP/API availability check;
- user can edit provider endpoint/key in MVP UI;
- manual context and retrieved context are mixed in wording.

## Task 3. Отобразить result metadata, external status, warnings и limitations

**Цель:** расширить `Details` page model/view так, чтобы сохраненный результат показывал режим анализа и neutral metadata результата без provider payload leakage.

**Зависимости:** Task 1-2; существующие `Details.cshtml.cs`, `Details.cshtml`, `AiAnalysisResultMetadata`.

**Входит:**

- загрузка сохраненной metadata результата в `DetailsModel`;
- display model для:
  - `AnalysisMode`;
  - engine name;
  - provider name;
  - adapter name/profile/workflow metadata, если они сохранены neutral model;
  - retrieved context state;
  - manual context forwarding flag, если сохранен;
  - warnings/limitations;
  - diagnostic snapshot only in sanitized neutral form, если он уже сохранен;
- neutral labels in `AnalysisUiText`;
- UI block рядом с preliminary AI result, отделенный от expert evaluation/conclusion;
- tests, что direct LLM result shows direct mode without retrieved context claim;
- tests, что external result shows external metadata and limitations.

**Не входит:**

- provider-specific raw payload rendering;
- Dify DTO/fields as UI model;
- export behavior changes;
- new persistence fields/migrations;
- retrieval context item table rendering.

**Ожидаемый diff:**

- update `src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml.cs`;
- update `src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml`;
- small label helpers in `AnalysisUiText`;
- page tests for metadata/warnings/limitations.

**Проверки:**

- `dotnet test`;
- targeted `AnalysisPagesTests`;
- manual diff review for `Dify`/DTO/provider payload references in UI.

**Критерии Done:**

- saved result metadata is visible to the user;
- warnings/limitations do not look like expert conclusion;
- direct LLM path remains understandable and does not show synthetic retrieved context;
- UI model remains neutral and stored-result based.

**Red flags для review:**

- UI renders full raw Dify response as stable result section;
- limitations are hidden inside generic diagnostics only;
- direct LLM is described as having retrieval basis;
- DetailsModel injects analysis engine, selector or adapter.

## Task 4. Отобразить retrieved context из saved result без provider payload leakage

**Цель:** добавить отдельный block на `Details` page для retrieved context, metadata-only state, partial state или unavailable limitation, используя только сохраненную neutral модель результата.

**Зависимости:** Task 3; сохраненные `RetrievedContextItem` и `RetrievedContextState`.

**Входит:**

- Details display model for `RetrievedContextItem`;
- отдельный UI section "Основание external AI/RAG результата" или близкая формулировка;
- rendering full/excerpt text when saved;
- rendering metadata-only item fields when full text is absent;
- rendering source title/id/fragment id/rank/score/completeness if present in neutral model;
- visible limitation for `Unavailable`, `MetadataOnly`, `Partial`;
- empty/not applicable state for `DirectLlm` without artificial retrieved context;
- tests for available, metadata-only, unavailable and partial retrieved context states;
- wording that retrieved context is preliminary AI/RAG basis, not expert decision.

**Не входит:**

- linking every `ImpactMapItem` to retrieved context unless already reliably saved and approved for this task;
- fetching missing retrieved context from provider;
- own retrieval/search;
- Dify-specific fields or raw response;
- export changes.

**Ожидаемый diff:**

- update `Details.cshtml.cs` display records;
- update `Details.cshtml` retrieved context section;
- page tests with saved result fixtures;
- no application adapter/provider changes.

**Проверки:**

- `dotnet test`;
- targeted page tests for all retrieved context states;
- manual review of UI wording around preliminary material and limitations.

**Критерии Done:**

- retrieved context is visibly separate from manual context;
- metadata-only and partial states are explicit;
- unavailable retrieved context is shown as limitation of reproducibility;
- UI does not leak provider DTO or payload.

**Red flags для review:**

- UI calls adapter/provider to fill missing context;
- metadata-only is displayed as full context;
- retrieved context is presented as expert rationale;
- manual context fragments are relabeled as retrieved context.

## Task 5. Проверить export/UI boundary над сохраненным result/context

**Цель:** зафиксировать, что Stage 6 UI/export review работает только с сохраненным результатом и не добавляет повторные calls to `IAiAnalysisEngine`, adapter/provider или Dify.

**Зависимости:** Task 4; existing `ExportArchitectureTests`, `Stage5ArchitectureRegressionTests`, export service tests.

**Входит:**

- architecture tests, что Razor Pages/PageModels do not reference:
  - `DifyExternalRagAdapter`;
  - Dify DTO/options;
  - `IExternalRagAdapter`;
  - provider payload classes;
  - `HttpClient` for external AI/RAG;
- architecture tests, что export services do not reference `IAiAnalysisEngine`, selector, adapters, Dify or provider clients;
- page tests, что export links remain download of saved result only;
- regression, что `DetailsModel` export handlers use only `IAnalysisMarkdownExportService` / `IAnalysisJsonExportService`;
- source-token checks for no endpoint/API key/workflow UI additions.

**Не входит:**

- changing export format unless a Stage 6 UI/export review finds a narrow bug in already saved result rendering;
- calling Dify/mock adapter from export;
- adding Playwright/E2E framework;
- network integration tests.

**Ожидаемый diff:**

- update/add architecture tests in `tests/RequirementImpactAssistant.Tests/Application`;
- targeted page/export tests;
- no production changes except narrow fixes identified by tests.

**Проверки:**

- `dotnet build`;
- `dotnet test`;
- `git diff --stat` before review/commit.

**Критерии Done:**

- UI/PageModels remain above application-level boundary;
- export remains saved-result only;
- Dify details remain inside adapter area and tests;
- DirectLlm dependencies are unchanged.

**Red flags для review:**

- architecture tests are weakened to allow adapter references in UI;
- export calls engine/adapter to enrich result;
- UI introduces endpoint/API key fields;
- Stage 6 silently starts own retrieval pipeline.

## Task 6. Закрыть Stage 6 summary

**Цель:** после последовательного выполнения и review Task 1-5 зафиксировать итог Stage 6, выполненные commits, проверки и сохраненные границы.

**Зависимости:** Task 1-5 completed, reviewed and committed individually.

**Входит:**

- new `docs/program/mvp1/stage-6-summary.md`;
- список реализованных Stage 6 tasks;
- список commits Stage 6;
- проверка, что DirectLlm default path сохранен;
- подтверждение, что UI выбирает mode через application service;
- подтверждение, что retrieved context отображается из saved result;
- подтверждение, что UI/export не вызывают engine/adapter/provider напрямую;
- финальные проверки `dotnet build`, `dotnet test`, `git diff --check`, `git status --short`.

**Не входит:**

- Stage 7 smoke-сценарий;
- final MVP-1 gate;
- новые production changes помимо документа summary;
- commit без отдельной команды пользователя.

**Ожидаемый diff:**

- only `docs/program/mvp1/stage-6-summary.md`.

**Проверки:**

- `git diff --stat`;
- `git diff --check`;
- `git status --short`;
- optional `dotnet test` result from final implementation task referenced in summary.

**Критерии Done:**

- Stage 6 documented as complete only after all implementation tasks passed review;
- summary preserves boundaries and known limitations;
- next stage/gate is not started automatically.

**Red flags для review:**

- summary claims Stage 7/final MVP-1 gate started;
- summary hides unresolved UI/export boundary risks;
- summary is committed together with production changes from another task.

## Итоговый критерий готовности Stage 6

Stage 6 считается готовой к переходу на следующий gate только если:

- все Stage 6 implementation tasks выполнены последовательно и прошли отдельные review;
- пользователь может выбрать `DirectLlm` или `ExternalRag` перед запуском анализа;
- `DirectLlm` остается default/fallback режимом;
- external mode запускается только через `IAnalysisExecutionService` / application-level selector;
- UI явно показывает availability/warnings/limitations external режима;
- saved result показывает mode, engine/provider/adapter metadata и warnings/limitations;
- saved retrieved context отображается отдельно от manual context;
- unavailable/metadata-only/partial retrieved context видны как ограничения воспроизводимости, а не как экспертное заключение;
- UI/PageModels не зависят напрямую от Dify adapter, Dify DTO, provider payload, endpoint/API key;
- export не вызывает `IAiAnalysisEngine`, adapter или provider;
- в diff Stage 6 нет own RAG/retrieval pipeline, embeddings, rerank, vector DB, agentic workflow, new secrets/config, migrations, MVP-0 changes или unrelated production changes.

## Открытые вопросы и риски для review breakdown

- Нужен ли отдельный application-level availability service для external mode, или Stage 6 достаточно показать configured/local availability через уже существующий execution outcome.
- Поддержана ли передача manual context во внешний AI/RAG-контур как user-selectable input на application boundary; если нет, checkbox не должен появляться в UI до отдельной task.
- Нужно ли Stage 6 включать UI/export review только через tests, или понадобится ручной browser smoke после реализации Razor changes.
- Stage 5 summary указывает следующий этап как отдельную общую/final MVP-1 проверку; перед implementation Stage 6 нужно явно подтвердить, что проектный gate разрешает именно Stage 6 по implementation plan.

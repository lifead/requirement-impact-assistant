# Task breakdown MVP-1. Stage 7

## Назначение

Документ фиксирует маленькие implementation tasks только для этапа 7 из `docs/program/mvp1/implementation-plan.md`: "Smoke-сценарий MVP-1".

Это именно breakdown Stage 7, а не начало реализации smoke tests, browser/E2E framework или production code. Реализация Stage 7 должна начаться только после отдельного review/approval этого breakdown. Каждая task должна проходить отдельный цикл:

```text
task review -> implementation -> code review -> commit -> next task
```

Цель Stage 7 - зафиксировать один полный проверяемый smoke-сценарий MVP-1 на подготовленных обезличенных данных: от создания или открытия анализа до экспертного заключения и Markdown/JSON export, с проверкой `DirectLlm`, `ExternalRag`, retrieved context state и сохраненных mode/engine/provider/adapter metadata.

## Основание

- `docs/program/mvp1/implementation-plan.md`
- `docs/program/mvp1/technical-design.md`
- `docs/program/mvp1/requirements-draft.md`
- `docs/program/mvp1/stage-6-summary.md`
- `docs/program/mvp1/task-breakdown-stage-6.md`

## Реальная точка старта

По итогам Stage 1-6 в MVP-1 уже должны быть доступны:

- выбор `AnalysisMode.DirectLlm` и `AnalysisMode.ExternalRag` через UI/PageModel boundary;
- запуск анализа через `IAnalysisExecutionService` и application-level selector;
- `DirectLlmAnalysisEngine` как default/old POST fallback;
- `ExternalRagAnalysisEngine` через mock adapter или optional configured Dify adapter;
- сохранение result metadata, provider/adapter metadata, warnings/limitations и retrieved context state;
- просмотр saved result metadata и saved retrieved context на `Details` page;
- Markdown/JSON export сохраненного результата без повторного вызова engine/adapter/provider;
- Stage 6 architecture regression checks для UI/PageModel/export boundaries.

Stage 7 не должна начинать Stage 8 как отдельный большой контур architecture/reproducibility tests. Внутри Stage 7 допустимы только те smoke regression checks, которые нужны для защиты самого полного smoke-сценария.

## Границы Stage 7

Входит:

- один обезличенный baseline smoke-сценарий MVP-1 с подготовленными исходными данными и manual context;
- создание нового анализа или открытие подготовленного анализа через существующие application/page boundaries;
- заполнение исходных данных и manual context;
- запуск `DirectLlm` smoke path;
- запуск `ExternalRag` smoke path через mock adapter или отдельно разрешенный configured Dify path без обязательного real network;
- просмотр impact map после каждого режима, если результат сохранен успешно;
- просмотр retrieved context или limitation для external режима;
- экспертная оценка и экспертное заключение как отдельный человеческий слой;
- Markdown export и JSON export сохраненного результата;
- проверка, что export содержит mode, engine/provider/adapter и retrieved context state;
- минимальные smoke regression checks, что сценарий воспроизводим без секретов и обязательной сети;
- Stage 7 summary после выполнения и review всех implementation tasks.

Не входит:

- load/performance testing;
- full production integration tests;
- обязательный Playwright/browser E2E framework, если сценарий можно покрыть через existing page/application tests;
- browser smoke как обязательная часть Stage 7 без отдельной task/decision;
- real corporate data;
- mandatory network call to Dify или DeepSeek;
- new RAG/retrieval pipeline, embeddings, rerank, vector DB, agentic workflow;
- new secrets/config committed;
- migrations;
- MVP-0 changes;
- UI expansion outside smoke needs;
- provider-specific UI, endpoint/API key/workflow fields;
- изменение export на повторный анализ или external enrichment;
- commit/push без отдельной команды пользователя.

## Task 1. Подготовить smoke fixture baseline на обезличенных данных

**Цель:** зафиксировать минимальный reproducible baseline для полного MVP-1 smoke-сценария, который можно использовать в application/page tests без реальных корпоративных данных, секретов и сети.

**Зависимости:** завершенная Stage 6; существующие тестовые helpers/fixtures для analysis pages, application services и export.

**Входит:**

- обезличенный проектный запрос для smoke-сценария;
- исходные данные анализа, достаточные для карты влияния;
- manual context, явно отделенный от retrieved context;
- ожидаемые признаки результата для smoke assertion:
  - analysis exists/opened;
  - input data persisted;
  - manual context persisted;
  - impact map visible or available in saved result;
  - expert evaluation/conclusion can be saved;
  - export can be generated;
- baseline для external mock path с retrieved context available или controlled limitation;
- neutral naming без реальных названий организаций, систем, сотрудников, документов, endpoint-ов и токенов;
- размещение fixture в существующем тестовом контуре, если он уже есть, без создания новой большой test infrastructure.

**Не входит:**

- production seed data;
- реальные корпоративные материалы;
- Dify/DeepSeek secrets или config;
- новый storage format;
- migrations;
- browser automation framework.

**Ожидаемый diff:**

- narrow test fixture/helper changes в существующей test structure;
- no production changes unless current smoke path cannot be exercised without a tiny approved helper.

**Проверки:**

- targeted tests for fixture construction;
- `git diff --check`;
- manual review, что baseline не содержит sensitive/corporate data.

**Критерии Done:**

- smoke baseline can be reused by later Stage 7 tasks;
- data is anonymized and deterministic;
- manual context and retrieved context remain distinct.

**Red flags для review:**

- fixture embeds real customer/project names;
- fixture requires network, API key, endpoint or local secret;
- baseline creates new product behavior instead of test data.

## Task 2. Зафиксировать DirectLlm smoke path через existing boundaries

**Цель:** проверить полный Direct LLM smoke path от анализа с обезличенными данными до сохраненного результата и impact map, не обходя `IAnalysisExecutionService`/page boundary.

**Зависимости:** Task 1; Stage 6 UI/PageModel boundary; existing direct LLM test/demo provider behavior.

**Входит:**

- создание или открытие анализа из smoke baseline;
- заполнение или проверка исходных данных и manual context;
- запуск `DirectLlm` через existing application/page boundary;
- проверка, что `DirectLlm` остается default/fallback where applicable;
- проверка saved result metadata:
  - mode is `DirectLlm`;
  - engine is direct LLM engine;
  - provider metadata is present only in approved neutral form, если применимо;
  - retrieved context is not artificially created;
- проверка, что impact map доступна для просмотра/экспорта;
- targeted application/page tests using existing project patterns.

**Не входит:**

- реальный DeepSeek/network call as mandatory smoke dependency;
- изменение direct LLM prompt/provider behavior;
- export assertions beyond basic saved-result availability, если они покрываются Task 4;
- UI expansion.

**Ожидаемый diff:**

- targeted smoke/page/application tests for DirectLlm path;
- small fixture reuse from Task 1;
- no provider/network configuration changes.

**Проверки:**

- targeted DirectLlm smoke tests;
- `dotnet test`;
- manual diff review, что test path не вызывает provider напрямую из UI/export.

**Критерии Done:**

- DirectLlm smoke path passes without secrets and mandatory network;
- saved result has correct mode/engine metadata;
- no retrieved context is invented for direct mode.

**Red flags для review:**

- DirectLlm smoke requires real DeepSeek;
- test bypasses application/page boundary and calls engine in a way that does not match user workflow;
- direct result is marked as external/retrieved.

## Task 3. Зафиксировать ExternalRag smoke path через mock/configured fake path

**Цель:** проверить external AI/RAG smoke path через mock adapter или separately approved configured Dify path без обязательной сети, с явным retrieved context или limitation.

**Зависимости:** Task 1; Stage 4-6 external adapter/mock/UI result display.

**Входит:**

- запуск `ExternalRag` через existing application/page boundary;
- использование `MockExternalRagAdapter` или configured fake/local path, который не требует network/secrets;
- optional configured Dify path только если отдельно разрешен для manual check и не становится обязательным для tests;
- проверка saved result metadata:
  - mode is `ExternalRag`;
  - engine is external RAG engine;
  - provider/adapter metadata stored in neutral form;
  - retrieved context state is `Available`, `MetadataOnly`, `Partial` или `Unavailable`;
  - warnings/limitations visible when context is partial/unavailable;
- проверка, что retrieved context или limitation отображается отдельно от manual context;
- проверка, что impact map можно просмотреть, если external result successful/usable.

**Не входит:**

- real Dify network call as mandatory test;
- Dify-specific DTO/payload assertions as stable public model;
- own retrieval pipeline;
- embeddings, rerank, vector DB, agentic workflow;
- provider-specific UI/config fields.

**Ожидаемый diff:**

- targeted smoke/page/application tests for ExternalRag mock path;
- possible test fixture additions for retrieved context states;
- no committed secrets/config;
- no migrations or provider UI changes.

**Проверки:**

- targeted ExternalRag smoke tests;
- `dotnet test`;
- manual review, что absence of Dify config does not fail smoke tests.

**Критерии Done:**

- ExternalRag smoke path is reproducible without secrets and mandatory network;
- retrieved context state is saved and visible;
- limitation is explicit when retrieved context is unavailable/partial.

**Red flags для review:**

- tests require Dify endpoint/API key;
- UI/page test reaches into Dify adapter or DTO directly;
- retrieved context is mixed with manual context;
- task adds a retrieval pipeline.

## Task 4. Проверить экспертную оценку, заключение и Markdown/JSON export

**Цель:** завершить smoke-сценарий человеческим экспертным слоем и проверить, что Markdown/JSON export сохраненного результата содержит обязательные MVP-1 metadata.

**Зависимости:** Task 2-3; existing expert evaluation/conclusion and export services.

**Входит:**

- сохранение экспертной оценки по smoke result;
- сохранение экспертного заключения как отдельного человеческого слоя;
- Markdown export saved result;
- JSON export saved result;
- проверки export для DirectLlm и ExternalRag smoke variants where practical;
- assertions, что export содержит:
  - analysis mode;
  - engine;
  - provider/adapter, если применимо;
  - manual context information;
  - retrieved context state;
  - retrieved context или limitation для external режима;
  - expert evaluation;
  - expert conclusion;
- проверка, что export не вызывает `IAiAnalysisEngine`, external adapter или provider.

**Не входит:**

- PDF export;
- новая JSON schema как отдельный публичный контракт;
- повторный анализ при export;
- export enrichment из Dify/mock adapter;
- изменение экспертной оценки в автоматическое решение.

**Ожидаемый diff:**

- targeted export smoke tests;
- possible fixture reuse from Task 1-3;
- no production changes except narrow fixes if current export violates approved MVP-1 requirements.

**Проверки:**

- targeted export tests;
- `dotnet test`;
- manual diff review for export boundary.

**Критерии Done:**

- expert evaluation/conclusion survive through saved result and export;
- Markdown and JSON include mode, engine/provider/adapter and retrieved context state;
- export remains saved-result only.

**Red flags для review:**

- export calls engine/provider/adapter;
- expert conclusion is generated or overwritten by AI/RAG/LLM;
- provider raw payload becomes required stable export format.

## Task 5. Добавить smoke regression guard и финальные проверки Stage 7

**Цель:** закрепить, что полный smoke-сценарий MVP-1 воспроизводим в стандартном test/build контуре и не размывает архитектурные границы, не превращая Stage 7 в Stage 8 full architecture suite.

**Зависимости:** Task 1-4 completed and reviewed.

**Входит:**

- minimal regression checks, что smoke tests do not require network or secrets;
- checks, что absence of configured Dify does not break default test run;
- checks, что UI/page/export smoke path still uses application services and saved result exports;
- targeted source-token/architecture guard only for surfaces touched by Stage 7, if existing tests do not already cover them;
- final `dotnet build`;
- final `dotnet test`;
- `git diff --check`;
- `git status --short`;
- фиксация результатов проверок в ответе/review material.

**Не входит:**

- полный Stage 8 architecture/reproducibility suite;
- load/performance tests;
- production integration tests;
- Playwright/browser E2E framework by default;
- mandatory browser smoke без отдельной task/decision;
- mandatory real Dify/DeepSeek call.

**Ожидаемый diff:**

- targeted smoke regression tests or narrow updates to existing architecture tests;
- no new browser framework unless separately approved;
- no production changes except narrow smoke fixes approved in review.

**Проверки:**

- `dotnet build RequirementImpactAssistant.sln`;
- `dotnet test RequirementImpactAssistant.sln`;
- `git diff --check`;
- `git status --short`.

**Критерии Done:**

- one full MVP-1 smoke scenario is covered end-to-end in project tests or explicitly documented manual/browser smoke task;
- default checks pass without external secrets/network;
- smoke guard does not weaken existing architecture tests.

**Red flags для review:**

- Stage 7 silently introduces broad E2E framework;
- architecture tests are weakened;
- test suite starts requiring real external providers;
- Stage 7 duplicates or replaces Stage 8.

## Task 6. Закрыть Stage 7 summary

**Цель:** после последовательного выполнения и review Task 1-5 зафиксировать итог Stage 7, выполненные commits, проверки, smoke coverage и сохраненные границы.

**Зависимости:** Task 1-5 completed, reviewed and committed individually.

**Входит:**

- new `docs/program/mvp1/stage-7-summary.md`;
- список реализованных Stage 7 tasks;
- список commits Stage 7;
- описание smoke baseline на обезличенных данных;
- подтверждение DirectLlm smoke path;
- подтверждение ExternalRag smoke path через mock/configured fake path;
- подтверждение retrieved context/limitation display;
- подтверждение expert evaluation/conclusion and Markdown/JSON export checks;
- подтверждение, что export содержит mode, engine/provider/adapter и retrieved context state;
- финальные проверки `dotnet build`, `dotnet test`, `git diff --check`, `git status --short`;
- открытые ограничения, если browser smoke или optional Dify manual check вынесены отдельно.

**Не входит:**

- Stage 8 implementation;
- final production integration gate;
- новые production changes помимо summary;
- commit без отдельной команды пользователя.

**Ожидаемый diff:**

- only `docs/program/mvp1/stage-7-summary.md`.

**Проверки:**

- `git diff --stat`;
- `git diff --check`;
- `git status --short`;
- optional reference to final `dotnet build` / `dotnet test` results from Task 5.

**Критерии Done:**

- Stage 7 documented as complete only after all implementation tasks passed review;
- summary preserves no-network/no-secrets reproducibility boundary;
- Stage 8 is not started automatically.

**Red flags для review:**

- summary claims Stage 8/full architecture suite is complete;
- summary hides mandatory external provider dependency;
- summary is committed together with unrelated production changes.

## Итоговый критерий готовности Stage 7

Stage 7 считается готовой к переходу на следующий gate только если:

- все Stage 7 implementation tasks выполнены последовательно и прошли отдельные review;
- один полный MVP-1 smoke-сценарий проходит на обезличенных данных;
- сценарий покрывает создание/открытие анализа, исходные данные и manual context;
- `DirectLlm` smoke path проходит через existing application/page boundaries;
- `ExternalRag` smoke path проходит через mock adapter или отдельно разрешенный configured fake/Dify path без mandatory network;
- impact map доступна для просмотра после успешного результата;
- retrieved context или limitation видны и отделены от manual context;
- экспертная оценка и экспертное заключение сохранены как человеческий слой;
- Markdown export и JSON export формируются из saved result;
- export содержит mode, engine/provider/adapter и retrieved context state;
- default checks pass without real corporate data, committed secrets, Dify/DeepSeek mandatory calls, migrations, MVP-0 changes, own RAG/retrieval pipeline, embeddings, rerank, vector DB или agentic workflow;
- если browser smoke нужен, он вынесен в отдельную task/decision и не подменяет existing page/application tests.

## Открытые вопросы и риски для review breakdown

- Достаточно ли existing page/application tests для полного smoke-сценария, или нужен отдельный manual/browser smoke task после реализации.
- Нужно ли проверять оба режима в одном saved analysis или в двух отдельных smoke runs для ясного сравнения export и metadata.
- Какой retrieved context state должен быть baseline для mock external path: `Available` как happy path или `Unavailable/Partial` как проверка limitation.
- Нужна ли optional manual Dify smoke-проверка в Stage 7, или configured Dify path лучше оставить вне обязательного smoke gate.

# Task breakdown MVP-1. Stage 5

## Назначение

Документ фиксирует маленькие implementation tasks только для этапа 5 из `docs/program/mvp1/implementation-plan.md`: "Минимальный Dify adapter".

Цель Stage 5 - добавить Dify external RAG adapter за neutral `IExternalRagAdapter` boundary, не протаскивая Dify-specific поля в domain/UI/stable export и не превращая приложение в собственный RAG.

Stage 5 должна использовать уже созданные Stage 1-4 элементы:

- `IExternalRagAdapter` и neutral request/response models в `src/RequirementImpactAssistant.Web/Application/Analysis/External`;
- `ExternalRagAnalysisEngine` как единственную application-level точку обращения к external adapter;
- `MockExternalRagAdapter` как воспроизводимый локальный adapter по умолчанию;
- `IAiAnalysisEngineSelector` и `AnalysisExecutionService.RunAsync(..., AnalysisMode.ExternalRag)`;
- сохранение `AiAnalysisResultMetadata` и `RetrievedContextItem`;
- уже расширенный Markdown/JSON export сохраненных metadata и retrieved context.

Этот breakdown не разрешает реализацию автоматически. Каждая task должна проходить отдельный цикл:

```text
task review -> implementation -> code review -> commit -> next task
```

## Реальная точка старта

По текущей структуре проекта Stage 4 уже добавила:

- deterministic `MockExternalRagAdapter` в `src/RequirementImpactAssistant.Web/Application/Analysis/External`;
- регистрацию `IExternalRagAdapter -> MockExternalRagAdapter` через `TryAddScoped` в `src/RequirementImpactAssistant.Web/Extensions/ServiceCollectionExtensions.cs`;
- local external mode без Dify, сети, секретов и provider configuration;
- tests для mock adapter scenarios, `ExternalRagAnalysisEngine`, `AnalysisExecutionService`, export already-saved data;
- `tests/RequirementImpactAssistant.Tests/Application/Stage4ArchitectureRegressionTests.cs`, который сейчас ожидает единственную production implementation `IExternalRagAdapter` - `MockExternalRagAdapter` - и запрещает Dify/HTTP/provider configuration в Stage 4 boundary.

На старте Stage 5 Dify adapter отсутствует. UI выбора режима еще не реализован и остается scope Stage 6. Export уже должен работать по сохраненной neutral модели и не должен вызывать adapter/provider.

## Границы Stage 5

Входит:

- neutral options/config binding для Dify adapter без секретов в репозитории;
- минимальный `DifyExternalRagAdapter`, реализующий `IExternalRagAdapter`;
- HTTP client abstraction / typed client по существующему проектному паттерну, если используется `HttpClient`;
- Dify request/response DTO и mapping только внутри Dify adapter area;
- mapping Dify response в `ExternalRagAdapterResponse`, `ImpactMap`, provider metadata, sanitized diagnostic snapshot и `RetrievedContextItem`;
- controlled unavailable/failure behavior при отсутствующей или неполной конфигурации;
- tests через fake HTTP/message handler или эквивалент без реальной сети;
- DI wiring behind configuration, при котором direct LLM и local mock остаются рабочими defaults;
- architecture regression checks, запрещающие Dify leakage в domain/UI/export/direct LLM.

Не входит:

- UI/Razor production changes;
- собственный RAG/retrieval pipeline, embeddings, rerank, vector database, retrieval trace или agentic workflow;
- provider secrets в репозитории;
- hardcoded API keys/endpoints;
- изменения MVP-0 docs;
- migrations/persistence, если нет строго документированной необходимости;
- export changes, кроме compile-fix/tests уже сохраненных neutral данных;
- вызов engine/adapter/provider из export;
- dependency `DirectLlmAnalysisEngine` на Dify/external adapter;
- управление Dify knowledge base из приложения;
- реальные сетевые tests по умолчанию;
- dashboard, PDF, workflow platform expansion.

## Task 1. Добавить Dify options/config boundary без секретов

**Цель:** подготовить optional configuration surface для Dify adapter так, чтобы отсутствие endpoint/workflow/app id/API key считалось штатной недоступностью Dify, а не ошибкой direct LLM или mock режима.

**Зависимости:** завершенная Stage 4; существующие `ServiceCollectionExtensions`, `ApplicationConfigurationTests`, паттерн options для `AiAnalysisOptions`/`DeepSeekLlmProviderOptions`.

**Входит:**

- options class для Dify external adapter в `src/RequirementImpactAssistant.Web/Application/Analysis/External` или вложенной `Dify` области;
- нейтральное правило `Enabled`/`IsConfigured` или аналог, не требующее секретов для запуска tests;
- поля configuration только для минимального adapter:
  - base address / endpoint;
  - workflow/app identifier, если нужен выбранный Dify режим;
  - API key как значение, приходящее из окружения/user secrets, но не из committed config;
  - timeout/profile name, если нужно для controlled behavior;
- validation helper, который возвращает unavailable/disabled state без выбрасывания runtime exception для default mock/direct сценариев;
- tests на missing/disabled/partially configured options;
- documentation comments only where needed to prevent accidental committed secrets.

**Не входит:**

- `DifyExternalRagAdapter`;
- HTTP calls, DTO или response mapping;
- DI switch на Dify как default adapter;
- appsettings с реальными endpoint/key/workflow id;
- user secrets values, environment-specific secrets, migrations, UI.

**Ожидаемый diff:**

- 1-2 small production files для options/config validation;
- tests в `tests/RequirementImpactAssistant.Tests/Configuration` или `tests/RequirementImpactAssistant.Tests/Application`;
- возможно точечная подготовка `ServiceCollectionExtensions` без активации Dify по умолчанию;
- без изменений Razor Pages, export services, domain model, migrations и MVP-0 docs.

**Проверки:**

- `dotnet test`;
- targeted configuration/options tests;
- manual diff review на отсутствие committed endpoint/API key/user secrets.

**Критерии Done:**

- Dify configuration может быть полностью отсутствующей без падения build/test;
- API key/secret не имеет default value и не появляется в committed files;
- options не протаскивают Dify-specific fields в domain/UI/export models;
- direct LLM и mock external mode не зависят от Dify options.

**Red flags для review:**

- реальные endpoint/key/workflow id добавлены в `appsettings*.json`;
- отсутствие Dify configuration ломает `AddApplicationAnalysis`;
- options становятся частью domain model или stable JSON export;
- task одновременно добавляет HTTP adapter или UI настройки.

## Task 2. Реализовать internal Dify request/response mapping happy path

**Цель:** добавить минимальный `DifyExternalRagAdapter`, который принимает neutral `ExternalRagAdapterRequest`, формирует internal Dify payload, обрабатывает успешный Dify-shaped response и возвращает neutral `ExternalRagAdapterResponse`.

**Зависимости:** Task 1; существующие `IExternalRagAdapter`, `ExternalRagAdapterRequest`, `ExternalRagAdapterResponse`, `ImpactMap`, `RetrievedContextItem`.

**Входит:**

- `DifyExternalRagAdapter` в отдельной Dify-specific области под `Application/Analysis/External`, например `External/Dify`;
- internal DTO/mapper classes, не используемые domain/UI/export;
- HTTP client abstraction или typed `HttpClient`, если выбран текущий проектный паттерн;
- mapping neutral request:
  - analysis input snapshot;
  - optional manual context as separate block;
  - expected result/boundary notice where useful;
  - no secrets inside request body/logged diagnostics;
- happy path mapping response:
  - `ExternalRagAdapterResponseStatus.Completed`;
  - normalized `ImpactMap`;
  - provider metadata with provider/adapter/workflow/profile names;
  - `RetrievedContextState.Available` when full/excerpt context returned;
  - `RetrievedContextItem` collection;
  - sanitized diagnostic snapshot without raw secret-bearing payload;
- unit tests using fake `HttpMessageHandler` or equivalent no-real-network technique.

**Не входит:**

- DI activation behind configuration;
- real Dify integration/manual test with network;
- retry policy, streaming, file upload, knowledge base management;
- support for every possible Dify response shape;
- UI/export/persistence changes;
- own retrieval, embeddings, rerank, vector DB.

**Ожидаемый diff:**

- new Dify adapter/DTO/mapper files under `src/RequirementImpactAssistant.Web/Application/Analysis/External/Dify`;
- tests such as `DifyExternalRagAdapterTests.cs` with fake HTTP handler;
- narrow update to Stage 4 architecture check only if needed to acknowledge Stage 5 will add one Dify adapter implementation;
- no Razor Pages/appsettings secrets/migrations/export semantics changes.

**Проверки:**

- `dotnet test`;
- targeted Dify adapter happy path tests;
- review source tokens: Dify names stay inside Dify adapter/tests and do not appear in domain/UI/export.

**Критерии Done:**

- adapter implements only `IExternalRagAdapter`;
- test proves request is sent through fake HTTP without network;
- Dify-specific DTO are internal to adapter area;
- neutral response contains `ImpactMap`, metadata and retrieved context using existing Stage 1-3 models;
- sanitized diagnostic snapshot does not contain API key, authorization header, full raw response as stable contract, personal data or sensitive content.

**Red flags для review:**

- Dify DTO leak into `Domain`, Razor Pages, export DTO or stable JSON export;
- adapter hardcodes endpoint/API key/workflow id;
- tests require network, real Dify account or secrets;
- adapter creates embeddings/rerank/retrieval logic inside application.

## Task 3. Покрыть Dify unavailable, incomplete, timeout and error states

**Цель:** обеспечить controlled behavior для отсутствующей конфигурации, timeout, provider error, malformed/incomplete response и отсутствующего retrieved context.

**Зависимости:** Task 2.

**Входит:**

- unavailable response when Dify options are disabled or incomplete;
- timeout/cancellation handling без раскрытия endpoint/key/token;
- mapping provider error into neutral `ExternalRagAdapterError`;
- handling response without structured result as `Failed`;
- handling structured result without retrieved context as `RetrievedContextState.Unavailable` with warning/limitation;
- handling metadata-only/partial retrieved context as `MetadataOnly`/`Partial`;
- tests with fake HTTP responses for each state.

**Не входит:**

- real retry/backoff framework unless already required by project pattern;
- production network integration test;
- UI limitation display;
- export format changes;
- persistence/migration;
- mapping provider-specific raw error body into public/stable data.

**Ожидаемый diff:**

- extension of Dify adapter mapping/error handling;
- focused tests in `DifyExternalRagAdapterTests.cs`;
- maybe small helper for sanitized diagnostics;
- no changes outside Dify adapter area except tests.

**Проверки:**

- `dotnet test`;
- targeted tests for unavailable/metadata-only/partial/failed/timeout states;
- review, что error messages are sanitized.

**Критерии Done:**

- missing/disabled Dify config returns neutral unavailable/failed response, not unhandled exception;
- incomplete retrieved context is explicit in state/warnings;
- provider errors do not expose secrets, authorization headers or raw payload as stable model;
- partial result with usable `ImpactMap` can be returned as partial with warnings.

**Red flags для review:**

- timeout/provider error bubbles as raw exception into UI/application service;
- incomplete retrieved context is silently marked available;
- raw provider response is saved or exported as required public structure;
- adapter starts compensating by doing its own retrieval/search.

## Task 4. Подключить Dify adapter behind configuration, сохранив mock/direct defaults

**Цель:** добавить DI wiring, при котором Dify adapter используется только при явной корректной конфигурации, а default local external mode через mock и direct LLM остаются рабочими без Dify.

**Зависимости:** Task 1, Task 2, Task 3; существующая регистрация `IExternalRagAdapter -> MockExternalRagAdapter` в `ServiceCollectionExtensions`.

**Входит:**

- conditional registration, например:
  - no Dify config -> `MockExternalRagAdapter` remains local default;
  - Dify enabled and configured -> `DifyExternalRagAdapter` registered as `IExternalRagAdapter`;
- typed `HttpClient` registration for Dify adapter if needed;
- configuration tests for disabled/missing/configured states;
- regression, что `RunAsync(id)` default remains `DirectLlm`;
- regression, что `RunAsync(id, AnalysisMode.ExternalRag)` remains available locally without Dify via mock adapter.

**Не входит:**

- UI switch between mock and Dify;
- appsettings secrets/endpoints committed to repo;
- real Dify network call;
- changing LLM provider selection;
- changing export or persistence schema.

**Ожидаемый diff:**

- targeted changes in `src/RequirementImpactAssistant.Web/Extensions/ServiceCollectionExtensions.cs`;
- tests in `ApplicationConfigurationTests.cs` and/or focused DI tests;
- possible small test-only configuration factory;
- no Razor Pages, migrations, MVP-0 docs or export behavior changes.

**Проверки:**

- `dotnet test`;
- DI/configuration tests;
- manual review that no committed file contains real Dify endpoint/API key.

**Критерии Done:**

- application starts/tests pass without Dify configuration;
- configured Dify path can resolve `DifyExternalRagAdapter`;
- unconfigured path still resolves `MockExternalRagAdapter`;
- direct LLM default does not depend on Dify availability;
- no production code hardcodes secrets or provider endpoint.

**Red flags для review:**

- Dify becomes mandatory for `AddApplicationAnalysis`;
- mock adapter is removed before Stage 6/smoke workflow can use local mode;
- `DirectLlmAnalysisEngine` gains dependency on Dify/external adapter;
- configuration uses committed fake key that looks like a real secret.

## Task 5. Проверить execution path через configured Dify adapter на fake HTTP

**Цель:** подтвердить, что configured Dify adapter проходит через `ExternalRagAnalysisEngine` и `AnalysisExecutionService` на application level, сохраняет только neutral metadata/retrieved context и не требует UI, сети или секретов.

**Зависимости:** Task 4; существующие `AnalysisExecutionServiceTests`, `ApplicationDbContext` mappings, fake HTTP test infrastructure from Task 2-3.

**Входит:**

- service-level tests for `AnalysisExecutionService.RunAsync(analysisId, AnalysisMode.ExternalRag, ...)` with configured Dify adapter and fake HTTP response;
- checks saved result:
  - `AnalysisMode.ExternalRag`;
  - `EngineName = ExternalRagAnalysisEngine`;
  - provider/adapter metadata identifies Dify only as provider/adapter metadata;
  - retrieved context state/items are neutral `RetrievedContextItem`;
  - warnings/limitations are saved for partial/unavailable states;
  - raw/sanitized diagnostics do not include secrets;
- regression, что export of already saved data still does not call adapter/provider;
- regression, что mock/default external mode still works when Dify is not configured.

**Не входит:**

- real Dify call;
- UI/Razor changes;
- new export fields or JSON schema changes;
- migration/persistence change unless strictly necessary and separately documented;
- expert evaluation/conclusion changes.

**Ожидаемый diff:**

- service-level tests in `AnalysisExecutionServiceTests.cs` or new focused test file;
- possible test fixture for fake HTTP and configured service provider;
- production changes only if narrow DI/mapping compile-fix is required;
- no appsettings secrets, migrations, UI or export service behavior changes.

**Проверки:**

- `dotnet test`;
- targeted service-level tests;
- review, что tests do not require network/user secrets.

**Критерии Done:**

- external mode can use configured Dify adapter through application-level `IAiAnalysisEngine` path;
- persisted result remains neutral and comparable with direct/mock external results;
- Dify-specific DTO/payload are not stored in domain model or stable export;
- export remains read-only over saved result and never calls Dify/adapter/engine.

**Red flags для review:**

- tests bypass `AnalysisExecutionService` and only test mapper internals;
- persistence stores Dify request/response DTO as public model;
- export calls `IExternalRagAdapter` to enrich result;
- service-level tests require real Dify config.

## Task 6. Закрыть architecture regression checks Stage 5

**Цель:** зафиксировать, что Stage 5 добавила optional Dify adapter только за neutral boundary и не протащила Dify, HTTP, secrets, UI, export calls или собственный RAG в запрещенные слои.

**Зависимости:** Task 5.

**Входит:**

- update Stage 4 architecture checks so production `IExternalRagAdapter` implementations are exactly:
  - `MockExternalRagAdapter`;
  - `DifyExternalRagAdapter`;
- architecture tests, что Dify-specific types live only under Dify adapter area and tests;
- architecture tests, что domain model, Razor Pages/PageModels, export services and stable export tests do not reference Dify DTO/options/adapter directly;
- architecture tests, что `DirectLlmAnalysisEngine` does not reference Dify/external adapter boundary;
- source-token checks that secrets/hardcoded keys/endpoints are absent from committed source/config;
- source-token checks that Stage 5 production code does not add embeddings/rerank/vector DB/retrieval pipeline/agentic workflow.

**Не входит:**

- Stage 6 UI smoke;
- Playwright/browser E2E;
- real Dify integration test by default;
- new Stage 5 summary without separate decision;
- MVP-0 docs changes.

**Ожидаемый diff:**

- changes in `Stage4ArchitectureRegressionTests.cs` or new `Stage5ArchitectureRegressionTests.cs`;
- possibly focused additions to `ExportArchitectureTests.cs`;
- no production behavior changes except corrections from previous tasks.

**Проверки:**

- `dotnet build`;
- `dotnet test`;
- `git diff --stat` before review/commit;
- manual diff review for out-of-scope files.

**Критерии Done:**

- tests pass without Dify, network, user secrets and external keys;
- Dify adapter remains optional integration layer behind `IExternalRagAdapter`;
- UI/domain/export/direct LLM do not depend on Dify-specific types;
- stable export contains only neutral saved metadata/retrieved context/result blocks;
- diff Stage 5 contains no UI production changes, own RAG/embeddings/rerank/vector DB, hardcoded secrets/endpoints, migrations without documented necessity, MVP-0 docs changes, dashboard/PDF/workflow platform work.

**Red flags для review:**

- architecture tests are weakened so Dify can leak into UI/export/domain;
- checks allow hardcoded endpoint/key because "test only";
- Dify adapter becomes mandatory provider for external mode;
- source-token checks forbid Dify everywhere and therefore cannot distinguish allowed adapter area from leakage.

## Итоговый критерий готовности Stage 5

Stage 5 считается готовым к переходу на следующий этап только если:

- все 6 tasks реализованы последовательно и прошли отдельные review;
- Dify adapter реализует `IExternalRagAdapter` и остается optional;
- missing Dify configuration does not break direct LLM and local mock external mode;
- all default tests pass without Dify, network, user secrets and external keys;
- Dify request/response DTO stay inside Dify adapter area;
- saved results and Markdown/JSON export use neutral metadata/retrieved context/result blocks only;
- `DirectLlmAnalysisEngine`, UI/Razor Pages, domain model and export do not depend on Dify adapter;
- full raw Dify response is not saved by default and is not stable public model;
- real Dify verification, if needed, remains explicitly enabled manual/integration check outside default test path.

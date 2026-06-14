# Task breakdown MVP-1. Stage 3

## Назначение

Документ фиксирует маленькие implementation tasks только для этапа 3 из `docs/program/mvp1/implementation-plan.md`: "Neutral external RAG adapter contract".

Цель Stage 3 - добавить нейтральную границу внешнего AI/RAG adapter и подготовить `ExternalRagAnalysisEngine` как реализацию `IAiAnalysisEngine`, не подключая mock adapter, Dify adapter, реальные внешние вызовы, сеть, секреты или UI.

Stage 3 должна создать только contract/orchestration boundary:

- neutral request/response models для external AI/RAG adapter;
- interface external AI/RAG adapter;
- `ExternalRagAnalysisEngine` как application-level orchestration layer за `IAiAnalysisEngine`;
- controlled unavailable/failure behavior для external mode без configured adapter;
- selector/registry для выбора `IAiAnalysisEngine` по `AnalysisMode`, если это нужно текущей архитектуре;
- application-level и architecture tests для новых границ.

Этот breakdown не разрешает реализацию автоматически. Каждая task должна проходить отдельный цикл:

```text
task review -> implementation -> code review -> commit -> next task
```

## Реальная точка старта

По текущей структуре проекта Stage 1 и Stage 2 уже добавили:

- `AnalysisMode` в `src/RequirementImpactAssistant.Web/Domain/Enums/AnalysisMode.cs`;
- `RetrievedContextState` в `src/RequirementImpactAssistant.Web/Domain/Enums/RetrievedContextState.cs`;
- `RetrievedContextItemCompleteness` в `src/RequirementImpactAssistant.Web/Domain/Enums/RetrievedContextItemCompleteness.cs`;
- `AiAnalysisResultMetadata` в `src/RequirementImpactAssistant.Web/Domain/AiAnalysisResultMetadata.cs`;
- `RetrievedContextItem` в `src/RequirementImpactAssistant.Web/Domain/RetrievedContextItem.cs`;
- persistence для metadata и `RetrievedContextItems` в `src/RequirementImpactAssistant.Web/Data/ApplicationDbContext.cs`;
- Markdown/JSON export сохраненных metadata и retrieved context в `src/RequirementImpactAssistant.Web/Application/Export`;
- export architecture tests в `tests/RequirementImpactAssistant.Tests/Application/ExportArchitectureTests.cs`.

Текущий analysis boundary находится в:

- `src/RequirementImpactAssistant.Web/Application/Analysis/IAiAnalysisEngine.cs`;
- `src/RequirementImpactAssistant.Web/Application/Analysis/DirectLlmAnalysisEngine.cs`;
- `src/RequirementImpactAssistant.Web/Application/Analysis/AiAnalysisRequest.cs`;
- `src/RequirementImpactAssistant.Web/Application/Analysis/AiAnalysisResponse.cs`;
- `src/RequirementImpactAssistant.Web/Application/Analysis/AnalysisExecutionService.cs`;
- `src/RequirementImpactAssistant.Web/Extensions/ServiceCollectionExtensions.cs`;
- `tests/RequirementImpactAssistant.Tests/Application/AnalysisExecutionServiceTests.cs`;
- `tests/RequirementImpactAssistant.Tests/Configuration/ApplicationConfigurationTests.cs`.

На старте Stage 3 `IAiAnalysisEngine` зарегистрирован как единственная реализация `DirectLlmAnalysisEngine`. `AnalysisExecutionService` получает один `IAiAnalysisEngine`, всегда сохраняет metadata как direct LLM и не имеет selector/registry по `AnalysisMode`. `AiAnalysisResponse` возвращает status, `ImpactMap`, raw response, errors и boundary notice, но не несет external adapter metadata/retrieved context, необходимую для сохранения результата external AI/RAG режима.

## Границы Stage 3

Входит:

- neutral request/response models для external AI/RAG adapter;
- neutral status/diagnostics vocabulary external adapter response без привязки к Dify;
- interface external AI/RAG adapter на application/infrastructure boundary;
- минимальное расширение internal response/metadata bridge, если оно нужно, чтобы `ExternalRagAnalysisEngine` мог вернуть metadata и retrieved context в `AnalysisExecutionService`;
- `ExternalRagAnalysisEngine` как orchestration layer, который использует neutral adapter contract;
- controlled unavailable/failed response, если external mode выбран, но production adapter не настроен;
- selector/registry `IAiAnalysisEngine` по `AnalysisMode`, если текущий single-engine registration мешает подключить второй engine;
- tests на application-level behavior и architectural boundaries.

Не входит:

- UI выбора режима или отображения external mode;
- mock external RAG adapter как production/testable feature Stage 4;
- Dify adapter, Dify endpoint, Dify API key, Dify configuration или user secrets;
- real external calls, network access и integration tests с внешними сервисами;
- собственный RAG, embeddings, rerank, vector database, retrieval pipeline внутри приложения или agentic search;
- fake/mock retrieved context generation inside `ExternalRagAnalysisEngine`;
- новые EF migrations или persistence changes, кроме минимальной необходимости компиляции уже существующей Stage 1 модели;
- export changes, кроме минимальной необходимости компиляции при изменении internal response contract;
- изменение MVP-0 документов;
- изменение direct LLM behavior, prompt или provider call;
- изменение экспертной оценки, экспертного заключения, PDF export, dashboard или workflow.

## Task 1. Добавить neutral external AI/RAG request и response models

**Цель:** зафиксировать нейтральную форму обмена между `ExternalRagAnalysisEngine` и внешним adapter, не привязанную к Dify, endpoint-ам, SDK, raw provider payload или внутреннему retrieval pipeline.

**Зависимости:** завершенные Stage 1 и Stage 2; существующие `AiAnalysisRequest`, `AnalysisInputSnapshot`, `ExpectedAnalysisResultStructure`, `RetrievedContextState`, `RetrievedContextItem`.

**Входит:**

- новые модели в application-level области, например `src/RequirementImpactAssistant.Web/Application/Analysis/External/`;
- request model для external adapter, включающая:
  - analysis id или correlation id;
  - исходные данные из `AnalysisInputSnapshot`;
  - optional manual context block как отдельную часть запроса;
  - признак, можно ли передавать manual context во внешний контур;
  - `ExpectedAnalysisResultStructure`;
  - `AnalysisBoundaryNotice`;
  - execution metadata без секретов;
- response model external adapter, включающая:
  - neutral status: completed, completedWithWarnings, partial, failed или близкий согласованный набор;
  - structured result или normalized `ImpactMap`, если adapter уже вернул пригодный результат;
  - provider/adapter/model/workflow/profile metadata без секретов;
  - `RetrievedContextState`;
  - `RetrievedContextItem` collection или пустое представление;
  - warnings;
  - error information без раскрытия секретов;
  - sanitized diagnostic snapshot или metadata response как optional string/value, без требования сохранять полный raw response;
- tests на нейтральность моделей: metadata-only, unavailable и partial response допустимы без текста retrieved context.

**Не входит:**

- `IExternalRagAdapter`;
- `ExternalRagAnalysisEngine`;
- adapter implementation, mock adapter или Dify adapter;
- HTTP/client/network код;
- configuration, secrets, endpoint settings;
- EF migration, UI или export.

**Ожидаемый diff:**

- несколько небольших файлов моделей в `Application/Analysis/External`;
- focused tests в `tests/RequirementImpactAssistant.Tests/Application` или близкой существующей группе;
- без изменений Razor Pages, export builders/services, LLM providers, appsettings и MVP-0 docs.

**Проверки:**

- `dotnet test`;
- targeted tests новых external contract models;
- ручной review diff на отсутствие `Dify`, `HttpClient`, endpoint/API key settings, embeddings, rerank и vector database.

**Критерии Done:**

- external adapter request может представить вход анализа, expected result structure и boundary notice без provider-specific payload;
- external adapter response может представить success, partial и failed outcomes без raw provider response как обязательного контракта;
- retrieved context в response использует уже существующие Stage 1 модели и состояния;
- модели не требуют Dify, сети, секретов или собственного retrieval pipeline.

**Red flags для review:**

- в модели появились обязательные Dify/workflow-specific поля;
- request содержит API key, endpoint, token или secret-like данные;
- response требует полный raw provider response как штатную публичную модель;
- модели начинают описывать embeddings, rerank, vector database или внутренний retrieval trace приложения.

## Task 2. Ввести `IExternalRagAdapter` как neutral boundary

**Цель:** добавить минимальный interface external AI/RAG adapter, через который `ExternalRagAnalysisEngine` сможет обращаться к внешнему контуру, не раскрывая UI, domain model и export provider-specific детали.

**Зависимости:** Task 1.

**Входит:**

- interface, например `IExternalRagAdapter`, в согласованной application/infrastructure boundary области;
- один async method, принимающий neutral request model и `CancellationToken`, возвращающий neutral response model;
- минимальное описание имени adapter/provider через response metadata, а не через Dify-specific свойства interface;
- tests или architecture assertions, что contract не зависит от Dify SDK, HTTP types, `ILlmProvider`, Razor Pages и export;
- при необходимости - пустая registration surface без production adapter implementation, если она нужна для компиляции будущего `ExternalRagAnalysisEngine`.

**Не входит:**

- production implementation interface-а;
- deterministic mock external RAG adapter;
- Dify adapter;
- real external call или network client;
- user secrets, options binding для внешнего adapter, endpoint/API key settings;
- UI, export или persistence schema changes.

**Ожидаемый diff:**

- один небольшой interface файл в `Application/Analysis/External` или согласованной соседней области;
- architecture/contract tests в существующем test project;
- без изменений `appsettings.json`, `appsettings.Development.json`, Razor Pages, export services и MVP-0 docs.

**Проверки:**

- `dotnet test`;
- review contract signature на отсутствие provider-specific leakage;
- review source tokens на отсутствие `Dify`, `HttpClient`, `ApiKey`, `Endpoint`, `Embedding`, `Rerank`, `Vector`.

**Критерии Done:**

- в коде есть одна нейтральная точка обращения к external AI/RAG adapter;
- interface работает только с neutral request/response models;
- отсутствие production adapter implementation считается нормальным состоянием Stage 3;
- contract не создает зависимости UI/export/domain на внешний provider.

**Red flags для review:**

- interface принимает или возвращает Dify-specific DTO;
- interface требует endpoint/key/configuration как аргументы метода;
- вместе с interface добавлен mock/Dify adapter;
- export или Razor Pages начинают ссылаться на external adapter contract.

## Task 3. Подготовить bridge для engine-provided metadata в `AiAnalysisResponse`

**Цель:** дать `ExternalRagAnalysisEngine` возможность вернуть в `AnalysisExecutionService` metadata, retrieved context и diagnostics результата, не заставляя service заново угадывать external details и не меняя direct LLM behavior.

**Зависимости:** Task 1; текущие `AiAnalysisResponse`, `AiAnalysisResultMetadata`, `AnalysisExecutionService`.

**Входит:**

- минимальное расширение `AiAnalysisResponse` или близкого internal application DTO, например optional metadata/diagnostic payload;
- правило совместимости: если engine не вернул metadata, `AnalysisExecutionService` продолжает формировать direct LLM metadata как сейчас;
- mapping tests, что direct LLM path сохраняет `AnalysisMode.DirectLlm`, текущие provider/model metadata и отсутствие retrieved context items;
- tests, что external-shaped response metadata может быть сохранена через уже существующую Stage 1 persistence model без новой migration;
- сохранение текущего status mapping `Succeeded`/`Partial`/`Failed`, если нет явной необходимости менять enum.

**Не входит:**

- изменение prompt или `DirectLlmAnalysisEngine` provider call;
- selector/registry;
- `ExternalRagAnalysisEngine`;
- fake retrieved context generation;
- export behavior changes, кроме compile-fix при изменении response contract;
- EF migration или новая схема.

**Ожидаемый diff:**

- точечное изменение `AiAnalysisResponse.cs`;
- точечная правка `AnalysisExecutionService.cs`, чтобы использовать engine-provided metadata только когда она явно передана;
- tests в `AnalysisExecutionServiceTests.cs` на direct LLM regression и external-shaped metadata save;
- без изменений Razor Pages, export semantics, LLM providers, migrations и appsettings.

**Проверки:**

- `dotnet test`;
- targeted `AnalysisExecutionServiceTests`;
- review diff на отсутствие изменений direct LLM prompt/raw provider behavior.

**Критерии Done:**

- direct LLM сценарий остается прежним и не получает retrieved context items;
- `AnalysisExecutionService` может сохранить external metadata/retrieved context, если engine вернул их явно;
- legacy и export tests не требуют ручной правки данных;
- изменение не требует сети, Dify, user secrets или новой persistence schema.

**Red flags для review:**

- direct LLM metadata начинает заполняться через external adapter concepts;
- service создает synthetic retrieved context для direct LLM или failed external mode;
- ради bridge добавлена EF migration;
- export начинает повторно вызывать analysis engine для получения metadata.

## Task 4. Добавить `ExternalRagAnalysisEngine` с controlled unavailable/failure behavior

**Цель:** добавить `ExternalRagAnalysisEngine` как реализацию `IAiAnalysisEngine`, которая выполняет только orchestration через neutral adapter contract и корректно возвращает unavailable/failed state, если adapter не настроен.

**Зависимости:** Task 1, Task 2, Task 3.

**Входит:**

- `ExternalRagAnalysisEngine` в `src/RequirementImpactAssistant.Web/Application/Analysis`;
- mapping `AiAnalysisRequest` -> neutral external adapter request;
- передача `ExpectedAnalysisResultStructure` и `AnalysisBoundaryNotice` во внешний adapter request;
- controlled failed response, если adapter отсутствует или недоступен:
  - `AiAnalysisResponseStatus.Failed`;
  - `AnalysisMode.ExternalRag`;
  - `RetrievedContextState.Unavailable`;
  - warning/error без секретов;
  - no retrieved context items;
- mapping neutral adapter response -> `AiAnalysisResponse`, `ImpactMap`, metadata, retrieved context и warnings;
- unit tests `ExternalRagAnalysisEngine` с test-only stub adapter внутри test class для completed, partial, failed и no-adapter scenarios.

**Не входит:**

- production mock external RAG adapter;
- Dify adapter;
- real HTTP/network call;
- generation of fake/mock retrieved context inside engine;
- собственная нормализация provider-specific raw response;
- UI выбора режима;
- persistence changes сверх использования Stage 1 metadata.

**Ожидаемый diff:**

- новый `ExternalRagAnalysisEngine.cs`;
- tests в `tests/RequirementImpactAssistant.Tests/Application`, например `ExternalRagAnalysisEngineTests.cs`;
- возможно небольшой helper для sanitized diagnostics, если он нужен engine tests;
- без изменений appsettings, Dify/provider configuration, Razor Pages, export behavior и migrations.

**Проверки:**

- `dotnet test`;
- targeted `ExternalRagAnalysisEngineTests`;
- review diff на отсутствие network clients, Dify tokens, production mock data generator, embeddings/rerank/vector code.

**Критерии Done:**

- `ExternalRagAnalysisEngine` реализует `IAiAnalysisEngine`;
- engine не выполняет retrieval сам и не создает fake retrieved context;
- no-adapter scenario завершается controlled failed/unavailable result без исключений наружу и без секретов;
- adapter warnings/errors сохраняются как diagnostics, а не как экспертное заключение.

**Red flags для review:**

- engine сам строит retrieved context из input/manual context ради демонстрации;
- engine вызывает HTTP/client/network напрямую;
- no-adapter scenario падает DI/runtime exception вместо controlled failed response на application level;
- в engine попали Dify-specific request/response поля.

## Task 5. Ввести selector/registry `IAiAnalysisEngine` по `AnalysisMode`

**Цель:** убрать single-engine предположение из application execution path и подготовить выбор direct LLM или external AI/RAG engine на application level, сохранив default direct LLM поведение до появления UI на Stage 6.

**Зависимости:** Task 4.

**Входит:**

- application-level selector/registry, например `IAiAnalysisEngineSelector` или близкий по текущей архитектуре вариант;
- registration `DirectLlmAnalysisEngine` и `ExternalRagAnalysisEngine` как отдельных engines без прямого выбора из UI;
- обновление `AnalysisExecutionService`, чтобы он выбирал engine по `AnalysisMode`, при этом default mode остается `DirectLlm`, пока UI не передает другое значение;
- минимальное application-level API для explicit mode, если без него невозможно протестировать external mode без UI;
- tests, что:
  - default execution выбирает direct LLM и сохраняет прежнее поведение;
  - explicit `ExternalRag` выбирает `ExternalRagAnalysisEngine`;
  - недоступный external adapter возвращает controlled failed/unavailable result;
  - direct LLM не зависит от external adapter availability.

**Не входит:**

- UI radio/select/toggle режима;
- изменение Razor Pages handlers, кроме compile-fix, если application interface меняется с default-compatible параметром;
- mock external RAG adapter feature;
- Dify adapter/configuration;
- export changes;
- изменение direct LLM prompt/provider behavior.

**Ожидаемый diff:**

- новый selector/registry file в `Application/Analysis`;
- точечное изменение `AnalysisExecutionService.cs` и `IAnalysisExecutionService.cs`, если нужен optional/default mode parameter;
- DI changes в `ServiceCollectionExtensions.cs` без external adapter implementation;
- tests в `AnalysisExecutionServiceTests.cs` и `ApplicationConfigurationTests.cs`;
- без changes в Razor UI markup, export builders/services, appsettings и migrations.

**Проверки:**

- `dotnet test`;
- configuration/DI tests;
- review diff на default direct LLM compatibility и отсутствие UI scope creep.

**Критерии Done:**

- application layer умеет выбрать engine по `AnalysisMode`;
- direct LLM остается default/fallback behavior для существующих callers;
- external mode доступен на application level, но без UI и без production adapter;
- отсутствие configured external adapter не ломает direct LLM и не требует secrets/network.

**Red flags для review:**

- selector тянет provider-specific adapter details в `AnalysisExecutionService`;
- default behavior внезапно меняется на external AI/RAG;
- Razor Pages получают dependency на external adapter или selector internals;
- DI требует зарегистрированный Dify/mock adapter для запуска приложения или tests.

## Task 6. Закрыть architecture regression checks Stage 3

**Цель:** зафиксировать, что Stage 3 добавила только contract/orchestration boundary и не протащила UI, export calls, provider-specific integration, network, secrets или собственный RAG.

**Зависимости:** Task 5.

**Входит:**

- architecture tests, что Razor Pages/PageModels не зависят от `IExternalRagAdapter`, external adapter models, Dify/provider-specific types или network clients;
- architecture tests, что export не зависит от `IAiAnalysisEngine`, selector/registry, `IExternalRagAdapter`, external adapter models, provider types или network clients;
- source-token checks на отсутствие `Dify`, endpoint/API key settings, embeddings, rerank, vector database в Stage 3 production code;
- regression tests, что `dotnet test` не требует сети, user secrets, Dify configuration или внешних ключей;
- final manual diff review scope checklist для Stage 3.

**Не входит:**

- Stage 4 mock external RAG adapter tests;
- Stage 5 Dify/manual integration tests;
- Playwright/browser E2E;
- UI smoke выбора режима;
- новая документация summary, кроме отдельного решения после завершения implementation stage.

**Ожидаемый diff:**

- небольшие architecture tests в существующем test project;
- возможно расширение текущего `ExportArchitectureTests.cs` и отдельный test для Pages/Application boundary;
- без production behavior changes, кроме уже внесенной Stage 3 boundary.

**Проверки:**

- `dotnet build`;
- `dotnet test`;
- `git diff --stat` перед review/commit;
- manual diff review на отсутствие out-of-scope файлов и зависимостей.

**Критерии Done:**

- Stage 3 tests проходят без сети, Dify, user secrets и внешних ключей;
- UI/PageModels не зависят от external adapter boundary;
- export не вызывает adapter/provider/engine и остается read-only over saved result;
- production diff не содержит mock adapter, Dify adapter, real calls, RAG, embeddings, rerank, vector database, PDF export, dashboard, workflow или MVP-0 docs changes.

**Red flags для review:**

- architecture tests разрешают UI ссылаться на adapter потому что "так проще";
- export получает dependency на selector или engine для получения metadata;
- test suite начинает требовать Dify configuration, network или secrets;
- Stage 3 silently добавляет mock adapter, Dify adapter или retrieved context generation.

## Итоговый критерий готовности Stage 3

Stage 3 считается готовым к переходу на следующий этап только если:

- все 6 tasks реализованы последовательно и прошли отдельные review;
- neutral external AI/RAG adapter contract существует и не привязан к Dify;
- `ExternalRagAnalysisEngine` реализует `IAiAnalysisEngine` и использует только neutral adapter boundary;
- no-adapter external mode возвращает controlled failed/unavailable result без synthetic retrieved context;
- application layer может выбрать direct LLM или external AI/RAG engine по `AnalysisMode`, при этом direct LLM остается default и не зависит от external adapter;
- `dotnet build` и `dotnet test` проходят без сети, Dify, user secrets и внешних ключей;
- в diff Stage 3 нет UI, mock external RAG adapter, Dify adapter, real external calls, собственных RAG/embeddings/rerank/vector database, PDF export, dashboard, workflow, изменений MVP-0 документов и изменений экспертной оценки/заключения.

# Task breakdown MVP-1. Stage 4

## Назначение

Документ фиксирует маленькие implementation tasks только для этапа 4 из `docs/program/mvp1/implementation-plan.md`: "Mock external RAG adapter".

Цель Stage 4 - добавить воспроизводимый локальный mock external RAG adapter поверх neutral adapter contract Stage 3, чтобы external AI/RAG mode можно было проверить без Dify, сети, секретов, provider configuration и собственного RAG/retrieval pipeline.

Stage 4 должна использовать уже созданные Stage 1-3 элементы:

- `IExternalRagAdapter` и neutral request/response models в `src/RequirementImpactAssistant.Web/Application/Analysis/External`;
- `ExternalRagAnalysisEngine` как единственную application-level точку обращения к adapter;
- `IAiAnalysisEngineSelector` и `AnalysisExecutionService.RunAsync(..., AnalysisMode.ExternalRag)`;
- сохранение `AiAnalysisResultMetadata` и `RetrievedContextItem`;
- уже расширенный Markdown/JSON export сохраненных metadata и retrieved context.

Этот breakdown не разрешает реализацию автоматически. Каждая task должна проходить отдельный цикл:

```text
task review -> implementation -> code review -> commit -> next task
```

## Реальная точка старта

По текущей структуре проекта Stage 3 уже добавила:

- neutral external AI/RAG models в `src/RequirementImpactAssistant.Web/Application/Analysis/External`;
- `IExternalRagAdapter`;
- `ExternalRagAnalysisEngine`;
- selector `IAiAnalysisEngineSelector` / `AiAnalysisEngineSelector`;
- overload `AnalysisExecutionService.RunAsync(Guid, AnalysisMode, CancellationToken)`;
- architecture regression checks в `tests/RequirementImpactAssistant.Tests/Application/Stage3ArchitectureRegressionTests.cs`.

На старте Stage 4 production implementation `IExternalRagAdapter` отсутствует. `ExternalRagAnalysisEngine` при отсутствии adapter возвращает controlled failed/unavailable response. `ServiceCollectionExtensions.AddApplicationAnalysis` передает в `ExternalRagAnalysisEngine` optional `IExternalRagAdapter`, но не регистрирует adapter. UI выбора режима еще не реализован и остается scope Stage 6.

## Границы Stage 4

Входит:

- deterministic `MockExternalRagAdapter` как локальная реализация `IExternalRagAdapter`;
- предсказуемый `ImpactMap`, совместимый с текущей моделью результата;
- mock retrieved context items на обезличенных демонстрационных данных;
- варианты mock response: full retrieved context, metadata only, unavailable retrieved context, partial result with warnings, failed response;
- DI wiring, если он нужен, чтобы `AnalysisMode.ExternalRag` локально проходил через mock adapter без Dify;
- tests для mock adapter, `ExternalRagAnalysisEngine`, `AnalysisExecutionService` external mode и export already-saved data;
- architecture checks, что mock не протекает в UI/export/stable provider boundary и не требует сети/секретов.

Не входит:

- Dify adapter, Dify DTO, Dify endpoint, workflow/app id, API key или user secrets;
- real external calls, `HttpClient` для external provider, network access или integration tests с внешними сервисами;
- provider configuration для реального external provider;
- собственный RAG, retrieval pipeline, embeddings, rerank, vector database, retrieval trace или agentic search;
- UI выбора режима, отображение retrieved context на страницах или изменение Razor Pages;
- export behavior changes, кроме compile-fix или tests уже существующего Stage 2 export;
- persistence/migrations без явной необходимости;
- изменение MVP-0 документов;
- Stage 5 work.

## Task 1. Добавить deterministic `MockExternalRagAdapter` happy path

**Цель:** создать локальную реализацию `IExternalRagAdapter`, которая возвращает предсказуемый successful external AI/RAG result с full retrieved context и не обращается к сети.

**Зависимости:** завершенная Stage 3; существующие `IExternalRagAdapter`, `ExternalRagAdapterRequest`, `ExternalRagAdapterResponse`, `ImpactMap`, `RetrievedContextItem`.

**Входит:**

- новый `MockExternalRagAdapter` в `src/RequirementImpactAssistant.Web/Application/Analysis/External` или согласованной соседней области;
- deterministic completed response:
  - `ExternalRagAdapterResponseStatus.Completed`;
  - provider/adapter metadata с нейтральными именами mock/local demo;
  - `RetrievedContextState.Available`;
  - 1-3 обезличенных `RetrievedContextItem` с full text/excerpt/source metadata;
  - небольшой `ImpactMap`, пригодный для сохранения и export;
  - sanitized diagnostic snapshot без raw provider payload;
- unit tests на happy path adapter response.

**Не входит:**

- scenario selector для partial/failed/metadata-only вариантов;
- DI registration;
- изменения `ExternalRagAnalysisEngine`;
- Dify, HTTP, secrets, endpoint/configuration;
- UI, export builders/services, migrations.

**Ожидаемый diff:**

- один production file mock adapter;
- tests в `tests/RequirementImpactAssistant.Tests/Application`, например `MockExternalRagAdapterTests.cs`;
- без изменений Razor Pages, appsettings, migrations, export services и MVP-0 docs.

**Проверки:**

- `dotnet test`;
- targeted tests mock adapter happy path;
- review diff на отсутствие `Dify`, `HttpClient`, `ApiKey`, `Endpoint`, embeddings, rerank, vector database.

**Критерии Done:**

- mock adapter реализует только `IExternalRagAdapter`;
- response deterministic и не зависит от времени, сети, секретов или внешних файлов;
- retrieved context отделен от manual context и заполнен через `RetrievedContextItem`;
- sanitized diagnostic snapshot не содержит provider-specific raw response.

**Red flags для review:**

- adapter делает HTTP/file/network lookup;
- mock data выглядит как реальные корпоративные данные;
- в mock response появились Dify-specific поля как обязательная модель;
- `ExternalRagAnalysisEngine` начинает генерировать mock context вместо adapter.

## Task 2. Добавить варианты mock response для incomplete/error states

**Цель:** покрыть все состояния Stage 4 из implementation plan: metadata only, unavailable retrieved context, partial result with warnings и failed response.

**Зависимости:** Task 1.

**Входит:**

- минимальный deterministic mechanism выбора mock scenario без внешней provider configuration, например constructor parameter/options для tests или нейтральный local-demo profile;
- сценарий `MetadataOnly`:
  - `RetrievedContextState.MetadataOnly`;
  - items без полного `Text`, но с source/reference/completeness metadata;
- сценарий `Unavailable`:
  - structured result доступен;
  - `RetrievedContextState.Unavailable`;
  - empty items;
  - limitation/warning;
- сценарий `Partial`:
  - `ExternalRagAdapterResponseStatus.Partial` или `CompletedWithWarnings`;
  - partial retrieved context;
  - warnings;
- сценарий `Failed`:
  - `ExternalRagAdapterResponseStatus.Failed`;
  - no `ImpactMap`;
  - sanitized error;
- tests для каждого scenario.

**Не входит:**

- UI/API для выбора scenario;
- appsettings/user-secrets/provider options;
- Dify/manual integration behavior;
- retry, timeout или network simulation;
- изменение domain enums без отдельной необходимости.

**Ожидаемый diff:**

- расширение `MockExternalRagAdapter.cs` небольшим scenario vocabulary;
- tests в `MockExternalRagAdapterTests.cs`;
- возможно небольшой test helper/fixture внутри test project;
- без changes в pages, export, persistence и real provider code.

**Проверки:**

- `dotnet test`;
- targeted tests всех mock scenarios;
- review, что scenario selection не стал production provider configuration.

**Критерии Done:**

- все варианты из Stage 4 implementation plan представлены deterministic tests;
- неполный retrieved context фиксируется как limitation/warning;
- failed response не содержит `ImpactMap` и не раскрывает secrets/raw payload;
- adapter не реализует retrieval, embeddings, rerank или scoring pipeline.

**Red flags для review:**

- scenario selection завязан на реальные endpoint/key/environment variables;
- partial/unavailable состояния маскируются как полноценный retrieved context;
- failed response бросает exception вместо neutral failed response, если это не отдельный test сценарий;
- mock adapter добавляет новый public API за пределами neutral contract.

## Task 3. Подключить mock adapter для локального external mode

**Цель:** сделать `AnalysisMode.ExternalRag` локально исполнимым через mock adapter на application level, не затрагивая UI и не добавляя внешнюю provider configuration.

**Зависимости:** Task 1, Task 2; существующие `ServiceCollectionExtensions`, `ExternalRagAnalysisEngine`, `AiAnalysisEngineSelector`.

**Входит:**

- DI registration `IExternalRagAdapter -> MockExternalRagAdapter` или близкий минимальный wiring, согласованный с текущим `AddApplicationAnalysis`;
- сохранение default `DirectLlm` behavior для существующего `RunAsync(id)`;
- explicit external mode через `RunAsync(id, AnalysisMode.ExternalRag, ...)` проходит mock adapter;
- no-adapter behavior `ExternalRagAnalysisEngine` остается unit-testable прямым созданием engine без adapter;
- update configuration/DI tests.

**Не входит:**

- UI control для выбора режима;
- appsettings switch, secrets, endpoint или provider-specific options;
- Dify adapter или Stage 5 configuration;
- изменение LLM provider selection;
- export behavior changes.

**Ожидаемый diff:**

- точечная правка `src/RequirementImpactAssistant.Web/Extensions/ServiceCollectionExtensions.cs`;
- tests в `ApplicationConfigurationTests.cs` и/или `AnalysisExecutionServiceTests.cs`;
- возможная правка Stage 3 architecture test, чтобы запретить любые external adapter implementations кроме разрешенного mock;
- без Razor Pages/appsettings/migrations.

**Проверки:**

- `dotnet test`;
- DI/configuration tests;
- review, что direct LLM default не зависит от mock adapter.

**Критерии Done:**

- application DI может создать external engine с mock adapter;
- `RunAsync(id)` по-прежнему выбирает `DirectLlm`;
- `RunAsync(id, AnalysisMode.ExternalRag)` доступен для локальной проверки без Dify, сети и secrets;
- отсутствие Dify configuration не влияет на build/test.

**Red flags для review:**

- default mode внезапно меняется на external RAG;
- DI требует external endpoint/key или user secrets;
- Razor Pages получают dependency на `IExternalRagAdapter` или mock class;
- Stage 5 Dify registration добавляется "заодно".

## Task 4. Проверить сохранение результата external mode через mock adapter

**Цель:** убедиться, что локальный external mode через mock adapter проходит весь application execution path и сохраняет metadata/retrieved context в уже существующую модель Stage 1.

**Зависимости:** Task 3; существующие `AnalysisExecutionServiceTests`, `ApplicationDbContext` mappings.

**Входит:**

- service-level tests для `AnalysisExecutionService.RunAsync(analysisId, AnalysisMode.ExternalRag, ...)`;
- проверки saved result:
  - status completed/with warnings/failed по scenario;
  - `AnalysisMode.ExternalRag`;
  - `EngineName = ExternalRagAnalysisEngine`;
  - mock provider/adapter metadata;
  - `RetrievedContextState`;
  - persisted `RetrievedContextItems`;
  - warnings/limitations;
- regression, что direct LLM result не получает retrieved context и не зависит от mock adapter scenario.

**Не входит:**

- новая persistence schema или EF migration;
- изменение expert evaluation/conclusion;
- UI отображение результата;
- попытка связать retrieved context items с каждым `ImpactMapItem`;
- изменение prompt/direct LLM provider call.

**Ожидаемый diff:**

- tests в `AnalysisExecutionServiceTests.cs` или отдельном service-level test file;
- возможно маленькие test fixtures для создания минимального analysis;
- без production changes, кроме compile-fix при обнаружении узкого mapping дефекта.

**Проверки:**

- `dotnet test`;
- targeted `AnalysisExecutionServiceTests`;
- review, что tests не используют network/secrets и не требуют UI.

**Критерии Done:**

- external mode через mock adapter сохраняет retrieved context и metadata;
- metadata-only/unavailable/partial/failed states сохраняются как ожидается;
- direct LLM остается отдельным режимом без synthetic retrieved context;
- результат пригоден для последующего export уже существующими services.

**Red flags для review:**

- ради mock adapter добавлена migration;
- service подменяет manual context на retrieved context;
- failed external scenario ломает сохранение диагностики;
- tests обходят `AnalysisExecutionService` и не проверяют реальный application path.

## Task 5. Проверить export уже сохраненного mock external результата

**Цель:** подтвердить, что Stage 2 Markdown/JSON export корректно выводит saved mock external result без изменения export semantics.

**Зависимости:** Task 4; существующие `AnalysisMarkdownExportServiceTests`, `AnalysisJsonExportServiceTests`.

**Входит:**

- service-level export tests для saved result, полученного через mock external adapter;
- проверки Markdown и JSON на:
  - `ExternalRag`;
  - engine/provider/adapter metadata;
  - retrieved context full/metadata-only/unavailable/partial states;
  - warnings/limitations;
  - absence of Dify/provider-specific stable payload;
- compile-fix в export только если текущий Stage 2 code не читает уже сохраненные данные.

**Не входит:**

- новый формат export или published JSON Schema;
- повторный вызов adapter/engine из export;
- real external calls;
- UI-facing download changes;
- PDF export.

**Ожидаемый diff:**

- tests в existing export test files;
- production export code changes только при необходимости исправить чтение уже сохраненной модели;
- без изменений Razor Pages, adapter contract, appsettings, migrations.

**Проверки:**

- `dotnet test`;
- targeted Markdown/JSON export tests;
- review, что export строится только из saved `Analysis` graph.

**Критерии Done:**

- saved mock external result экспортируется с retrieved context или limitation;
- `MetadataOnly` допускает отсутствие полного текста;
- `Unavailable` сохраняет пустые items и warning/limitation;
- export не вызывает `IAiAnalysisEngine`, `IExternalRagAdapter`, LLM provider или network.

**Red flags для review:**

- export дергает mock adapter, чтобы "достроить" retrieved context;
- JSON включает полный provider-specific raw response как обязательный contract;
- Markdown формулирует retrieved context как экспертное заключение;
- export behavior меняется для unrelated legacy/direct LLM сценариев.

## Task 6. Закрыть architecture regression checks Stage 4

**Цель:** зафиксировать, что Stage 4 добавила только mock external adapter и локальные проверки external mode, не протащив Dify, real calls, secrets, UI или собственный RAG.

**Зависимости:** Task 5.

**Входит:**

- update Stage 3 architecture checks с учетом единственной разрешенной implementation `MockExternalRagAdapter`;
- architecture tests, что Razor Pages/PageModels не зависят от mock adapter, `IExternalRagAdapter`, external adapter models или network clients;
- architecture tests, что export не зависит от `IAiAnalysisEngine`, selector, `IExternalRagAdapter`, mock adapter или network clients;
- source-token checks на отсутствие `Dify`, endpoint/API key/user secrets, embeddings, rerank, vector database, retrieval pipeline в Stage 4 production diff;
- regression, что `dotnet test` проходит без сети и внешних ключей.

**Не входит:**

- Stage 5 Dify/manual integration tests;
- Playwright/browser E2E;
- UI smoke выбора режима;
- новая Stage 4 summary без отдельного решения;
- изменения MVP-0 docs.

**Ожидаемый diff:**

- небольшие изменения в `Stage3ArchitectureRegressionTests.cs` или новый `Stage4ArchitectureRegressionTests.cs`;
- возможно расширение `ExportArchitectureTests.cs`;
- без production behavior changes, кроме уже внесенного mock adapter/DI wiring.

**Проверки:**

- `dotnet build`;
- `dotnet test`;
- `git diff --stat` перед review/commit;
- manual diff review на отсутствие out-of-scope файлов и зависимостей.

**Критерии Done:**

- tests проходят без Dify, network, user secrets и внешних ключей;
- единственная production implementation `IExternalRagAdapter` на Stage 4 - deterministic mock adapter;
- UI и export не зависят от mock/external adapter boundary;
- diff Stage 4 не содержит Dify adapter, real external calls, собственный RAG/embeddings/rerank/vector database, UI, PDF export, dashboard, workflow или MVP-0 docs changes.

**Red flags для review:**

- architecture tests ослаблены так, что разрешают UI ссылаться на adapter;
- mock adapter становится stable provider boundary для будущих real integrations;
- появились `HttpClient`, endpoint/API key или user secrets для external provider;
- Stage 4 silently включает Stage 5 Dify work.

## Итоговый критерий готовности Stage 4

Stage 4 считается готовым к переходу на следующий этап только если:

- все 6 tasks реализованы последовательно и прошли отдельные review;
- deterministic `MockExternalRagAdapter` реализует `IExternalRagAdapter` и возвращает full, metadata-only, unavailable, partial и failed scenarios;
- external mode может пройти локально через mock adapter на application level без UI, Dify, сети, secrets и real provider configuration;
- сохраненный mock external result содержит `ExternalRag` metadata, provider/adapter сведения, retrieved context items или limitation;
- Markdown/JSON export уже сохраненного mock external result работает без повторного вызова adapter;
- `dotnet build` и `dotnet test` проходят без сети, Dify, user secrets и внешних ключей;
- в diff Stage 4 нет Dify adapter, real external calls, собственного RAG/retrieval pipeline/embeddings/rerank/vector database, UI changes, migrations без необходимости, MVP-0 docs changes и Stage 5 work.

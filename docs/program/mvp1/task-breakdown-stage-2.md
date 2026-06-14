# Task breakdown MVP-1. Stage 2

## Назначение

Документ фиксирует маленькие implementation tasks только для этапа 2 из `docs/program/mvp1/implementation-plan.md`: "Расширение Markdown и JSON export".

Цель Stage 2 - расширить существующие Markdown и JSON export так, чтобы они использовали уже сохраненные после Stage 1 данные:

- analysis mode;
- engine/provider/adapter/model-workflow-profile metadata;
- manual context usage во внешнем контуре;
- retrieved context state;
- retrieved context items или metadata;
- limitations и warnings.

Stage 2 строится только поверх данных, уже сохраненных моделью Stage 1. Export остается read-only представлением сохраненного анализа и не должен запускать интеллектуальный анализ повторно.

Этот breakdown не разрешает реализацию автоматически. Каждая task должна проходить отдельный цикл:

```text
task review -> implementation -> code review -> commit -> next task
```

## Реальная точка старта

По текущей структуре проекта Stage 1 уже добавила:

- `AnalysisMode` в `src/RequirementImpactAssistant.Web/Domain/Enums/AnalysisMode.cs`;
- `RetrievedContextState` в `src/RequirementImpactAssistant.Web/Domain/Enums/RetrievedContextState.cs`;
- `RetrievedContextItemCompleteness` в `src/RequirementImpactAssistant.Web/Domain/Enums/RetrievedContextItemCompleteness.cs`;
- `AiAnalysisResultMetadata` в `src/RequirementImpactAssistant.Web/Domain/AiAnalysisResultMetadata.cs`;
- `RetrievedContextItem` в `src/RequirementImpactAssistant.Web/Domain/RetrievedContextItem.cs`;
- EF mapping для metadata и `RetrievedContextItems` в `src/RequirementImpactAssistant.Web/Data/ApplicationDbContext.cs`;
- persistence tests для direct LLM, legacy MVP-0 и external-shaped данных в `tests/RequirementImpactAssistant.Tests/Data/ApplicationDbContextPersistenceTests.cs`;
- заполнение direct LLM metadata в `src/RequirementImpactAssistant.Web/Application/Analysis/AnalysisExecutionService.cs`.

Текущий export находится в:

- `src/RequirementImpactAssistant.Web/Application/Export/AnalysisMarkdownReportBuilder.cs`;
- `src/RequirementImpactAssistant.Web/Application/Export/AnalysisMarkdownExportService.cs`;
- `src/RequirementImpactAssistant.Web/Application/Export/AnalysisJsonReportBuilder.cs`;
- `src/RequirementImpactAssistant.Web/Application/Export/AnalysisJsonExportService.cs`;
- `tests/RequirementImpactAssistant.Tests/Application/AnalysisMarkdownExportServiceTests.cs`;
- `tests/RequirementImpactAssistant.Tests/Application/AnalysisJsonExportServiceTests.cs`.

На старте Stage 2 Markdown export выводит input, manual context fragments, impact map, expert evaluation, expert conclusion и decision boundary, но не выводит Stage 1 metadata/retrieved context. JSON export имеет стабильные top-level поля `metadata`, `input`, `contextFragments`, `aiAnalysisResult`, `impactMap`, `expertEvaluation`, `expertConclusion`, `exportMetadata`, но пока не содержит смысловых блоков Stage 1 metadata/retrieved context. Export services загружают `AiAnalysisResult`, но Stage 2 должна явно проверить, что owned `RetrievedContextItems` читаются из SQLite в service-level export сценариях.

## Границы Stage 2

Входит:

- расширение export model/builders/services существующего Markdown и JSON export;
- отображение сохраненных Stage 1 metadata и retrieved context;
- legacy MVP-0 compatibility;
- tests для direct LLM, legacy MVP-0 и external-shaped сохраненных данных;
- минимальная smoke/checklist фиксация Stage 2, если она нужна после кода.

Не входит:

- UI;
- mock external RAG adapter;
- Dify adapter;
- real external calls;
- новый analysis engine;
- selector/registry;
- собственный RAG, embeddings, rerank, vector database;
- PDF export;
- dashboard или workflow;
- изменение MVP-0 документов;
- изменение поведения direct LLM анализа;
- изменение экспертной оценки или экспертного заключения сверх отображения сохраненных данных в export;
- повторный вызов `IAiAnalysisEngine`, LLM, external AI/RAG provider, Dify, mock adapter или внешних сервисов из export.

## Task 1. Добавить в Markdown блок происхождения результата

**Цель:** сделать Markdown export человекочитаемым по части Stage 1 metadata без retrieved context item-детализации: эксперт должен видеть режим анализа, engine/provider/adapter/model profile, использование manual context во внешнем контуре, состояние retrieved context и warnings.

**Зависимости:** завершенная Stage 1; существующий `AnalysisMarkdownReportBuilder`.

**Входит:**

- новый Markdown section, например `## Analysis result metadata` или близкое по смыслу название, в `AnalysisMarkdownReportBuilder`;
- вывод `AiAnalysisResult.Metadata.AnalysisMode`;
- вывод `Metadata.EngineName` с fallback к legacy `AiAnalysisResult.EngineName`;
- вывод `ProviderName`, `AdapterName`, `ModelWorkflowProfileName` с `Not provided`/нейтральным отсутствием для legacy/direct случаев;
- вывод `ManualContextForwardedToExternalAiOrRag`;
- вывод `RetrievedContextState`;
- вывод `Warnings` как сохраненных diagnostics/limitations;
- unit/service tests в `AnalysisMarkdownExportServiceTests` для direct LLM metadata и legacy MVP-0 default metadata.

**Не входит:**

- вывод списка `RetrievedContextItems`;
- JSON export;
- изменение загрузки данных из внешних provider-ов;
- изменение `AnalysisExecutionService`, prompt или direct LLM behavior;
- UI.

**Ожидаемый diff:**

- точечное изменение `AnalysisMarkdownReportBuilder.cs`;
- несколько assertions или новых tests в `AnalysisMarkdownExportServiceTests.cs`;
- без изменений domain model, EF migrations, adapters, Razor Pages и MVP-0 docs.

**Проверки:**

- `dotnet test`;
- targeted markdown export tests, если используется фильтр по классу;
- ручной review diff на отсутствие вызовов `IAiAnalysisEngine`, `ILlmProvider`, Dify, mock adapter, network/API code.

**Критерии Done:**

- Markdown export для нового direct LLM результата показывает `DirectLlm`, engine/provider/model metadata и `RetrievedContextState.Unavailable`;
- legacy MVP-0 результат без полной Stage 1 metadata экспортируется как совместимый direct LLM результат, а не как поврежденные данные;
- warnings выводятся только из сохраненной metadata;
- direct LLM не получает synthetic retrieved context.

**Red flags для review:**

- export пытается достроить metadata через повторный анализ;
- direct LLM export получает пустой retrieved context item ради унификации;
- Markdown формулировки смешивают AI result metadata с экспертным заключением;
- в diff появились UI, adapters, provider settings, secrets или network code.

## Task 2. Добавить в JSON стабильные блоки metadata результата

**Цель:** расширить JSON export смысловыми полями Stage 1 metadata без фиксации отдельной JSON Schema и без provider-specific payload, сохранив пригодность JSON для будущего сравнения режимов.

**Зависимости:** Task 1.

**Входит:**

- расширение `AnalysisJsonReportBuilder.ToAiAnalysisResult` или близкого private mapping;
- JSON fields внутри `aiAnalysisResult`, например:
  - `analysisMode`;
  - `analysisEngine`;
  - `provider`;
  - `adapter`;
  - `modelWorkflowProfile`;
  - `manualContextUsage`;
  - `retrievedContextState`;
  - `retrievedContextLimitations`;
  - `warnings`;
- сохранение прежних legacy fields `engineName`, `providerName`, `modelName`, `promptVersion`, `inputSnapshot`, `rawResponse`, `errorMessage`, если они уже нужны текущим tests/пользователям;
- расширение `exportMetadata` версией/признаком Stage 2 export format, если это не ломает существующий top-level contract;
- JSON tests для direct LLM и legacy MVP-0 compatibility.

**Не входит:**

- список `retrievedContext.items`;
- опубликованная JSON Schema;
- Dify-specific JSON как публичный формат;
- изменение top-level порядка без необходимости;
- изменение expert evaluation/conclusion semantics.

**Ожидаемый diff:**

- точечное изменение `AnalysisJsonReportBuilder.cs`;
- assertions в `AnalysisJsonExportServiceTests.cs`;
- без изменений EF migrations, application analysis services, adapters, UI.

**Проверки:**

- `dotnet test`;
- JSON parse assertions на camelCase names;
- review, что JSON строится только из `analysis.AiAnalysisResult.Metadata` и legacy fallback fields.

**Критерии Done:**

- JSON export содержит mode/engine/provider/adapter/manual-context/retrieved-context-state/warnings для saved result;
- legacy MVP-0 result экспортируется без exception и без synthetic retrieved context;
- direct LLM JSON явно показывает отсутствие retrieved context как состояние/limitation, а не как ошибку;
- прежние обязательные chapter/export fields остаются доступными.

**Red flags для review:**

- JSON export начинает включать raw Dify/provider response как стабильный публичный контракт;
- top-level shape ломается без отдельного решения;
- export вызывает сервис анализа или provider boundary;
- warnings берутся из `ErrorMessage` вместо сохраненной metadata без явного, проверенного правила compatibility.

## Task 3. Вывести retrieved context items в Markdown

**Цель:** сделать Markdown export пригодным для чтения основания external AI/RAG-shaped результата: показать retrieved context state, items/metadata, completeness, rank/score, source/reference и limitation notes, не смешивая это с manual context fragments.

**Зависимости:** Task 1.

**Входит:**

- отдельный Markdown section для retrieved context, например `## Retrieved context`;
- вывод empty/unavailable состояния для `DirectLlm` и legacy без списка items;
- вывод `Available`, `MetadataOnly`, `Partial`, `Unavailable`;
- вывод каждого сохраненного `RetrievedContextItem`:
  - source title;
  - source id/external reference;
  - fragment id;
  - text или excerpt, если сохранены;
  - url/reference;
  - rank/score;
  - provider/adapter;
  - completeness;
  - warning/limitation note;
- корректное fenced-block форматирование для retrieved text/excerpt по аналогии с context fragments, чтобы backticks в сохраненном тексте не ломали report;
- Markdown tests для external-shaped `Available`, `MetadataOnly`, `Partial` и `Unavailable` сценариев.

**Не входит:**

- связывание retrieved context items с каждым `ImpactMapItem`;
- редактирование retrieved context;
- UI просмотра retrieved context;
- создание retrieval trace;
- изменение manual context fragments.

**Ожидаемый diff:**

- расширение `AnalysisMarkdownReportBuilder.cs` helper-методами форматирования retrieved context;
- tests/fixtures в `AnalysisMarkdownExportServiceTests.cs` только на in-memory builder graph или заранее подготовленном result fixture;
- без changes в domain/EF и без service-level round-trip/query loading для owned `RetrievedContextItems`; загрузка из SQLite проверяется только в Task 5.

**Проверки:**

- `dotnet test`;
- targeted Markdown export tests;
- review Markdown output на человекочитаемость и явное разделение `Context fragments` и `Retrieved context`.

**Критерии Done:**

- external-shaped saved result с full text/excerpt/metadata-only item виден в Markdown export;
- partial/unavailable state виден как limitation, а не как экспертное заключение;
- direct LLM/legacy export не показывает искусственные retrieved context items;
- Markdown не ломается, если retrieved text/excerpt содержит backtick fences.

**Red flags для review:**

- retrieved context представлен как достаточное основание управленческого решения;
- items сортируются/пересчитываются через новую retrieval/rerank логику;
- export пытается скачать или восстановить полный текст по ссылке;
- manual context и retrieved context объединены в один раздел без различения происхождения.

## Task 4. Вывести retrieved context в JSON

**Цель:** добавить в JSON export машинно-удобный блок `retrievedContext`, который отражает только сохраненные Stage 1 state/items/limitations и не зависит от Dify или иного provider-specific response.

**Зависимости:** Task 2, Task 3.

**Входит:**

- JSON block внутри `aiAnalysisResult` или рядом с ним по согласованной текущей структуре:
  - `state`;
  - `items`;
  - `limitations`;
  - `warnings`, если они не дублируются неуправляемо;
- mapping `RetrievedContextItem` в camelCase:
  - `sourceTitle`;
  - `sourceId`;
  - `externalReference`;
  - `fragmentId`;
  - `text`;
  - `excerpt`;
  - `urlOrReference`;
  - `rank`;
  - `score`;
  - `provider`;
  - `adapter`;
  - `completeness`;
  - `warningOrLimitationNote`;
- deterministic ordering items по сохраненному order/ordinal, если EF возвращает его, или по нейтральному стабильному fallback без смыслового rerank;
- JSON tests для `Available`, `MetadataOnly`, `Partial`, `Unavailable`, direct LLM и legacy.

**Не входит:**

- новая публичная JSON Schema;
- provider-specific raw response;
- Dify workflow ids как обязательная часть export model;
- вычисление качества retrieval;
- связь retrieved context с impact map items.

**Ожидаемый diff:**

- изменение `AnalysisJsonReportBuilder.cs`;
- assertions в `AnalysisJsonExportServiceTests.cs`;
- возможно маленький shared test fixture внутри test project, если это уменьшит duplication между Markdown/JSON tests.

**Проверки:**

- `dotnet test`;
- JSON parse assertions для каждого состояния retrieved context;
- review на отсутствие external calls и provider-specific stable contract.

**Критерии Done:**

- JSON export содержит весь сохраненный retrieved context item metadata/text/excerpt без потерь;
- `MetadataOnly` item допускает отсутствие `text` и `excerpt`;
- `Unavailable` допускает пустой `items` и сохраняет limitation/warnings;
- direct LLM и legacy остаются совместимыми и не получают synthetic items.

**Red flags для review:**

- JSON builder делает HTTP/file/network lookup по `urlOrReference`;
- порядок items меняется через score-based rerank;
- `retrievedContext` становится обязательным источником экспертного заключения;
- в stable JSON попали Dify-specific поля как обязательные.

## Task 5. Закрыть service-level export round-trip для saved Stage 1 данных

**Цель:** убедиться, что Markdown и JSON export services читают из SQLite все Stage 1 данные, включая owned `RetrievedContextItems`, а не только in-memory builder graph.

**Зависимости:** Task 3, Task 4.

**Входит:**

- service-level tests, где analysis сохраняется через `ApplicationDbContext`, затем экспортируется через `AnalysisMarkdownExportService` и `AnalysisJsonExportService`;
- проверка external-shaped saved result с at least one retrieved context item;
- проверка legacy MVP-0 result после migrations, если существующие helpers позволяют сделать это без большого duplication;
- при необходимости - точечная правка query в `AnalysisMarkdownExportService.LoadAnalysisAsync` и `AnalysisJsonExportService.LoadAnalysisAsync`, например `AsSplitQuery()` или EF include/owned-loading fix;
- проверка, что export unavailable без expert conclusion остается прежним.

**Не входит:**

- изменение requirement lifecycle/status model;
- изменение Razor Pages, PageModel, page handlers и Details page export handlers;
- создание нового repository/export facade;
- миграции БД;
- любые adapter или engine changes.

**Ожидаемый diff:**

- tests в `AnalysisMarkdownExportServiceTests.cs` и `AnalysisJsonExportServiceTests.cs`;
- возможно небольшая query правка в `AnalysisMarkdownExportService.cs` и `AnalysisJsonExportService.cs`;
- без изменений domain, migrations, analysis execution, UI layout.

**Проверки:**

- `dotnet test`;
- targeted service export tests;
- review SQL/EF changes на отсутствие schema churn.

**Критерии Done:**

- saved external-shaped result экспортируется с retrieved context items в Markdown и JSON;
- saved direct LLM result экспортируется без retrieved context items;
- legacy result экспортируется без exception и без требования manual data repair;
- export services по-прежнему не имеют зависимостей от `IAiAnalysisEngine`, `ILlmProvider`, external adapter или network.

**Red flags для review:**

- ради загрузки retrieved context добавлена новая persistence schema или migration;
- export service получил dependency на analysis execution service или provider;
- legacy compatibility проверяется только на in-memory объекте, а не на saved/read сценарии;
- page handler behavior изменился; проверка UI-facing download сценария допустима только как smoke/manual verification существующего скачивания уже готового export без изменений Razor Pages, PageModel или handlers.

## Task 6. Финальная regression-проверка Stage 2 и smoke/checklist

**Цель:** зафиксировать, что Stage 2 завершена как export-only расширение и не протащила в проект UI/adapters/RAG/real calls, а оба export-формата пригодны для сравнения direct LLM и external-shaped результатов.

**Зависимости:** Task 5.

**Входит:**

- минимальные regression tests или architecture assertions, что export services/builders не зависят от `IAiAnalysisEngine`, `ILlmProvider`, external adapter types и network clients;
- smoke/manual verification существующего UI-facing download сценария для Markdown/JSON без изменений Razor Pages, PageModel, page handlers или Details page export handlers;
- при необходимости - `docs/program/mvp1/stage-2-smoke-checklist.md` или маленький раздел в Stage 2 summary, но только если на это будет отдельное решение в рамках task;
- итоговая ручная smoke-проверка: direct LLM saved result, legacy saved result, external-shaped saved result с retrieved context.

**Не входит:**

- реализация полного MVP-1 smoke из Stage 7 implementation plan;
- Playwright/browser E2E framework;
- UI выбора режима;
- mock/Dify adapters;
- real external integration tests.

**Ожидаемый diff:**

- небольшие tests в существующем test project;
- опционально один Stage 2 checklist/summary doc только после отдельного подтверждения scope task;
- без изменения production behavior, кроме уже внесенного export behavior.

**Проверки:**

- `dotnet build`;
- `dotnet test`;
- `git diff --stat` перед review/commit;
- manual diff review на отсутствие out-of-scope файлов.

**Критерии Done:**

- Stage 2 export behavior покрыт tests для direct LLM, legacy и external-shaped данных;
- Markdown и JSON показывают saved mode, metadata, manual context usage, retrieved context state/items/limitations/warnings;
- tests проходят без сети, Dify, user secrets и внешних ключей;
- scope не вышел за export model/builders/services/tests и разрешенную Stage 2 документацию.

**Red flags для review:**

- появилась реализация mock external RAG adapter, Dify adapter или selector/registry;
- добавлены secrets/configuration для внешних сервисов;
- Stage 2 tests требуют сеть или реальные provider credentials;
- export начинает менять analysis state, expert evaluation или expert conclusion.

## Итоговый критерий готовности Stage 2

Stage 2 считается готовым к переходу на следующий этап только если:

- все 6 tasks реализованы последовательно и прошли отдельные review;
- Markdown export и JSON export используют только сохраненные Stage 1 данные;
- direct LLM и legacy MVP-0 сценарии остаются совместимыми;
- external-shaped saved data экспортируется с retrieved context state/items или limitation;
- `dotnet build` и `dotnet test` проходят без сети, Dify, user secrets и внешних ключей;
- в diff Stage 2 нет UI, adapters, real external calls, RAG, embeddings, rerank, vector database, PDF export, dashboard, workflow и изменений MVP-0 документов.

# Stage 6 summary MVP-1

## Назначение

Stage 6 добавила UI/PageModel boundary для выбора режима анализа `DirectLlm` / `ExternalRag` и просмотра сохраненного основания external AI/RAG результата без расширения provider-specific UI и без новых external/RAG pipeline.

## Основание

- `docs/program/mvp1/task-breakdown-stage-6.md`
- `docs/program/mvp1/implementation-plan.md`
- `docs/program/mvp1/technical-design.md`
- `docs/program/mvp1/stage-5-summary.md`

## Что было реализовано

- task breakdown Stage 6;
- server-side input boundary на `Review` page для выбора `AnalysisMode`;
- neutral UI controls выбора `DirectLlm` / `ExternalRag`;
- сохранение `DirectLlm` как default и old POST fallback;
- отображение saved result metadata на `Details` page;
- отображение saved retrieved context из persisted `AiAnalysisResult.Metadata`;
- architecture regression checks Stage 6 для UI/PageModel/export boundaries.

## Коммиты Stage 6

- `2007a6d` - Add MVP-1 stage 6 task breakdown
- `e47280f` - Add analysis mode input boundary
- `9aa1ec9` - Add analysis mode selection controls
- `c0992be` - Show analysis result metadata
- `306fdb6` - Show saved retrieved context
- `e119026` - Add Stage 6 architecture regression checks

## Сохраненные границы

- UI/PageModel выбирает режим через application-level boundary; `DirectLlm` остается default/old POST fallback.
- UI controls нейтральные и не зависят от Dify, provider options, DTO, API key, endpoint или config.
- `Details` page показывает saved result metadata и saved retrieved context только из persisted `AiAnalysisResult.Metadata`.
- Retrieved context display не вызывает external providers, не парсит `RawResponse`, не смешивает manual `ContextFragments` с retrieved context и оставляет `RawResponse` diagnostics доступными отдельно.
- Export boundary не изменялась: export использует только saved result downloads, без вызовов `IAiAnalysisEngine`, adapter или provider.
- Собственный RAG/retrieval pipeline, embeddings, rerank, vector DB, agentic workflow, secrets/config, migrations и MVP-0 changes не добавлялись.

## Проверки

- `dotnet build RequirementImpactAssistant.sln` прошел: 0 warnings / 0 errors.
- `dotnet test RequirementImpactAssistant.sln` прошел: 300/300 tests passed.
- `git diff --check` прошел без ошибок.
- Финальный status после Task 5 был clean/up to date на `e119026`.

## Что Stage 6 НЕ делала

- Не начинала Stage 7 smoke-сценарий или final MVP-1 gate.
- Не добавляла provider-specific настройки в UI.
- Не добавляла новые endpoint/API key/workflow поля.
- Не меняла export на повторный анализ или external enrichment.
- Не добавляла production changes за пределами утвержденных Stage 6 tasks.

## Следующий этап

Следующий шаг - отдельный Stage 6/full MVP-1 gate или следующий запланированный stage/review как новое решение.

Этот summary не запускает следующий gate и не заменяет отдельное решение о продолжении.

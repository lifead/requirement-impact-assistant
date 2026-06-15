# Stage 7 summary MVP-1

## Назначение

Stage 7 зафиксировала MVP-1 smoke-сценарий на обезличенной baseline fixture: от подготовленных исходных данных и manual context до сохраненного результата, impact map, экспертного слоя и Markdown/JSON export.

## Основание

- `docs/program/mvp1/task-breakdown-stage-7.md`
- `docs/program/mvp1/implementation-plan.md`
- `docs/program/mvp1/technical-design.md`
- `docs/program/mvp1/stage-6-summary.md`

## Что было реализовано

- task breakdown Stage 7;
- обезличенная baseline fixture для воспроизводимого MVP-1 smoke-сценария;
- `DirectLlm` smoke path для default/explicit `DirectLlm` через application service boundary;
- проверка persisted result, metadata, impact map и manual context для direct path;
- `ExternalRag` smoke path через local fake/mock adapter through application boundary;
- проверка retrieved context state/items и metadata для external path;
- smoke checks экспертной оценки, экспертного заключения и Markdown/JSON export из saved result graph;
- smoke regression guards для test-only boundaries, отсутствия production smoke helper leakage, обязательной browser/Playwright зависимости и real network dependency.

## Коммиты Stage 7

- `d32e926` - Add MVP-1 stage 7 task breakdown
- `78cab23` - Add MVP-1 smoke baseline fixture
- `940fe5b` - Add Direct LLM smoke path test
- `ad38ecf` - Add external RAG smoke path test
- `f14c4ba` - Add MVP-1 export smoke checks
- `bae3780` - Add Stage 7 smoke regression checks

## Smoke coverage

- `DirectLlm` покрывает default/explicit `DirectLlm` через application service boundary, persisted result/metadata/impact map/manual context и не вызывает external adapter.
- `ExternalRag` покрывает local fake/mock adapter through application boundary, retrieved context state/items и metadata, без real Dify/DeepSeek/network/secrets.
- Expert evaluation/conclusion и Markdown/JSON export запускаются от saved result graph.
- Export не вызывает `IAiAnalysisEngine`, selector, adapter или provider.

## Сохраненные границы

- Production code не менялся.
- Новый RAG/retrieval pipeline, embeddings, rerank, vector DB, agentic workflow не добавлялись.
- Новые secrets/config, migrations и MVP-0 changes не добавлялись.
- Smoke regression guards защищают test-only boundaries и отсутствие production smoke helper leakage.
- Browser, Playwright и real network не стали обязательной зависимостью Stage 7.

## Проверки

- `dotnet build RequirementImpactAssistant.sln` прошел: 0 warnings / 0 errors.
- `dotnet test RequirementImpactAssistant.sln` прошел: 319/319 tests passed.
- `git diff --check` прошел без ошибок.
- Финальный status после Task 5 был clean/up to date на `bae3780`.

## Следующий этап

Следующий шаг - отдельный Stage 8 / full MVP-1 gate или следующий запланированный stage/review как новое решение.

Этот summary не запускает Stage 8 и не заменяет отдельное решение о продолжении.

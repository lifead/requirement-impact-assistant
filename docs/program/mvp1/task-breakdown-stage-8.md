# Task breakdown MVP-1. Stage 8

## Назначение

Документ фиксирует маленькие implementation tasks только для этапа 8 из `docs/program/mvp1/implementation-plan.md`: "Тесты на архитектурные границы и воспроизводимость".

Это именно breakdown Stage 8, а не начало реализации тестов, production code, migrations, browser/E2E framework или новой функциональности. Реализация Stage 8 должна начаться только после отдельного review/approval этого breakdown. Каждая task должна проходить отдельный цикл:

```text
task review -> implementation -> code review -> commit -> next task
```

Цель Stage 8 - защитить архитектурные решения MVP-1 от случайного размывания и подтвердить, что основной test/build контур воспроизводим без Dify/DeepSeek secrets, real network, реальных корпоративных данных и обязательной внешней конфигурации.

## Основание

- `docs/program/mvp1/implementation-plan.md`
- `docs/program/mvp1/technical-design.md`
- `docs/program/mvp1/requirements-draft.md`
- `docs/program/mvp1/stage-7-summary.md`
- `docs/program/mvp1/task-breakdown-stage-7.md`

## Реальная точка старта

По итогам Stage 7 в MVP-1 уже должны быть доступны:

- reproducible smoke baseline на обезличенных данных;
- `DirectLlm` smoke path через application service boundary;
- `ExternalRag` smoke path через local fake/mock adapter;
- сохранение mode, engine/provider/adapter metadata, retrieved context state/items и warnings/limitations;
- expert evaluation/conclusion как отдельный человеческий слой;
- Markdown/JSON export из saved result graph;
- smoke regression checks, что export не вызывает `IAiAnalysisEngine`, selector, adapter или provider;
- подтверждение, что `dotnet test` Stage 7 проходил без real Dify/DeepSeek/network/secrets.

Stage 8 не должна повторять Stage 7 smoke-сценарий как пользовательский workflow. Ее фокус - архитектурные и воспроизводимые regression guards поверх уже реализованных MVP-1 границ.

## Границы Stage 8

Входит:

- architecture/reproducibility test coverage only;
- selector/registry boundary для выбора `IAiAnalysisEngine` по mode;
- regression checks для `DirectLlm` и `ExternalRag` mode boundaries;
- coverage матрицы retrieved context states: `Available`, `MetadataOnly`, `Unavailable`, `Partial`;
- Markdown/JSON export matrix для direct, external и legacy saved results;
- проверка, что export не вызывает `IAiAnalysisEngine`, selector, adapter или provider;
- проверка, что UI/PageModels идут через application services и не зависят напрямую от AI/RAG provider, Dify adapter, retrieval API или external LLM provider;
- проверка, что mock adapter не требует network/secrets;
- проверка, что `dotnet test` не зависит от Dify config, secrets или network;
- проверка, что external adapter error handling не раскрывает secrets;
- финальные reproducibility/full suite checks и Stage 8 summary.

Не входит без отдельного решения:

- new production feature behavior;
- собственный RAG/retrieval pipeline, embeddings, rerank, vector DB, agentic workflow;
- mandatory Dify/DeepSeek network calls;
- real corporate data;
- new secrets/config committed;
- migrations/MVP-0 changes;
- Playwright/browser E2E;
- production integration/load/performance tests;
- dashboard/workflow/Jira/Confluence integrations;
- commit/push без отдельной явной команды пользователя.

## Task 1. Scope inventory и gap review существующих architecture/reproducibility tests

**Цель:** перед добавлением Stage 8 guards зафиксировать, какие проверки уже есть после Stage 7, где есть дублирование, а где остаются пробелы по architecture/reproducibility coverage.

**Зависимости:** завершенная Stage 7; clean или явно понятное рабочее дерево; существующие test projects и Stage 7 smoke tests.

**Входит:**

- inventory существующих тестов по:
  - selector/registry;
  - `DirectLlm` и `ExternalRag` boundaries;
  - retrieved context states;
  - Markdown/JSON export;
  - UI/PageModel/application service boundary;
  - mock adapter no-network/no-secrets;
  - config independence;
  - secret sanitization;
- фиксация gap list для Tasks 2-5;
- решение, какие проверки должны быть targeted unit/application/source-token tests, а какие уже покрыты smoke tests;
- проверка, что Stage 8 не требует browser/Playwright или real provider integration.

**Не входит:**

- добавление production behavior;
- переписывание Stage 7 smoke tests;
- массовый рефакторинг тестовой инфраструктуры;
- изменение Dify/DeepSeek конфигурации.

**Ожидаемый diff:**

- documentation-only или narrow test inventory note в существующем test context, если в проекте уже есть такой паттерн;
- no production code changes.

**Проверки:**

- targeted inspection commands;
- `git diff --check`;
- `git status --short`.

**Критерии Done:**

- понятен минимальный набор Stage 8 guards;
- исключено дублирование Stage 7 smoke coverage без необходимости;
- каждый дальнейший gap привязан к отдельной task.

**Red flags для review:**

- inventory сразу меняет production code;
- Stage 8 превращается в broad refactor;
- предлагается обязательный browser/E2E или real external provider gate.

## Task 2. Selector/registry и DirectLlm/ExternalRag boundary regression tests

**Цель:** закрепить выбор analysis engine через selector/registry и подтвердить, что `DirectLlm` и `ExternalRag` остаются двумя отдельными поддерживаемыми режимами за `IAiAnalysisEngine`.

**Зависимости:** Task 1; существующие selector/registry/application service implementations.

**Входит:**

- tests, что `DirectLlm` mode выбирает direct engine;
- tests, что `ExternalRag` mode выбирает external RAG engine;
- checks, что неизвестный/недоступный mode обрабатывается контролируемо по существующим правилам;
- regression guard, что direct path не требует retrieved context;
- regression guard, что external path сохраняет provider/adapter metadata и retrieved context state, когда adapter их возвращает;
- checks, что UI/PageModel не выбирает provider/adapter напрямую, а передает mode в application service.

**Не входит:**

- новая логика выбора режимов;
- новые режимы анализа;
- provider-specific UI/config behavior;
- вызов real Dify/DeepSeek.

**Ожидаемый diff:**

- targeted tests для selector/registry/application service boundary;
- возможные минимальные test helpers в существующем test project;
- no production changes кроме narrow fix, если текущая реализация явно нарушает утвержденную boundary.

**Проверки:**

- targeted selector/registry tests;
- `dotnet test`;
- `git diff --check`.

**Критерии Done:**

- выбор engine покрыт regression tests;
- `DirectLlm` и `ExternalRag` не подменяют друг друга;
- PageModels/UI не получают прямые зависимости на AI/RAG provider layer.

**Red flags для review:**

- test вызывает concrete provider вместо selector/application service;
- direct mode начинает требовать external adapter;
- Dify-specific детали становятся частью stable application boundary.

## Task 3. Retrieved context state matrix и external adapter error sanitization tests

**Цель:** покрыть все утвержденные states retrieved context и убедиться, что ошибки external adapter не раскрывают secrets/tokens/endpoints.

**Зависимости:** Task 1; Stage 4-7 external adapter/mock behavior; существующая модель warnings/limitations.

**Входит:**

- matrix tests для:
  - `Available`;
  - `MetadataOnly`;
  - `Unavailable`;
  - `Partial`;
- checks, что retrieved context отделен от manual context;
- checks, что limitation/warning сохраняется для unavailable/partial/metadata-only cases;
- tests для external adapter error handling:
  - timeout/unavailable/error response;
  - malformed or incomplete response;
  - failed status with sanitized diagnostics;
- assertions, что diagnostic text не содержит API key, token, Authorization header, secret-like config values или raw sensitive payload;
- подтверждение, что mock/fake adapter не требует network/secrets.

**Не входит:**

- оценка качества retrieval;
- embeddings/rerank/vector DB;
- сохранение полного raw provider response как default behavior;
- production integration tests с реальным внешним provider.

**Ожидаемый diff:**

- targeted tests для retrieved context mapping/state handling;
- targeted tests для adapter error sanitization;
- no committed secrets/config.

**Проверки:**

- targeted retrieved context/error tests;
- `dotnet test`;
- manual diff review for secret-like literals.

**Критерии Done:**

- все четыре retrieved context states покрыты;
- partial/unavailable не маскируются как полноценное основание результата;
- adapter errors сохраняют диагностичность без раскрытия secrets.

**Red flags для review:**

- tests используют реальные токены или endpoint-ы;
- raw external response становится обязательной persisted/export model;
- retrieved context смешивается с manual context.

## Task 4. Export reproducibility matrix для direct/external/legacy saved results

**Цель:** подтвердить, что Markdown/JSON export воспроизводимо строится только из saved result graph для direct, external и legacy MVP-0 results, без повторного анализа или external enrichment.

**Зависимости:** Task 1; Stage 2 export expansion; Stage 7 export smoke checks.

**Входит:**

- export matrix для:
  - saved `DirectLlm` result;
  - saved `ExternalRag` result with retrieved context state;
  - legacy MVP-0 saved result без полной MVP-1 metadata;
- Markdown assertions:
  - mode/engine information;
  - provider/adapter where applicable;
  - manual context information;
  - retrieved context or limitation for external result;
  - expert evaluation/conclusion;
- JSON assertions:
  - stable semantic blocks for mode/engine/provider/adapter;
  - retrieved context state/items/limitations;
  - expert layer;
  - export metadata;
- explicit guard, что export не вызывает `IAiAnalysisEngine`, selector, adapter или provider;
- checks, что legacy result export остается usable и не считается corrupted.

**Не входит:**

- PDF export;
- новая отдельная JSON Schema как public contract;
- повторный запуск AI/RAG/LLM при export;
- Dify-specific raw payload as stable export format.

**Ожидаемый diff:**

- targeted export tests and fixtures;
- no production changes кроме narrow fixes, если export нарушает approved MVP-1 behavior.

**Проверки:**

- targeted export matrix tests;
- `dotnet test`;
- `git diff --check`.

**Критерии Done:**

- direct/external/legacy exports проходят matrix checks;
- export остается saved-result-only operation;
- legacy MVP-0 compatibility сохранена.

**Red flags для review:**

- export вызывает engine/provider/adapter;
- export требует external provider configuration;
- legacy result ломается из-за отсутствия MVP-1 metadata.

## Task 5. Reproducibility/config independence guard для dotnet test и no network/secrets

**Цель:** закрепить, что стандартный `dotnet test` воспроизводим без Dify config, secrets, network и real corporate data.

**Зависимости:** Tasks 2-4; существующие test settings/patterns.

**Входит:**

- tests/guards, что отсутствие Dify configuration не ломает default test run;
- checks, что mock adapter path не читает secrets и не инициирует network;
- source/config guard, что test fixtures не содержат real API keys, Authorization values, corporate names или committed secret-like values;
- проверка, что optional/manual provider checks не входят в default `dotnet test`;
- confirmation, что DeepSeek/Dify calls are not mandatory for architecture/reproducibility tests;
- documentation in test names/comments only where useful to explain optional external provider behavior.

**Не входит:**

- создание нового secret management flow;
- mandatory real Dify/DeepSeek integration test;
- network-blocking framework beyond current project needs;
- load/performance tests.

**Ожидаемый diff:**

- targeted reproducibility/config independence tests;
- possible small test utility for no-network/no-secret guard if it matches existing patterns;
- no new committed secrets/config.

**Проверки:**

- targeted reproducibility tests;
- `dotnet test`;
- manual review of new literals/config/test data;
- `git diff --check`.

**Критерии Done:**

- default test suite does not require external provider configuration;
- no test data contains real corporate/sensitive material;
- optional external provider checks remain optional/manual.

**Red flags для review:**

- `dotnet test` starts failing without Dify endpoint/API key;
- tests commit placeholder that looks like a real secret;
- mock adapter performs real network I/O.

## Task 6. Final architecture/full suite guard и Stage 8 summary

**Цель:** закрыть Stage 8 финальным full-suite gate и зафиксировать результат этапа отдельным summary после выполнения и review Tasks 1-5.

**Зависимости:** Tasks 1-5 completed, reviewed and committed individually.

**Входит:**

- final architecture guard review:
  - UI/PageModels -> application services only;
  - application services -> selector/`IAiAnalysisEngine`;
  - external details stay behind adapter/provider boundary;
  - export -> saved result graph only;
  - mock adapter -> no network/secrets;
- final `dotnet build RequirementImpactAssistant.sln`;
- final `dotnet test RequirementImpactAssistant.sln`;
- `git diff --check`;
- `git status --short`;
- new `docs/program/mvp1/stage-8-summary.md` after all implementation tasks are complete;
- summary of:
  - implemented Stage 8 guards;
  - commits Stage 8;
  - final build/test results;
  - preserved non-goals;
  - remaining optional/manual checks, if any.

**Не входит:**

- implementation before approval of this breakdown;
- new production feature behavior;
- new RAG/retrieval pipeline;
- mandatory external provider integration gate;
- commit/push без отдельной команды.

**Ожидаемый diff:**

- final narrow tests/guards if a gap remains;
- `docs/program/mvp1/stage-8-summary.md` only for documentation part;
- no unrelated changes.

**Проверки:**

- `dotnet build RequirementImpactAssistant.sln`;
- `dotnet test RequirementImpactAssistant.sln`;
- `git diff --check`;
- `git status --short`.

**Критерии Done:**

- Stage 8 architecture/reproducibility tests pass in the default suite;
- full suite is reproducible without Dify/DeepSeek secrets, mandatory network or real corporate data;
- summary accurately records completed tasks and preserved boundaries.

**Red flags для review:**

- summary claims production integration/load/browser E2E coverage that was not approved;
- final suite depends on local secrets/network;
- Stage 8 weakens or deletes Stage 7 smoke coverage.

## Итоговый критерий готовности Stage 8

Stage 8 считается готовой только если:

- все Stage 8 tasks выполнены последовательно и прошли отдельные review;
- selector/registry boundary покрыта regression tests;
- `DirectLlm` и `ExternalRag` mode boundaries защищены;
- retrieved context state matrix покрывает `Available`, `MetadataOnly`, `Unavailable`, `Partial`;
- Markdown/JSON export matrix покрывает direct, external и legacy saved results;
- export не вызывает `IAiAnalysisEngine`, selector, adapter или provider;
- UI/PageModels не имеют прямых AI/RAG provider dependencies;
- mock adapter не требует network/secrets;
- default `dotnet test` не зависит от Dify config/secrets/network;
- external adapter errors sanitize secrets;
- final `dotnet build` и `dotnet test` проходят;
- Stage 8 summary создан после реализации и review всех tasks.

## Открытые вопросы и риски для review breakdown

- Достаточно ли существующих source-token architecture tests для UI/PageModel boundary, или нужен отдельный более строгий dependency scan.
- Нужен ли отдельный guard против случайного добавления optional/manual Dify tests в default test run, если такие tests появятся позже.
- Где лучше держать legacy MVP-0 export fixture: рядом с export tests или в общей Stage 8 fixture зоне.
- Нужно ли в Stage 8 явно проверять отсутствие network через fake handler/test double, или достаточно contract/source guard в текущей архитектуре.
- Какие secret-like patterns считать блокирующими для test fixtures, чтобы guard не давал слишком много false positives.

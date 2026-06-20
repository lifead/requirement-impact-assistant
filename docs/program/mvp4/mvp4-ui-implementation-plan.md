# MVP-4 UI Implementation Plan

## Статус

Этот документ является `implementation plan artifact` для MVP-4 UI.

Документ не является реализацией, не является командой писать код и не вносит изменения в файлы проекта.

План основан на approved документах:

- `docs/program/mvp4/mvp4-ui-requirements-draft.md`;
- `docs/program/mvp4/mvp4-ui-technical-design.md`;
- `docs/program/mvp4/mvp4-ui-technical-design-review.md`.

После этого документа обязателен отдельный `mvp4-ui-implementation-plan-review.md`.

## Prerequisites

Перед началом любой implementation task должны быть выполнены условия:

1. `mvp4-ui-requirements-review.md` имеет статус `PASS`.
2. `mvp4-ui-technical-design-review.md` имеет статус `PASS`.
3. `mvp4-ui-implementation-plan.md` создан и прошел review.
4. Пользователь дал явную команду начать конкретную implementation task.
5. Перед изменением кода проверен актуальный `git status`.
6. Одна task выполняется отдельно: один scope, одна проверка, один review, один commit.

## Implementation Principles

- Не менять domain model, storage, DB schema, migrations, API, routes, state machine, AI/RAG boundary, provider-specific DTO или export format.
- Не добавлять новые requirements или design decisions во время реализации.
- Не превращать MVP-4 в task board, approval workflow, ALM/Jira/Confluence-аналог, RAG-платформу или dashboard.
- UI не должен вызывать LLM/RAG/provider напрямую.
- Запуск анализа остается только через existing `IAnalysisExecutionService`.
- Export остается чтением сохраненного состояния без повторного AI/RAG/LLM-вызова.
- AI/RAG/LLM-result всегда подается как preliminary analytical material.
- Expert evaluation и expert conclusion остаются человеческими действиями.
- Diagnostics доступны для проверки MVP, но не управляют основным пользовательским маршрутом.
- Изменения делать минимальными, преимущественно в Razor UI и текстах, без перестройки архитектуры.

## Task Sequence

### Task 1: Clarify `Analyses Index` Actions

**Scope**

Уточнить смысл действий на списке анализов: `Review` как preflight, `Details` как обзор сохраненного анализа.

**Likely affected files**

- `src/RequirementImpactAssistant.Web/Pages/Analyses/Index.cshtml`
- возможно `src/RequirementImpactAssistant.Web/Pages/Analyses/AnalysisUiText.cs`

**Expected outcome**

Пользователь на списке анализов понимает, что открыть для проверки входных данных, а что открыть для просмотра сохраненного состояния.

**Verification**

- Визуально проверить `/Analyses`.
- Убедиться, что не добавлены routes, statuses, workflow states или новые actions.
- Проверить, что смысл не похож на task board или approval queue.

**Review gate**

После task нужен review результата. Commit только после review `PASS`.

### Task 2: Focus `Review` As Preflight

**Scope**

Сделать экран `Review` более явно ориентированным на проверку input/manual context и запуск preliminary analysis.

**Likely affected files**

- `src/RequirementImpactAssistant.Web/Pages/Analyses/Review.cshtml`
- возможно `src/RequirementImpactAssistant.Web/Pages/Analyses/AnalysisUiText.cs`

**Expected outcome**

Пользователь понимает, что `Review` отвечает на вопрос: достаточно ли данных и контекста для запуска preliminary analysis.

**Verification**

- Проверить `/Analyses/{id}/Review`.
- Убедиться, что экран не стал дубликатом `Details`.
- Убедиться, что expert evaluation и expert conclusion не смешаны с preflight.

**Review gate**

После task нужен review результата. Commit только после review `PASS`.

### Task 3: Rework `Details` Semantic Priority

**Scope**

Упростить смысловую подачу `Details`: overview сохраненного `analysis`, separation of primary/secondary/diagnostic information.

**Likely affected files**

- `src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml`
- возможно `src/RequirementImpactAssistant.Web/Pages/Analyses/AnalysisUiText.cs`

**Expected outcome**

`Details` перестает восприниматься как равноправный raw data dump. Пользователь различает input, preliminary AI material, grounds/limitations, expert evaluation, expert conclusion, export и diagnostics.

**Verification**

- Проверить `/Analyses/{id}` на demo analysis.
- Убедиться, что preliminary AI material не выглядит как expert conclusion.
- Убедиться, что diagnostics не стали основным маршрутом.
- Убедиться, что manual context и retrieved context различимы.

**Review gate**

После task нужен review результата. Commit только после review `PASS`.

### Task 4: Clarify `ExpertEvaluation`

**Scope**

Уточнить экран expert evaluation как человеческую проверку preliminary AI material.

**Likely affected files**

- `src/RequirementImpactAssistant.Web/Pages/Analyses/ExpertEvaluation.cshtml`
- возможно `src/RequirementImpactAssistant.Web/Pages/Analyses/AnalysisUiText.cs`

**Expected outcome**

Эксперт понимает, что AI предложил, что эксперт подтверждает, исправляет, дополняет и какие ограничения учитывает.

**Verification**

- Проверить `/Analyses/{id}/ExpertEvaluation`.
- Убедиться, что экран не создает впечатление повторного AI/provider call.
- Убедиться, что expert actions отделены от preliminary impact map.

**Review gate**

После task нужен review результата. Commit только после review `PASS`.

### Task 5: Preserve Export Boundary Presentation

**Scope**

Проверить и при необходимости уточнить UI-подачу export как сохраненного Markdown/JSON результата без повторного анализа.

**Likely affected files**

- `src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml`
- возможно export-related UI text only

**Expected outcome**

Пользователь понимает, что export использует сохраненное состояние и не запускает новый AI/RAG/LLM-анализ.

**Verification**

- Проверить export actions на `Details`.
- Запустить существующие export boundary tests, если затронуты export-related участки.
- Убедиться, что не добавлен новый export format или PDF.

**Review gate**

После task нужен review результата. Commit только после review `PASS`.

### Task 6: Regression And Demo Smoke Check

**Scope**

Проверить итоговую согласованность UI после всех small tasks.

**Likely affected files**

Файлы не должны меняться без отдельного review finding.

**Expected outcome**

MVP-3 demo scenario проходит через UI: input/manual context -> preliminary result -> expert evaluation -> expert conclusion -> export.

**Verification**

- Проверить Direct LLM demo через existing demo provider.
- Проверить External AI/RAG demo через existing mock adapter или safe fallback.
- Проверить, что no network/secrets required для default development demo.
- Запустить релевантные automated tests.
- Зафиксировать результат review перед финальным commit, если были изменения.

**Review gate**

После regression нужен итоговый review. Дополнительные исправления выполняются отдельными маленькими tasks.

## Verification Per Task

Минимальная проверка каждой implementation task:

- `git diff --stat`;
- ручная проверка затронутого экрана;
- проверка отсутствия изменений вне scope;
- проверка отсутствия новых routes/PageModel/storage/API/DB/state-machine решений;
- проверка AI/expert boundary;
- review результата через отдельного reviewer-а;
- commit только после review `PASS`.

Если task затрагивает export behavior, дополнительно проверить export tests.

Если task затрагивает запуск анализа, дополнительно проверить, что UI по-прежнему использует existing `IAnalysisExecutionService`.

## Review Gates

Для каждой task применяется gate:

1. Implementation task выполнена.
2. Автор task фиксирует измененные файлы и ожидаемый outcome.
3. Reviewer проверяет scope, requirements alignment и boundary preservation.
4. Если review = `NEEDS_CHANGES`, выполняется отдельное исправление.
5. После исправления review повторяется.
6. Commit выполняется только после `PASS`.

Implementation plan считается готовым к исполнению только после отдельного `mvp4-ui-implementation-plan-review.md` со статусом `PASS`.

## Commit Strategy

- Один commit на одну завершенную implementation task.
- Перед каждым commit обязательно показать `git diff --stat`.
- Commit message должен отражать конкретный scope task.
- Не объединять несвязанные UI-изменения в один commit.
- Не коммитить изменения, которые относятся к следующей task.
- Если review выявил исправления в рамках той же task, они входят в commit этой task после повторного `PASS`.

Ожидаемые commit scopes:

1. `Clarify analyses list actions`
2. `Focus review preflight UI`
3. `Improve details semantic hierarchy`
4. `Clarify expert evaluation UI`
5. `Clarify export boundary presentation`
6. `Verify MVP4 UI demo flow`

Названия могут быть уточнены после фактического diff.

## Rollback And Risk Notes

### Risk: UI Changes Become Hidden Workflow

Смягчение: не добавлять новые states, approvals, task actions или workflow language.

### Risk: `Details` Still Too Dense

Смягчение: review проверяет, что diagnostics не равны primary information.

### Risk: AI Result Looks Final

Смягчение: все тексты и структура должны сохранять wording preliminary material и отделять expert conclusion.

### Risk: Manual And Retrieved Context Mixed

Смягчение: review отдельно проверяет происхождение context layers.

### Risk: Offline Demo Depends On Real Provider

Смягчение: implementation не должна менять demo/mock provider behavior или production config.

### Rollback

Rollback выполняется на уровне последнего task commit. Так как tasks disjoint, откат одной task не должен требовать отката всей MVP-4 документационной цепочки.

## Out Of Scope

В рамках этого implementation plan запрещено:

- менять domain model;
- менять storage или DB schema;
- создавать migrations;
- добавлять API endpoints;
- добавлять routes;
- менять PageModel contracts без отдельного approved design update;
- добавлять workflow/state machine;
- добавлять task board или approval workflow;
- добавлять RAG pipeline, embeddings, vector DB или rerank;
- расширять Dify adapter;
- вводить provider-specific DTO в UI/export;
- добавлять новый export format;
- добавлять PDF;
- менять AI/RAG boundary;
- вызывать AI/provider напрямую из UI;
- начинать реализацию без явной команды пользователя на конкретную task.

## Stop Condition Before Code

После создания этого implementation plan следующий шаг - только `mvp4-ui-implementation-plan-review.md`.

После `PASS` по plan review работа останавливается до явной команды пользователя на конкретную implementation task.

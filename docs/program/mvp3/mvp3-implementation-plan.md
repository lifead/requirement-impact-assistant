# MVP-3 implementation plan

## Назначение документа

Документ фиксирует порядок будущей реализации MVP-3 программы анализа влияния проектных запросов.

Это implementation plan. Он не является началом реализации и не разрешает создавать код, тесты, migration, export changes или новые документы без отдельной явной команды на конкретную task.

План опирается на:

- `docs/program/mvp3/mvp3-technical-design.md` как основной источник технических решений;
- `docs/program/mvp3/mvp3-task-breakdown.md` как предварительную нарезку задач;
- `docs/program/mvp3/mvp3-requirements-draft.md`;
- `docs/program/mvp3/mvp3-ui-flow.md`;
- `docs/program/mvp3/mvp3-ux-registration-strategy.md`;
- workflow-правила проекта о phase gates, review и commit.

## Решение по task breakdown

Сохраняем 10 implementation tasks, но уточняем их состав и порядок по technical design.

Причина: исходный breakdown уже дает удобную малую нарезку, но после technical design первая задача должна быть не только терминологической. Persisted `ProjectRequestType` влияет на model, persistence, Create/Edit/Details/Review и Markdown/JSON export, поэтому эту часть нужно выполнить первой и как единую сквозную task. Финальная regression-защита остается отдельной task, но объединяется с registration readiness notes, чтобы закрыть MVP-3 одним проверочным gate.

Итоговый порядок:

1. `ProjectRequestType` model/persistence/Create/Edit/Details/export.
2. UI labels/helper texts.
3. Review page mode clarity.
4. Details page navigation/status summary.
5. Manual context vs retrieved context presentation.
6. Impact map readability.
7. Expert evaluation context.
8. Expert conclusion clarity/passive conclusion types.
9. Demo scenario/checklist.
10. Registration readiness notes + regression assertions.

## Общие gates

Перед стартом каждой task:

- должно быть отдельное явное решение пользователя о начале именно этой task;
- scope task сверяется с этим планом и technical design;
- проверяется текущий `git status`, чужие изменения не откатываются и не смешиваются с задачей;
- task должна давать один проверяемый результат.

После реализации каждой task:

- выполняется build/test gate: минимум `dotnet test RequirementImpactAssistant.sln` для code/test tasks; для documentation-only tasks - review документа на scope, секреты и соответствие technical design;
- выполняется review gate: показывается scope diff, проверяются red flags;
- перед любым commit показывается `git diff --stat`;
- commit gate: один commit на одну task только по явной команде пользователя;
- push не выполняется без отдельной явной команды.

Default checks должны работать offline: без real Dify, DeepSeek, network, user-secrets, секретов и внешних сервисов.

## Матрица migration/export/docs

| Task | Migration | Export update | Documentation-only |
| --- | --- | --- | --- |
| 1. `ProjectRequestType` model/persistence/Create/Edit/Details/export | Да, одна additive migration | Да, Markdown/JSON additive field | Нет |
| 2. UI labels/helper texts | Нет | Нет | Нет |
| 3. Review page mode clarity | Нет | Нет | Нет |
| 4. Details page navigation/status summary | Нет | Нет | Нет |
| 5. Manual context vs retrieved context presentation | Нет | Нет | Нет |
| 6. Impact map readability | Нет | Нет | Нет |
| 7. Expert evaluation context | Нет | Нет | Нет |
| 8. Expert conclusion clarity/passive conclusion types | Нет | Нет | Нет |
| 9. Demo scenario/checklist | Нет | Нет | Да |
| 10. Registration readiness notes + regression assertions | Нет | Нет | Нет, task смешанная: docs + tests |

## Risks / Open questions before implementation

- Для `ProjectRequestType` используются enum values из technical design. Пересмотр enum values возможен только по отдельному решению и review.
- Нужно проверить текущую реализацию export parsing: если backward-safe parsing отсутствует или устроен иначе, task 1 должна добавить минимальные тесты без повышения `FormatVersion`, но не превращаться в переписывание export subsystem.
- Neutral placeholder для новых записей не должен конфликтовать с `Other`: `Other` используется как migration/default только для existing rows, backward compatibility и defensive server-side fallback. Новые записи создаются только через neutral placeholder + required validation, без молчаливого default.
- Helper texts могут стать слишком длинными. Task 2 должна держать тексты централизованными и компактными, без переименования domain/property/db/export names.
- Page tests должны проверять смысловые контракты, а не хрупкую верстку.
- Review page не должна раскрывать endpoint, keys, bearer tokens, cookies, CSRF, raw provider payload или Dify-specific config.
- Demo checklist безопасен, но менее удобен, чем one-click seed. Любая кнопка, route, seed data или fixture runtime - отдельная будущая feature, не MVP-3.
- Registration notes не должны выглядеть как юридическая заявка или маркетинговое описание; это техническая памятка о демонстрируемом функциональном составе.
- Любое обнаруженное желание добавить RAG, embeddings, rerank, workflow, dashboard, PDF, provider или analysis engine должно быть остановлено как out of scope.

## Task 1. `ProjectRequestType` model/persistence/Create/Edit/Details/export

**Цель.** Сохранить тип проектного запроса как устойчивую часть `Analysis`: C# enum, string storage, одна additive migration с default `Other` только для existing rows/backward compatibility, UI с neutral placeholder для новых записей и additive input snapshot + Markdown/JSON export.

**Зависимости.** Завершенный technical design MVP-3; текущая EF/SQLite persistence; существующие Create/Edit/Details/Review pages; существующий Markdown/JSON export.

**Основные файлы/слои.**

- Domain/model: `Analysis`, новый или существующий enum area для `ProjectRequestType`.
- Data: `ApplicationDbContext`, EF string conversion, migration и model snapshot.
- Web input: `AnalysisFormInput`, Create/Edit PageModels и Razor.
- Read views: Details/Review display of saved type.
- Export/input snapshot: Markdown/JSON builders/models/parsing tests и contract включения `ProjectRequestType` в input snapshot.
- Tests: persistence/default, page validation, export presence/backward-safe parsing.

**Ожидаемые проверки.**

- Existing rows после migration получают `Other`.
- Новые Create/Edit forms показывают neutral placeholder и не выбирают `RequirementChange` молча.
- Required validation требует явный выбор для новых записей.
- Новые записи не получают `Other` как молчаливый default; `Other` допустим только как осознанный выбор пользователя или fallback для старых/некорректных данных.
- Defensive server-side fallback корректно обрабатывает отсутствующее/неизвестное значение как `Other`, если это нужно для backward compatibility.
- Input snapshot содержит `ProjectRequestType` согласно technical design.
- Markdown export добавляет тип запроса в `Input`.
- JSON export добавляет `input.projectRequestType` или согласованный аналог без повышения текущего `FormatVersion`.
- Старые exports без `input.projectRequestType` читаются backward-safe.
- `dotnet test RequirementImpactAssistant.sln` проходит offline.

**Review.** Нужен. Особое внимание migration, default/backward compatibility, отсутствию unrelated schema changes и сохранению старых input field names.

**Commit.** Нужен как отдельный commit только после review, `git diff --stat` и явной команды пользователя.

**Red flags.**

- Переименование `OriginalDescription`, `ProjectRequest`, `SituationDescription`, `ChangeSource`, DB columns или export fields.
- `ProjectRequestType` хранится только в UI или вычисляется автоматически из текста.
- Новые записи получают `RequirementChange` без осознанного выбора.
- Повышается `FormatVersion` без необходимости.
- Export начинает включать provider-specific DTO или вызывать AI/RAG повторно.
- Migration меняет что-то кроме additive `ProjectRequestType`.

## Task 2. UI labels/helper texts

**Цель.** Уточнить терминологию входных данных без переименования domain/property/db/export names: текущая точка отсчета, проектное изменение, ситуация/причина, источник изменения.

**Зависимости.** Task 1, чтобы labels учитывали новый `ProjectRequestType`.

**Основные файлы/слои.**

- Centralized UI text helper: `AnalysisUiText` или ближайший существующий аналог.
- Create/Edit/Details/Review Razor pages.
- Page/source tests на устойчивые тексты.

**Ожидаемые проверки.**

- Labels/helper texts централизованы, а не размазаны inline-строками.
- Существующие property/db/export names не переименованы.
- Тексты не обещают, что проектное изменение уже принято.
- `dotnet test RequirementImpactAssistant.sln` проходит offline.

**Review.** Нужен. Проверить читабельность текстов и отсутствие scope creep.

**Commit.** Нужен как отдельный commit только после review, `git diff --stat` и явной команды пользователя.

**Red flags.**

- Migration или export update ради label changes.
- Новая localization system без необходимости.
- UI начинает описывать AI/RAG как субъект управленческого решения.

## Task 3. Review page mode clarity

**Цель.** Сделать Review page ясной перед запуском анализа: выбранный режим, provider-neutral пояснение DirectLlm/ExternalRag, отсутствие real health-check при GET.

**Зависимости.** Tasks 1-2.

**Основные файлы/слои.**

- `Review.cshtml`/`Review.cshtml.cs`.
- Safe provider-neutral helper/view model, если уже есть подходящее место.
- Tests на application-level boundary.

**Ожидаемые проверки.**

- Запуск анализа идет только через `IAnalysisExecutionService`.
- PageModel не зависит напрямую от `DifyExternalRagAdapter`, `DifyExternalRagOptions`, `IExternalRagAdapter`, `ILlmProvider`, `HttpClient` или provider DTO.
- GET Review page не вызывает сеть и не требует secrets.
- UI не выводит endpoint, API key, bearer token, cookies, CSRF или raw provider payload.
- `dotnet test RequirementImpactAssistant.sln` проходит offline.

**Review.** Нужен. Проверить provider-neutral wording и boundary.

**Commit.** Нужен как отдельный commit только после review, `git diff --stat` и явной команды пользователя.

**Red flags.**

- Прямые Dify dependencies в Razor Page/PageModel.
- Health-check внешнего provider при открытии страницы.
- External AI/RAG выглядит как обязательная зависимость для direct scenario.

## Task 4. Details page navigation/status summary

**Цель.** Добавить на Details page компактную смысловую навигацию и status summary по слоям: input, manual context, preliminary result, grounds/limitations, expert evaluation, expert conclusion, export.

**Зависимости.** Tasks 1-3.

**Основные файлы/слои.**

- Details Razor page and PageModel helpers.
- Existing status/result/evaluation/conclusion data.
- Page/source tests.

**Ожидаемые проверки.**

- Summary использует существующие statuses/data и ничего не запускает автоматически.
- Переходы ведут только к существующим pages/actions.
- Нет новых workflow states, taskboard, approval route или dashboard.
- `dotnet test RequirementImpactAssistant.sln` проходит offline.

**Review.** Нужен. Проверить, что status summary пассивен и не создает workflow semantics.

**Commit.** Нужен как отдельный commit только после review, `git diff --stat` и явной команды пользователя.

**Red flags.**

- Новые workflow statuses без отдельного решения.
- Автоматический запуск analysis/evaluation/export из summary.
- UI обещает согласование, назначение или выполнение задач.

## Task 5. Manual context vs retrieved context presentation

**Цель.** Развести ручной контекст карточки анализа и retrieved context, возвращенный external AI/RAG provider, включая состояния `Unavailable`, `Partial`, `MetadataOnly` там, где они применимы.

**Зависимости.** Task 4.

**Основные файлы/слои.**

- Details page sections/partials.
- Existing `ContextFragment`, `RetrievedContextItem`, analysis result metadata.
- Page/source tests.

**Ожидаемые проверки.**

- Manual context показан как введенный пользователем материал карточки.
- Retrieved context показан как сохраненный результат внешнего контура для конкретного анализа.
- Для `DirectLlm` не создается искусственный retrieved context.
- Для `ExternalRag` честно показывается saved retrieved context или состояние отсутствия/частичности/metadata-only.
- `dotnet test RequirementImpactAssistant.sln` проходит offline.

**Review.** Нужен. Проверить отсутствие смешивания источников контекста.

**Commit.** Нужен как отдельный commit только после review, `git diff --stat` и явной команды пользователя.

**Red flags.**

- RAG knowledge base представлена как внутренняя база приложения.
- UI подменяет отсутствующий retrieved context выдуманными источниками.
- Dify-specific DTO протекают в UI/domain/export.

## Task 6. Impact map readability

**Цель.** Улучшить читаемость structured `ImpactMap` как preliminary analytical material: затронутые элементы, риски, противоречия, вопросы и ограничения должны быть легче просматриваемы.

**Зависимости.** Task 5.

**Основные файлы/слои.**

- Details page impact map rendering.
- Existing structured impact map models.
- Page/source tests.

**Ожидаемые проверки.**

- Карта влияния остается preliminary material, а не экспертным решением.
- Free-form AI text, если есть, остается вспомогательным диагностическим материалом.
- Нет изменений prompt contract, response schema, analysis engines или providers.
- `dotnet test RequirementImpactAssistant.sln` проходит offline.

**Review.** Нужен. Проверить, что улучшение presentation не стало model/provider refactor.

**Commit.** Нужен как отдельный commit только после review, `git diff --stat` и явной команды пользователя.

**Red flags.**

- Изменен prompt/provider contract ради UI.
- Impact map начинает выглядеть как утвержденное решение.
- Добавлен dashboard или исследовательская панель.

## Task 7. Expert evaluation context

**Цель.** Дать эксперту контекст перед оценкой: входные данные, preliminary impact map, основания/ограничения, retrieved context state и warning/limitation notes без вызова analysis engine.

**Зависимости.** Task 6.

**Основные файлы/слои.**

- ExpertEvaluation page and PageModel.
- Existing saved `ImpactMap`, metadata, evaluation model.
- Tests на отсутствие engine/provider side effects.

**Ожидаемые проверки.**

- ExpertEvaluation работает только с сохраненным результатом.
- Сохранение экспертной оценки не меняет `AiAnalysisResult`.
- Сохранение экспертной оценки не вызывает provider/adapter/analysis engine.
- `dotnet test RequirementImpactAssistant.sln` проходит offline.

**Review.** Нужен. Проверить отделение human evaluation от AI/RAG result.

**Commit.** Нужен как отдельный commit только после review, `git diff --stat` и явной команды пользователя.

**Red flags.**

- Экспертная оценка запускает повторный анализ.
- Evaluation смешивается с исходным AI/RAG output.
- Появляются workflow actions, assignees или notifications.

## Task 8. Expert conclusion clarity/passive conclusion types

**Цель.** Уточнить UX экспертного заключения так, чтобы `SplitIntoSeveralTasks` и `ReturnForReanalysis` оставались пассивными conclusion types, а не действиями системы.

**Зависимости.** Task 7.

**Основные файлы/слои.**

- ExpertConclusion page and PageModel.
- `AnalysisUiText` или аналог для conclusion labels/helper texts.
- Tests на passive behavior.

**Ожидаемые проверки.**

- Заключение фиксирует человек.
- Conclusion types не создают задачи, уведомления, workflow, внешние requests или automatic reanalysis.
- Сохранение заключения не меняет `AiAnalysisResult` и не вызывает provider.
- `dotnet test RequirementImpactAssistant.sln` проходит offline.

**Review.** Нужен. Проверить формулировки и side effects.

**Commit.** Нужен как отдельный commit только после review, `git diff --stat` и явной команды пользователя.

**Red flags.**

- Появились entities/tasks/notifications/assignees.
- `ReturnForReanalysis` запускает анализ автоматически.
- UI обещает автоматическое исполнение экспертного вывода.

## Task 9. Demo scenario/checklist

**Цель.** Создать documentation-only demo checklist для воспроизводимого MVP-3 показа на обезличенных данных, без runtime seed, route, кнопки, real Dify requirement, secrets или внешних зависимостей.

**Зависимости.** Tasks 1-8.

**Основные файлы/слои.**

- Новый docs artifact в `docs/program/mvp3/`, например `mvp3-demo-smoke-checklist.md`.
- Никаких изменений в `src/`, `tests/`, project files, appsettings, migrations.

**Ожидаемые проверки.**

- Checklist содержит один реалистичный обезличенный кейс.
- Шаги различают current state, project change, manual context, retrieved context, preliminary result, expert evaluation, expert conclusion, Markdown/JSON export.
- DirectLlm через demo/mock provider и ExternalRag через mock adapter описаны как offline/default-safe варианты.
- Real Dify smoke, если упомянут, помечен как optional manual scenario вне default gate.
- Документ не содержит secrets, private data, bearer values, cookies, CSRF, endpoint с чувствительными параметрами или real provider payload.

**Review.** Нужен. Проверить безопасность данных и отсутствие расширения product scope.

**Commit.** Нужен как отдельный commit только после review, `git diff --stat` и явной команды пользователя.

**Red flags.**

- Документ требует Dify/DeepSeek/network как обязательный gate.
- Появляется seed data, route, browser automation framework или UI button.
- Demo превращается в новую integration/RAG/workflow feature.

## Task 10. Registration readiness notes + regression assertions

**Цель.** Закрыть MVP-3 notes для регистрационной готовности и добавить финальные точечные regression assertions, защищающие границы MVP-3 после UX/export изменений.

**Зависимости.** Tasks 1-9.

**Основные файлы/слои.**

- Documentation artifact в `docs/program/mvp3/` или `docs/review/` с кратким описанием демонстрируемого функционального состава.
- Existing test project/source assertions для boundaries.
- Без новых migrations, appsettings, providers, dashboards, PDF или workflow.

**Ожидаемые проверки.**

- Notes описывают программу как assistant for preliminary analytical material, не как субъект управления.
- Dify описан только как optional concrete provider за adapter boundary, не как публичная модель программы.
- Known limitations перечислены честно.
- Regression assertions подтверждают, что pages/export не зависят напрямую от Dify adapter/options, provider DTO, `IExternalRagAdapter`, `ILlmProvider`, `HttpClient`, network или secrets.
- Export services не вызывают analysis engines/provider adapters.
- Default `dotnet test RequirementImpactAssistant.sln` проходит offline.

**Review.** Нужен. Проверить точность регистрационных формулировок и фокус regression tests.

**Commit.** Нужен как отдельный commit только после review, `git diff --stat` и явной команды пользователя.

**Red flags.**

- Notes становятся юридической заявкой или маркетинговым текстом.
- Программа описана как принимающая управленческие решения.
- Regression task превращается в большой refactor или новую test infrastructure.
- Tests зависят от local Dify, real keys, user-secrets или network.

## Итоговые acceptance criteria MVP-3 implementation

- `ProjectRequestType` persisted in `Analysis` как enum/string storage, с additive migration и default `Other` только для existing rows/backward compatibility/defensive server-side fallback.
- Create/Edit для новых записей требуют осознанный выбор типа через neutral placeholder, без молчаливого `RequirementChange`.
- Domain/property/db/export names существующих input fields не переименованы.
- Labels/helper texts централизованы в `AnalysisUiText` или согласованном аналоге.
- Details/Review/Expert pages ясно разделяют input, manual context, retrieved context, preliminary AI/RAG/LLM material, grounds/limitations, expert evaluation и expert conclusion.
- Input snapshot и Markdown/JSON export additive содержат `input.projectRequestType` или согласованный аналог без повышения current `FormatVersion`.
- Demo mechanism для MVP-3 остается documentation checklist only.
- No new RAG/provider/analysis engine/workflow/dashboard/PDF.
- Default build/tests проходят offline.
- Каждая task прошла build/test/review/commit gate; commit выполнен только при явной команде пользователя.

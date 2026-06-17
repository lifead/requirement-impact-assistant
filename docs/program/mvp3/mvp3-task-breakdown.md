# MVP-3 task breakdown

## Назначение документа

Документ фиксирует предварительную нарезку будущей реализации MVP-3 на маленькие проверяемые task.

Это не реализация и не команда начинать кодирование. Документ может использоваться как материал для будущего implementation plan и review, но не заменяет phase gates. Перед стартом любой task нужно отдельно подтвердить, что пройдены необходимые phase gates MVP-3: требования, review требований, технический проект, review технического проекта и implementation plan/review, а также есть явное решение о начале конкретной task.

Главный принцип:

```text
одна task -> один проверяемый результат -> один обозримый diff -> один review -> один commit
```

## Основание

Breakdown подготовлен после чтения текущей структуры solution, domain/application/data/pages/export/test layers и документов:

- workflow docs;
- MVP-0 documents and review package;
- MVP-1 strategy, requirements, technical design, implementation/stage docs and stabilization docs;
- MVP-2 Dify Agent notes, task breakdown and manual smoke checklist;
- MVP-3 UX and registration strategy.

Текущая кодовая база уже содержит:

- `Analysis`, `ContextFragment`, `AiAnalysisResult`, `AiAnalysisResultMetadata`, `RetrievedContextItem`, `ExpertEvaluation`, `ExpertConclusion`;
- SQLite persistence через `ApplicationDbContext` и EF migrations;
- страницы списка, создания, редактирования, деталей, review, экспертной оценки и экспертного заключения;
- `DirectLlmAnalysisEngine`, `ExternalRagAnalysisEngine`, `IAiAnalysisEngineSelector`;
- `MockExternalRagAdapter` и `DifyExternalRagAdapter` через `IExternalRagAdapter`;
- Markdown/JSON export сохраненного результата;
- защиту оцененного/экспортированного результата от незаметного повторного анализа.

## Границы MVP-3 breakdown

Входит:

- доведение пользовательского пути до смысловой ясности;
- уточнение UI-терминологии вокруг текущего состояния, проектного изменения, ручного контекста, retrieved context, предварительного результата и экспертного решения;
- явное разделение контекстов: manual context принадлежит карточке анализа; RAG knowledge base остается внешней базой provider-а; retrieved context является только возвращенными provider-ом фрагментами и/или metadata для конкретного анализа;
- улучшение демонстрационной готовности без нового внешнего provider-а;
- минимальные regression checks, чтобы UX-изменения не нарушили `IAiAnalysisEngine`, provider/adapter boundaries, export и экспертный слой;
- подготовка демонстрационного сценария и регистрационной памятки как документационных артефактов.

Не входит:

- новый RAG, embeddings, vector database, rerank или собственный retrieval pipeline;
- новая интеграция с Jira, Confluence, GitLab, ALM, почтой, чатами или workflow-системами;
- dashboard, task board, backlog, sprint/workflow platform;
- PDF export;
- полный редизайн UI одной большой задачей;
- изменение Dify adapter сверх уже согласованного MVP-2 streaming scope;
- Dify-specific DTO в UI, domain model, export или экспертной оценке;
- автоматическое принятие, отклонение, эскалация изменения, создание задач, PR или изменение требований/API/кода.

## Implementation tasks

### Task 1. Уточнить терминологию входных данных

**Цель.** Сделать текущую точку отсчета и проектное изменение понятными пользователю без изменения доменной схемы и БД.

**Зависимости.** Утвержденные формулировки MVP-3 по различию "текущее состояние" и "проектное изменение"; существующие Create/Edit/Details/Review pages.

**Входит.**

- Обновить UI-labels и validation messages для существующих полей `OriginalDescription` и `ProjectRequest`, чтобы они читались как "текущее состояние / исходная точка" и "проектное изменение / запрос".
- Сохранить текущие property names, EF mapping, migrations и JSON/Markdown export contract без переименования.
- Обновить page tests/source assertions на новые labels.
- Проверить, что существующие данные открываются без миграции.

**Не входит.**

- Переименование C# properties, колонок, таблиц или JSON fields.
- Новая сущность "semantic diff".
- Новый экран создания анализа.
- Изменение DirectLlm/ExternalRag/Dify.

**Ожидаемый diff.**

- Небольшие изменения в `Pages/Analyses/Create.cshtml`, `Edit.cshtml`, `Details.cshtml`, `Review.cshtml`, `AnalysisFormInput.cs` или `AnalysisUiText.cs`.
- Точечные обновления `AnalysisPagesTests`.
- Без изменений migrations, `.csproj`, `appsettings*.json`, export builders.

**Проверки.**

- `dotnet test RequirementImpactAssistant.sln`.
- Page tests подтверждают новые labels и прежнюю запись/чтение `Analysis`.
- Manual smoke: создать анализ, увидеть понятные поля текущего состояния и изменения.

**Критерии Done.**

- Пользователь видит различие текущей точки отсчета и предлагаемого изменения.
- Сохранение, редактирование и открытие анализа работают на прежней модели данных.
- Export contract не изменен.

**Red flags для review.**

- Появилась migration только ради переименования label.
- JSON/Markdown export сломан или переименован без отдельного решения.
- UI начинает описывать проектное изменение как уже принятое управленческое решение.

### Task 2. Показать режим анализа и доступность external AI/RAG перед запуском

**Цель.** Сделать выбор `DirectLlm` / `ExternalRag` осознанным и честно показать, что external AI/RAG может идти через mock fallback или Dify, не вызывая provider заранее.

**Зависимости.** Task 1; текущие `ReviewModel`, `AiAnalysisEngineSelector`, DI fallback на `MockExternalRagAdapter` и provider-neutral read-only status/helper на application-level boundary.

**Входит.**

- Read-only отображение на Review page: выбранный режим, локальный Direct LLM путь, external AI/RAG availability note.
- Пояснение, что external AI/RAG может передать данные во внешний контур только в рамках application-level запуска.
- Отображение provider-neutral availability/status на уровне безопасной application-level информации, без вывода секретов и provider-specific config keys.
- Regression tests, что Review page по-прежнему вызывает только `IAnalysisExecutionService`, а не adapter/provider напрямую.

**Не входит.**

- Вызов Dify/DeepSeek для проверки доступности.
- UI для настройки endpoint/API key.
- Новый provider registry.
- Изменение `appsettings*.json`.

**Ожидаемый diff.**

- Небольшое расширение `Review.cshtml`/`Review.cshtml.cs`.
- Возможно, маленькая view model/read-only helper для безопасного статуса режима.
- Tests в `AnalysisPagesTests` или configuration tests.

**Проверки.**

- `dotnet test RequirementImpactAssistant.sln` без сети и секретов.
- Tests подтверждают, что real provider не вызывается при открытии Review page.
- Manual smoke: Direct LLM остается default; External AI/RAG виден как отдельный выбор.

**Критерии Done.**

- Перед запуском понятно, какой режим будет использован.
- Отсутствие Dify configuration не выглядит как поломка direct сценария.
- Секреты и endpoint values не выводятся в UI.

**Red flags для review.**

- Review page начинает зависеть от `DifyExternalRagAdapter`, `HttpClient` или provider DTO.
- Razor Page/PageModel начинает зависеть от `DifyExternalRagOptions`, `IOptions<...Dify...>` или config keys `ExternalRag:Dify:*`.
- Появляется network health check в обычном GET.
- В UI попадают API key, bearer token, raw endpoint с чувствительными параметрами.

### Task 3. Компактная навигация по смысловым этапам анализа

**Цель.** Дать пользователю простой ориентир: ввод -> контекст -> предварительный анализ -> экспертная оценка -> экспертное заключение -> export.

**Зависимости.** Task 1; существующие statuses и pages.

**Входит.**

- Добавить компактный step/status summary на Details page и, при необходимости, Review/Expert pages.
- Использовать существующие `AnalysisStatus`, наличие `AiAnalysisResult`, `ExpertEvaluation`, `ExpertConclusion`.
- Дать переходы только к уже существующим страницам и действиям.
- Сохранить пассивность статусов: summary ничего не запускает автоматически.

**Не входит.**

- Workflow engine.
- Approval route.
- Новые статусы без отдельного решения.
- Full UI redesign или новый layout всего приложения.

**Ожидаемый diff.**

- Точечные изменения Razor markup в `Pages/Analyses/*`.
- Возможно, helper methods в `AnalysisUiText`.
- Page source tests на отсутствие workflow/action side effects.

**Проверки.**

- `dotnet test RequirementImpactAssistant.sln`.
- Manual smoke: открыть анализ на разных состояниях и проверить корректные активные/неактивные шаги.

**Критерии Done.**

- Пользователь понимает, где он находится в сценарии.
- Шаги не создают задач, согласований или скрытых автоматических действий.
- Существующие маршруты работают без новых endpoint-ов.

**Red flags для review.**

- Появились workflow terms вроде approval, assignee, escalation.
- Stepper меняет статус анализа при простом просмотре.
- Большой CSS/layout refactor смешан с логикой.

### Task 4. Разделить ручной контекст и retrieved context в просмотре

**Цель.** Сделать на Details page более явным различие manual context и external retrieved context без возврата file upload UI.

**Зависимости.** Task 3; текущие `ContextFragments`, `AiAnalysisResult.Metadata.RetrievedContextItems`.

**Входит.**

- Отдельные компактные summary-блоки для ручного контекста и retrieved context.
- Явное пояснение: manual context принадлежит карточке анализа; RAG knowledge base остается внешней базой provider-а и не является новой базой знаний приложения; retrieved context - только возвращенные provider-ом фрагменты и/или metadata для этого анализа.
- Явное состояние: ручной контекст добавлен/не добавлен; retrieved context available/partial/metadataOnly/unavailable.
- Улучшение empty states и limitation text.
- Tests на отображение direct LLM результата без искусственного retrieved context и external result с retrieved context.

**Не входит.**

- Возврат live file upload.
- Импорт документов, embeddings, indexing, search.
- Изменение persistence модели retrieved context.
- Изменение export builders.

**Ожидаемый diff.**

- Изменения `Details.cshtml` и, возможно, display records в `Details.cshtml.cs`.
- Небольшие tests в `AnalysisPagesTests`.
- Без изменений `ApplicationDbContext`, migrations, Dify adapter, export.

**Проверки.**

- `dotnet test RequirementImpactAssistant.sln`.
- Source test подтверждает, что upload UI не вернулся.
- Manual smoke для direct LLM и mock external RAG.

**Критерии Done.**

- Manual context и retrieved context визуально и смыслово не смешаны.
- Отсутствие retrieved context показано как limitation, а не как экспертное решение.
- File upload surface остается отсутствующим до отдельного решения.

**Red flags для review.**

- Retrieved context отображается как доказательство правильности результата.
- Adapter-specific Dify event fields попали в UI.
- В задачу незаметно добавлен upload, storage service или retrieval pipeline.

### Task 5. Улучшить читаемость карты влияния

**Цель.** Сделать структурированную карту влияния пригодной для экспертного просмотра без большого редизайна.

**Зависимости.** Task 4; существующий `ImpactMap` и `DetailsModel.AiAnalysisResultDetails.ImpactSections`.

**Входит.**

- Маленькие UI-улучшения для секций карты влияния: anchors/section summary/empty-state wording или компактная группировка.
- Сохранить таблицы или существующую структуру, если она достаточна; улучшать только читаемость и навигацию.
- Показать, что карта влияния является preliminary analytical material.
- Tests/source assertions на наличие ключевых секций.

**Не входит.**

- Новый визуальный framework.
- Полный redesign Details page.
- Изменение `ImpactMap` model или AI response contract.
- Автоматическая оценка качества результата.

**Ожидаемый diff.**

- Только Razor/helper изменения и tests.
- Без изменений analysis engines, provider adapters, migrations и export.

**Проверки.**

- `dotnet test RequirementImpactAssistant.sln`.
- Manual smoke: карта читается на saved direct и saved external result.

**Критерии Done.**

- Эксперт может быстро найти риски, противоречия, вопросы, недостающую информацию и варианты рассмотрения.
- Empty sections не выглядят как ошибка.
- Предварительный характер результата сохранен.

**Red flags для review.**

- Карта превращена в чат или свободный LLM transcript.
- UI скрывает warnings/limitations ради "красивого" результата.
- Затронуты Dify mapper или prompt contract без необходимости.

### Task 6. Контекст экспертной оценки перед сохранением

**Цель.** На странице экспертной оценки показать, какой AI/RAG/LLM результат и какой режим оценивает человек.

**Зависимости.** Task 5; текущая `ExpertEvaluation` page.

**Входит.**

- Read-only summary на ExpertEvaluation page: analysis mode, engine/provider/adapter, retrieved context state, warnings count.
- Короткое напоминание, что экспертная оценка является человеческим слоем.
- Сохранение существующей формы экспертных отметок без изменения domain model.
- Tests, что сохранение evaluation не вызывает analysis engine/provider.

**Не входит.**

- Новые типы экспертных оценок.
- Расширение `ExpertEvaluation` schema.
- Автоматическое формирование экспертных выводов.
- Повторный запуск анализа со страницы оценки.

**Ожидаемый diff.**

- `ExpertEvaluation.cshtml(.cs)` и page tests.
- Возможно, небольшие helper labels.
- Без migrations и export changes.

**Проверки.**

- `dotnet test RequirementImpactAssistant.sln`.
- Page tests: evaluation сохраняется как раньше, AI result snapshot не меняется.

**Критерии Done.**

- Эксперт видит, что именно оценивает.
- Экспертное действие не смешивается с preliminary AI/RAG/LLM result.
- Snapshot lock behavior остается прежним.

**Red flags для review.**

- Форма оценки начинает предлагать AI-generated expert marks.
- Сохранение оценки вызывает `IAiAnalysisEngine`.
- Изменяется формат export без отдельного scope.

### Task 7. Ясность экспертного заключения и пассивных conclusion types

**Цель.** Уточнить UX экспертного заключения так, чтобы варианты "разделить" и "вернуть на повторный анализ" оставались только человеческой фиксацией вывода.

**Зависимости.** Task 6; текущая `ExpertConclusion` page и tests на passive workflow conclusion types.

**Входит.**

- Read-only summary перед сохранением заключения: оценка уже зафиксирована человеком, заключение не запускает workflow.
- Точечные тексты для passive conclusion types.
- Tests, что `SplitIntoSeveralTasks` и `ReturnForReanalysis` не создают задач, уведомлений, внешних действий или повторного анализа.

**Не входит.**

- Workflow согласования.
- Создание задач.
- Уведомления.
- Разблокировка повторного анализа после экспертной фиксации.

**Ожидаемый diff.**

- `ExpertConclusion.cshtml(.cs)`, `AnalysisUiText.cs`, page tests.
- Без изменений application analysis service, adapters, export.

**Проверки.**

- `dotnet test RequirementImpactAssistant.sln`.
- Manual smoke: сохранить каждый passive conclusion type и убедиться, что это только сохраненное значение.

**Критерии Done.**

- Заключение выглядит как человеческая фиксация, а не автоматическое действие системы.
- Existing single-conclusion update behavior сохранен.
- Snapshot protection не ослаблена.

**Red flags для review.**

- Появились entities/tasks/notifications/assignees.
- Conclusion type запускает новый workflow.
- UI обещает автоматическое исполнение экспертного вывода.

### Task 8. Демонстрационный сценарий MVP-3 на обезличенных данных

**Цель.** Зафиксировать воспроизводимый сценарий, который показывает смысловой diff и полный путь анализа без реальных corporate data, Dify/DeepSeek secrets и сети.

**Зависимости.** Tasks 1-7.

**Входит.**

- Новый документ smoke/checklist для MVP-3 в `docs/program/mvp3/`.
- Один реалистичный обезличенный кейс: текущее состояние, проектное изменение, ручной контекст, expected review focus.
- Явное различение в сценарии: manual context принадлежит карточке анализа; RAG knowledge base остается внешней базой provider-а; retrieved context - только возвращенные provider-ом фрагменты и/или metadata для конкретного анализа.
- Шаги для Direct LLM через demo provider и External AI/RAG через mock adapter.
- Проверка экспертной оценки, экспертного заключения, Markdown/JSON export как уже существующих функций.
- Явный список того, что optional/manual для real Dify и что не входит в default checks.

**Не входит.**

- Production seed data.
- Обязательный real Dify smoke.
- Browser automation framework.
- Dashboard или исследовательские метрики.

**Ожидаемый diff.**

- Новый docs-файл, например `docs/program/mvp3/mvp3-demo-smoke-checklist.md`.
- Возможно, обновление текущего task breakdown только если scope изменился до реализации.
- Без changes в `src/`, `tests/`, project files.

**Проверки.**

- Документ проходит review на отсутствие секретов и реальных corporate data.
- Шаги соответствуют текущему UI и не требуют сети.

**Критерии Done.**

- Демонстратор может пройти полный сценарий без помощи разработчика.
- Сценарий показывает различие current state, project change, manual context, retrieved context, preliminary result и expert decision.
- Checklist не расширяет продуктовый scope.

**Red flags для review.**

- В документ попали реальные secrets, cookies, CSRF, bearer values или приватные данные.
- Smoke требует Dify/DeepSeek как обязательный gate.
- Документ предлагает новые integrations или RAG pipeline.

### Task 9. Точечные regression assertions для MVP-3 UX boundaries

**Цель.** Точечно защитить MVP-3 UX-изменения от размывания архитектурных границ без превращения задачи в общий refactor или полный audit.

**Зависимости.** Tasks 1-7; существующие architecture regression tests.

**Входит только как точечные regression assertions.**

- Tests/source assertions, что Razor Pages не зависят от Dify adapter, `IExternalRagAdapter`, `ILlmProvider`, `HttpClient`.
- Tests, что export services не вызывают analysis engines/provider adapters.
- Tests, что Details/Review/Expert pages не возвращают file upload UI.
- Tests на сохранение direct/external/mocked scenarios без сети.
- Tests на отсутствие workflow/task-board language в critical UI texts, если такой подход согласован.

**Не входит.**

- Новая test infrastructure.
- Playwright/browser E2E.
- Network integration tests.
- Полная security audit.

**Ожидаемый diff.**

- Точечные test additions в существующем test project.
- Возможно, небольшие helpers в tests support.
- Без production code кроме минимальных поправок, если tests выявили расхождение в рамках MVP-3.

**Проверки.**

- `dotnet test RequirementImpactAssistant.sln` проходит offline.
- Поиск по diff не находит secrets или Dify-specific DTO в UI/domain/export.

**Критерии Done.**

- Главные boundaries MVP-0/MVP-1/MVP-2 сохранены после UX-изменений.
- Default tests не требуют Dify, DeepSeek, user-secrets или network.

**Red flags для review.**

- Regression task превращается в большой refactor.
- Тесты зависят от локального Dify или реальных ключей.
- Page tests закрепляют хрупкую верстку вместо смысловых контрактов.

### Task 10. MVP-3 registration readiness notes

**Цель.** Подготовить краткий пакет описания программы как демонстрируемого программного артефакта без юридической заявки и без привязки к Dify-specific деталям.

**Зависимости.** Tasks 1-9.

**Входит.**

- Документ в `docs/program/mvp3/` или `docs/review/` с кратким описанием функционального состава программы.
- Отдельное указание: AI/RAG/LLM формирует preliminary analytical material, экспертное решение остается за человеком.
- Список реализованных режимов: DirectLlm, ExternalRag через adapter boundary, Dify как optional конкретный provider.
- Список демонстрационных проверок и known limitations.
- Проверка, что описание не обещает workflow, Jira/Confluence/GitLab integrations, dashboard, PDF, собственный RAG или автоматическое управленческое действие.

**Не входит.**

- Юридическая заявка на регистрацию.
- Изменение экранов, export formats или доменной модели.
- Маркетинговый сайт или landing page.
- Упаковка инсталлятора.

**Ожидаемый diff.**

- Один documentation artifact.
- Возможно, ссылка из `docs/program/mvp3/mvp3-task-breakdown.md` после завершения, если решено вести index.
- Без `src/`, `tests/`, `.csproj`, appsettings, migrations.

**Проверки.**

- Документ review на точность относительно текущей кодовой базы.
- Проверка, что Dify описан как внешний provider за adapter boundary, а не как ядро программы.

**Критерии Done.**

- Есть короткое, демонстрационно пригодное описание программы для обсуждения регистрации и диссертационного показа.
- Известные ограничения перечислены честно.
- Нет обещаний функциональности, которой нет в программе.

**Red flags для review.**

- Документ превращается в юридическую заявку или рекламный текст.
- Описание приписывает программе принятие управленческих решений.
- Dify-specific internals описаны как stable public model программы.

## Рекомендуемый порядок

1. Task 1 - терминология входных данных.
2. Task 2 - режим анализа и доступность external AI/RAG.
3. Task 3 - компактная навигация по этапам.
4. Task 4 - разделение manual context и retrieved context.
5. Task 5 - читаемость карты влияния.
6. Task 6 - контекст экспертной оценки.
7. Task 7 - ясность экспертного заключения.
8. Task 8 - демонстрационный сценарий MVP-3.
9. Task 9 - regression checks для UX boundaries.
10. Task 10 - registration readiness notes.

## Общие правила review для каждой task

- Diff соответствует только scope task.
- Нет unrelated refactor.
- Нет изменений в Dify/DeepSeek/provider code, если task не про это явно.
- Нет новых migrations или schema changes без отдельного technical design.
- Нет secrets, real provider payloads, browser traces с токенами или локальных БД.
- `dotnet test RequirementImpactAssistant.sln` проходит, если task затрагивает код или tests.
- Перед commit показывается `git diff --stat`.
- Commit/push выполняются только по явной команде пользователя.

## Количество implementation tasks

Всего в breakdown: 10 implementation tasks.

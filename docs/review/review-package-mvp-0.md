# Review package MVP-0

## Название проекта

**requirement-impact-assistant** - MVP-0 программы анализа влияния проектных запросов.

## Git-сводка

Текущая ветка:

```text
main
```

Последний commit:

```text
ed203d9 Translate UI to Russian
```

`git status` до подготовки review package:

```text
On branch main
Your branch is up to date with 'origin/main'.

nothing to commit, working tree clean
```

`git log --oneline -5`:

```text
ed203d9 Translate UI to Russian
193c089 Fix SQLite database path resolution
cb271db Add end-to-end smoke checklist
9349dd9 Clarify smoke scenario task scope
e5f5f97 Protect evaluated analysis snapshots
```

`git diff --stat` до подготовки review package:

```text

```

Незакоммиченных изменений до подготовки пакета не было. После подготовки ожидаемо появляются только новые review-артефакты: `docs/review/review-package.md` и архив `requirement-impact-assistant-mvp0-review.zip`. Commit и push для этой задачи не выполняются.

## Краткое назначение MVP-0

MVP-0 помогает эксперту подготовить предварительный аналитический материал по проектному запросу: исходное требование или описание, предлагаемое изменение, ситуация, источник изменения, вручную подобранный контекст, карта влияния, экспертная оценка, экспертное заключение и экспорт результата.

Ключевое ограничение: LLM/AI не принимает управленческое решение. Она формирует предварительную карту влияния и аналитические подсказки, а экспертное заключение фиксирует человек.

## Что фактически реализовано

- ASP.NET Core 8 Razor Pages web-приложение с SQLite-хранилищем.
- Создание, редактирование, список и просмотр анализов.
- Ручное добавление контекста и загрузка подготовленных Markdown/TXT/JSON-файлов.
- Application-level boundary для интеллектуального анализа: `IAiAnalysisEngine`.
- `DirectLlmAnalysisEngine`, который отделен от конкретного LLM provider через `ILlmProvider`.
- Deterministic demo/mock provider для локального запуска без внешних ключей.
- DeepSeek provider как первый real provider, без секретов в репозитории.
- Сохранение AI/LLM-результата, raw response, input snapshot и карты влияния.
- Экспертная оценка элементов карты влияния, пропущенных элементов и корректировок.
- Фиксация экспертного заключения человеком.
- Markdown и JSON export после экспертного заключения.
- Защита воспроизводимости: после экспертной оценки, экспертного заключения или экспорта повторный анализ не должен незаметно перезаписывать оцененный результат.
- Документированный end-to-end smoke checklist.
- Набор unit/page/application tests.

## Task из implementation-plan

Закрыты:

- Task 1 - скелет solution.
- Task 2 - базовая конфигурация и SQLite.
- Task 3 - доменная модель MVP.
- Task 4a-4d - EF tooling, mapping, первая SQLite migration, persistence test.
- Task 5 - список анализов.
- Task 6 - создание и редактирование анализа.
- Task 7 - состояние артефакта анализа.
- Task 8 - ручной контекст.
- Task 9 - загрузка файлов Markdown/TXT/JSON.
- Task 10 - review входных данных.
- Task 11 - `IAiAnalysisEngine` contract и request assembly.
- Task 12 - `DirectLlmAnalysisEngine` и provider configuration.
- Task 13 - deterministic demo/mock provider.
- Task 14 - DeepSeek integration spike/provider.
- Task 15 - validation и fallback.
- Task 16 - запуск анализа и карта влияния.
- Task 17 - экспертная оценка.
- Task 18 - экспертное заключение.
- Task 19 - Markdown export.
- Task 20 - JSON export.
- Task 21 - защита воспроизводимости результата.
- Task 22 - end-to-end smoke scenario.

Закрыты частично:

- Нет известных частично закрытых task на уровне MVP-0. DeepSeek реализован как опциональный provider и не является обязательным для smoke-сценария.

Не закрыты:

- Нет известных незакрытых task из текущего implementation-plan MVP-0.

## Как запустить проект локально

Рекомендуемый локальный режим - Development с demo provider:

```powershell
$env:DOTNET_CLI_HOME='C:\git\requirement-impact-assistant\.dotnet_home'
dotnet tool restore
dotnet tool run dotnet-ef database update --project src\RequirementImpactAssistant.Web\RequirementImpactAssistant.Web.csproj --startup-project src\RequirementImpactAssistant.Web\RequirementImpactAssistant.Web.csproj
dotnet run --project src\RequirementImpactAssistant.Web\RequirementImpactAssistant.Web.csproj --urls http://localhost:5100
```

Открыть:

```text
http://localhost:5100
```

Development config использует `AiAnalysis:Provider=Demo`, поэтому для smoke-сценария API key не нужен.

## Как запустить тесты

```powershell
$env:DOTNET_CLI_HOME='C:\git\requirement-impact-assistant\.dotnet_home'
dotnet test RequirementImpactAssistant.sln
```

## Результаты build/test

`dotnet build RequirementImpactAssistant.sln`:

- результат: прошла;
- warnings: 0;
- errors: 0.

`dotnet test RequirementImpactAssistant.sln`:

- результат: прошли;
- всего тестов: 152;
- passed: 152;
- failed: 0;
- skipped: 0;
- длительность тестового запуска: около 4 s.

## Внешние зависимости

- .NET SDK 8.
- NuGet packages из project files:
  - `Microsoft.EntityFrameworkCore.Design` 8.0.6;
  - `Microsoft.EntityFrameworkCore.Sqlite` 8.0.6;
  - `Microsoft.NET.Test.Sdk` 17.8.0;
  - `xunit` 2.5.3;
  - `xunit.runner.visualstudio` 2.5.3;
  - `coverlet.collector` 6.0.0.
- SQLite используется через EF Core provider.
- Для demo/mock provider внешняя сеть и API key не нужны.
- Для DeepSeek provider нужен внешний сетевой доступ и API key, заданный вне репозитория.

## API key

Проект может работать через demo/mock provider без API key. В `src/RequirementImpactAssistant.Web/appsettings.Development.json` указан `AiAnalysis:Provider=Demo`.

DeepSeek provider реализован, но секреты не хранятся в репозитории. В `src/RequirementImpactAssistant.Web/appsettings.json` есть только provider/model/base URL, без API key.

## Smoke-сценарий MVP-0

Данные для smoke:

- Название: `Smoke: изменение SLA уведомлений`.
- Исходное описание/требование: `Система отправляет клиенту email-уведомление о статусе заявки в течение 15 минут после изменения статуса.`
- Проектный запрос/изменение: `Сократить целевое время отправки уведомления до 2 минут и добавить SMS для критичных статусов.`
- Ситуация: `Изменение требуется для пилотного клиента с повышенными требованиями к оперативности уведомлений.`
- Источник: `Протокол встречи с пилотным клиентом, 2026-06-10.`
- Контекст 1: `Текущая интеграция с SMS-провайдером используется только для MFA и имеет лимит 100 сообщений в минуту.`
- Контекст 2: `Email-очередь обрабатывается batch-процессом раз в 5 минут; SLA мониторится только по email.`

Шаги и ожидаемый результат:

1. Создать анализ.
   Ожидаемо: появляется новый анализ в списке, заполнены title, исходное описание, запрос, ситуация и источник.
2. Добавить 1-2 фрагмента контекста вручную или загрузкой подготовленного `.md`, `.txt` или `.json`.
   Ожидаемо: фрагменты отображаются на странице анализа с типом, источником и текстом.
3. Перейти на review входных данных.
   Ожидаемо: видны исходные данные и добавленный контекст, доступен запуск анализа.
4. Запустить интеллектуальный анализ через demo/mock provider.
   Ожидаемо: анализ завершается без API key и без сети, сохраняется AI result, raw response, input snapshot и карта влияния.
5. Посмотреть карту влияния.
   Ожидаемо: видны затронутые требования, задачи, решения, API/документы/тесты, риски, вопросы, варианты для экспертного рассмотрения и предварительная оценка.
6. Выполнить экспертную оценку.
   Ожидаемо: эксперт отмечает элементы как подтвержденные/исправленные/отклоненные/требующие уточнения, может добавить пропущенные элементы и корректировки.
7. Зафиксировать экспертное заключение.
   Ожидаемо: сохраняется conclusion type, комментарий, обоснование и дата фиксации; статус анализа становится `ExpertConclusionFixed`.
8. Экспортировать Markdown.
   Ожидаемо: скачивается человекочитаемый отчет, доступный только после экспертного заключения.
9. Экспортировать JSON.
   Ожидаемо: скачивается структурированный JSON с metadata, input, context, AI result, impact map, expert evaluation, expert conclusion и export metadata.

## Основные места кода

- Основной web-проект: `src/RequirementImpactAssistant.Web`.
- Тесты: `tests/RequirementImpactAssistant.Tests`.
- Конфигурация DI: `src/RequirementImpactAssistant.Web/Extensions/ServiceCollectionExtensions.cs`.
- Startup: `src/RequirementImpactAssistant.Web/Program.cs`.
- Razor Pages: `src/RequirementImpactAssistant.Web/Pages/Analyses`.
- Persistence: `src/RequirementImpactAssistant.Web/Data`.
- EF migrations: `src/RequirementImpactAssistant.Web/Data/Migrations`.

## Доменная модель

- Основные сущности: `src/RequirementImpactAssistant.Web/Domain`.
- Карта влияния: `src/RequirementImpactAssistant.Web/Domain/Impact`.
- Enum-ы статусов, экспертных оценок и типов: `src/RequirementImpactAssistant.Web/Domain/Enums`.

## AI/LLM boundary

- `IAiAnalysisEngine`: `src/RequirementImpactAssistant.Web/Application/Analysis/IAiAnalysisEngine.cs`.
- `DirectLlmAnalysisEngine`: `src/RequirementImpactAssistant.Web/Application/Analysis/DirectLlmAnalysisEngine.cs`.
- Provider boundary: `src/RequirementImpactAssistant.Web/Application/Analysis/Llm/ILlmProvider.cs`.
- Demo/mock provider: `src/RequirementImpactAssistant.Web/Application/Analysis/Llm/DemoLlmProvider.cs`.
- DeepSeek provider: `src/RequirementImpactAssistant.Web/Application/Analysis/Llm/DeepSeekLlmProvider.cs`.
- Provider options: `src/RequirementImpactAssistant.Web/Application/Analysis/Llm/AiAnalysisOptions.cs` и `DeepSeekLlmProviderOptions.cs`.
- Запуск анализа из application service: `src/RequirementImpactAssistant.Web/Application/Analysis/AnalysisExecutionService.cs`.
- Сбор входного snapshot: `src/RequirementImpactAssistant.Web/Application/Analysis/AnalysisInputAssembler.cs`.
- Boundary notice: `src/RequirementImpactAssistant.Web/Application/Analysis/AnalysisBoundaryNotice.cs`.

## Export

- Markdown export:
  - `src/RequirementImpactAssistant.Web/Application/Export/IAnalysisMarkdownExportService.cs`;
  - `src/RequirementImpactAssistant.Web/Application/Export/AnalysisMarkdownExportService.cs`;
  - `src/RequirementImpactAssistant.Web/Application/Export/AnalysisMarkdownReportBuilder.cs`.
- JSON export:
  - `src/RequirementImpactAssistant.Web/Application/Export/IAnalysisJsonExportService.cs`;
  - `src/RequirementImpactAssistant.Web/Application/Export/AnalysisJsonExportService.cs`;
  - `src/RequirementImpactAssistant.Web/Application/Export/AnalysisJsonReportBuilder.cs`.
- UI handlers для скачивания:
  - `src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml.cs`, методы `OnGetExportMarkdownAsync` и `OnGetExportJsonAsync`.

## Экспертная оценка и заключение

- Экспертная оценка:
  - domain: `src/RequirementImpactAssistant.Web/Domain/ExpertEvaluation.cs`, `ExpertEvaluatedItem.cs`, `ExpertMissedItem.cs`, `ExpertCorrection.cs`;
  - UI/page model: `src/RequirementImpactAssistant.Web/Pages/Analyses/ExpertEvaluation.cshtml` и `.cs`.
- Фиксация экспертного заключения:
  - domain: `src/RequirementImpactAssistant.Web/Domain/ExpertConclusion.cs`;
  - UI/page model: `src/RequirementImpactAssistant.Web/Pages/Analyses/ExpertConclusion.cshtml` и `.cs`.

## Проверка архитектурных ограничений

- UI/Razor Pages/controllers не вызывают LLM напрямую: запуск идет через `IAnalysisExecutionService` на странице review.
- Интеллектуальный анализ идет через `IAiAnalysisEngine`: DI регистрирует `IAiAnalysisEngine -> DirectLlmAnalysisEngine`.
- `DirectLlmAnalysisEngine` отделен от конкретного LLM provider: зависит от `ILlmProvider`, а не от DeepSeek напрямую.
- Demo/mock provider позволяет запуск без внешних ключей: Development config выбирает `Demo`.
- DeepSeek provider есть, но API key не хранится в репозитории.
- RAG, Dify, embeddings, rerank и agentic search не добавлены в MVP-0.
- Экспертное решение хранится отдельно от результата AI/LLM: `AiAnalysisResult`, `ExpertEvaluation`, `ExpertConclusion` являются отдельными доменными сущностями.
- AI/LLM-результат помечается как предварительный аналитический материал через boundary notice и export content.
- Статусы анализа пассивные и не превращены в workflow согласования.
- Варианты экспертного решения сохраняются как значения заключения и не запускают автоматические действия.

## Известные ограничения MVP-0

- Локальный web-прототип без авторизации и ролевой модели.
- Нет production-grade security model.
- Нет Jira/Confluence/GitLab/mail/chat integrations.
- Нет RAG, Dify, embeddings, rerank, agentic search, vector database.
- Нет workflow согласования, task board, backlog, sprint workflow.
- Нет автоматического изменения требований, API, документации или кода.
- Нет PDF export.
- Нет dashboard для экспериментальной главы.
- История всех LLM-запусков не хранится как полноценный audit log.
- DeepSeek provider требует внешний API key и сеть, но smoke не зависит от него.
- Файлы контекста ограничены Markdown/TXT/JSON и лимитом размера в page model.

## Специально не реализовано

- RAG.
- Dify.
- Embeddings.
- Rerank.
- Agentic search.
- Jira integration.
- Confluence integration.
- GitLab integration.
- Workflow согласования.
- Авторизация.
- PDF export.

## Места особого внимания при ревью

- Граница `IAiAnalysisEngine` и отсутствие прямых LLM-вызовов из UI.
- Разделение `DirectLlmAnalysisEngine` и `ILlmProvider`.
- Отсутствие секретов DeepSeek в репозитории и поведение без API key.
- Маркировка AI/LLM-результата как предварительного аналитического материала.
- Отдельное хранение экспертной оценки и экспертного заключения.
- Защита оцененного или экспортированного результата от незаметной перезаписи.
- Доступность Markdown/JSON export только после экспертного заключения.
- Стабильность JSON-полей для последующей экспериментальной обработки.
- То, что статусы и conclusion types не создают автоматический workflow.
- Исключение локальных баз, секретов и временных файлов из review archive.

## Дерево проекта

PowerShell-аналог выполнен по отслеживаемым файлам с исключением `bin`, `obj`, `.git`, `node_modules`, `data`, локальных баз и user-файлов. Vendor-файлы `wwwroot/lib` свернуты, чтобы не раздувать отчет.

```text
.
|-- .config/
|   `-- dotnet-tools.json
|-- .github/
|   `-- workflows/
|-- docs/
|   |-- program/
|   |   |-- clarification-decisions.md
|   |   |-- end-to-end-smoke-checklist.md
|   |   |-- implementation-plan.md
|   |   |-- initial-concept.md
|   |   |-- requirements-draft.md
|   |   |-- technical-design.md
|   |   `-- ui-concept.md
|   |-- review/
|   |   `-- review-package.md
|   `-- workflow/
|       |-- README.md
|       |-- checklists.md
|       `-- development-cycle.md
|-- src/
|   `-- RequirementImpactAssistant.Web/
|       |-- Application/
|       |   |-- Analysis/
|       |   |   |-- Llm/
|       |   |   |   |-- AiAnalysisOptions.cs
|       |   |   |   |-- DeepSeekLlmProvider.cs
|       |   |   |   |-- DeepSeekLlmProviderOptions.cs
|       |   |   |   |-- DemoLlmProvider.cs
|       |   |   |   |-- ILlmProvider.cs
|       |   |   |   |-- LlmProviderNames.cs
|       |   |   |   |-- LlmProviderRequest.cs
|       |   |   |   `-- LlmProviderResponse.cs
|       |   |   |-- AiAnalysisRequest.cs
|       |   |   |-- AiAnalysisResponse.cs
|       |   |   |-- AiAnalysisResponseValidator.cs
|       |   |   |-- AnalysisBoundaryNotice.cs
|       |   |   |-- AnalysisExecutionService.cs
|       |   |   |-- AnalysisInputAssembler.cs
|       |   |   |-- AnalysisInputSnapshot.cs
|       |   |   |-- DirectLlmAnalysisEngine.cs
|       |   |   |-- ExpectedAnalysisResultStructure.cs
|       |   |   |-- IAiAnalysisEngine.cs
|       |   |   |-- IAnalysisExecutionService.cs
|       |   |   `-- IAnalysisInputAssembler.cs
|       |   `-- Export/
|       |       |-- AnalysisJsonExportService.cs
|       |       |-- AnalysisJsonReportBuilder.cs
|       |       |-- AnalysisMarkdownExportService.cs
|       |       |-- AnalysisMarkdownReportBuilder.cs
|       |       |-- IAnalysisJsonExportService.cs
|       |       `-- IAnalysisMarkdownExportService.cs
|       |-- Data/
|       |   |-- ApplicationDbContext.cs
|       |   |-- ApplicationDbContextFactory.cs
|       |   |-- Migrations/
|       |   `-- SqliteConnectionStringResolver.cs
|       |-- Domain/
|       |   |-- Enums/
|       |   |-- Impact/
|       |   |-- AiAnalysisResult.cs
|       |   |-- Analysis.cs
|       |   |-- AnalysisStatusCalculator.cs
|       |   |-- ContextFragment.cs
|       |   |-- ExpertConclusion.cs
|       |   |-- ExpertCorrection.cs
|       |   |-- ExpertEvaluatedItem.cs
|       |   |-- ExpertEvaluation.cs
|       |   `-- ExpertMissedItem.cs
|       |-- Extensions/
|       |   `-- ServiceCollectionExtensions.cs
|       |-- Pages/
|       |   |-- Analyses/
|       |   |   |-- Create.cshtml(.cs)
|       |   |   |-- Details.cshtml(.cs)
|       |   |   |-- Edit.cshtml(.cs)
|       |   |   |-- ExpertConclusion.cshtml(.cs)
|       |   |   |-- ExpertEvaluation.cshtml(.cs)
|       |   |   |-- Index.cshtml(.cs)
|       |   |   `-- Review.cshtml(.cs)
|       |   `-- Shared/
|       |-- Properties/
|       |-- wwwroot/
|       |   |-- css/
|       |   |-- js/
|       |   `-- lib/...
|       |-- Program.cs
|       |-- RequirementImpactAssistant.Web.csproj
|       |-- appsettings.Development.json
|       `-- appsettings.json
|-- tests/
|   `-- RequirementImpactAssistant.Tests/
|       |-- Application/
|       |-- Configuration/
|       |-- Data/
|       |-- Domain/
|       |-- Pages/
|       `-- RequirementImpactAssistant.Tests.csproj
|-- .gitignore
|-- AGENTS.md
|-- NuGet.Config
`-- RequirementImpactAssistant.sln
```


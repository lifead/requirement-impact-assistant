# Технический проект MVP-3 программы анализа влияния проектных запросов

## Ключевые технические решения

| Вопрос | Варианты | Выбранное решение | Причина | Последствия для БД/export/tests |
| --- | --- | --- | --- | --- |
| Тип проектного запроса | A: persisted field in `Analysis` + migration; B: UI-only/text hint; C: derived value | A: отдельное поле `ProjectRequestType` в `Analysis`, C# enum, хранение строкой | Тип запроса входит в устойчивый функциональный состав MVP-3, нужен для воспроизводимости, деталей анализа и export | Нужна migration с default `Other` для существующих записей; UI новых записей требует явного выбора через neutral placeholder; Markdown/JSON export расширяется additive-полем; tests покрывают persistence, pages, export |
| Представление `ProjectRequestType` | Enum; свободная строка; справочник в БД | Enum `ProjectRequestType` с EF string conversion | Минимальный закрытый классификатор из требований, без отдельной таблицы и без runtime-администрирования | Новая колонка `Analyses.ProjectRequestType` как строка; default `Other`; tests на default/backward compatibility |
| Тип `Other` | Отдельное поле `OtherText`; описание в существующих текстовых полях; новая сущность | Использовать enum value `Other`, пояснение остается в `ProjectRequest`/`SituationDescription` | Не раздувает модель ради одного edge-case; пользователь уже обязан описать проектное изменение | Без дополнительной колонки; export показывает `Other` и существующие текстовые поля |
| Терминология входных данных | Переименовать C# properties/columns; изменить только UI labels/helper texts; добавить новые дублирующие поля | Сохранить `OriginalDescription`, `ProjectRequest`, `SituationDescription`, `ChangeSource`; менять labels/helper texts | Переименование домена и колонок не дает новой функции, но несет риск миграций, export break и потери совместимости | Без rename migration; tests меняются на UI text; export contract существующих полей сохраняется |
| Helper texts | Inline strings в Razor; `AnalysisUiText`/аналог; новая localization-система | Централизовать в `AnalysisUiText` или близком helper-е | Уже есть локальный helper для labels; полноценная localization преждевременна | Без БД; tests могут проверять стабильные тексты; domain model не меняется ради подсказок |
| Create/Edit | Новый wizard; новая страница; расширение текущих Razor Pages | Сохранить текущие Create/Edit Razor Pages, добавить поле типа запроса, labels, helper texts и neutral placeholder без молчаливого default для новых записей | Минимальное изменение в текущем пользовательском пути и явный осознанный выбор типа запроса | Затрагивает form input/page tests; без endpoint-ов, wizard и UI-платформы |
| Details | Отдельный result workspace; taskboard/workflow; улучшенная смысловая навигация в текущей странице | Улучшить текущую Details page: входные данные, manual context, retrieved context, карта влияния, основания/ограничения, preliminary material | Details уже центральный просмотр сохраненного артефакта | No DB кроме `ProjectRequestType`; page tests на разделение смысловых блоков; export остается из сохраненного артефакта |
| Review page | Прямой status/health-check Dify; provider-neutral availability; UI зависит от Dify options | Provider-neutral clarity без real health-check при GET; запуск только через `IAnalysisExecutionService` | Сохраняет application-level boundary и не требует сети/секретов для просмотра | Regression tests, что Razor Page не зависит от Dify adapter/options/HttpClient |
| ExpertEvaluation/ExpertConclusion | Workflow actions; passive human record; автоматический reanalysis | Сохранить человеческую оценку и пассивные conclusion types без задач, уведомлений, workflow и reanalysis | Соответствует исследовательской позиции: решение принимает человек | Без schema changes; tests на отсутствие engine/provider side effects |
| Export | Новый формат/PDF; переименование existing fields; additive расширение Markdown/JSON | Сохранить Markdown/JSON и добавить `input.projectRequestType`/аналог без повышения текущего `FormatVersion`, так как изменение additive-compatible | Export нужен для воспроизводимости MVP-3, PDF не входит | Tests на presence нового field, backward-safe parsing и сохранение разделов input/preliminary/grounds/expert |
| Demo scenario | A: кнопка "Заполнить пример"; B: demo action/route; C: documentation checklist only; D: seed data | C: documentation checklist only как базовый механизм MVP-3 | Самый безопасный способ без production seed, секретов, real Dify, новых route и внешних зависимостей | Только docs на этапе реализации демо; default tests без сети; no DB seed |
| Persistence/migrations | Без миграции; migration для `ProjectRequestType`; комплексная переработка schema | Одна additive migration для `ProjectRequestType`; no unrelated changes | Persisted field требует schema update, но изменение изолированное | Default/backward compatibility tests; existing records получают `Other` |
| Tests | Только manual smoke; page/export/persistence/boundary regression; network integration tests | Page labels/helper, persistence/default, export, application boundary, no real Dify/DeepSeek/network/secrets | MVP-3 меняет смысловой UX и export, значит нужны точечные regression checks | Default `dotnet test` offline; no user-secrets |
| Security/secrets | Вывод provider config; real payload в docs/tests; sanitized/provider-neutral model | Не трогать user-secrets, не выводить keys, не добавлять real provider payload в UI/export/tests/docs | MVP-2 уже зафиксировал правила безопасности Dify/DeepSeek | Tests/docs не содержат secrets; real Dify smoke остается manual/optional |
| Неизменяемые границы | Менять Dify/Direct/External behavior; менять prompt contract; добавить RAG | Не менять Dify adapter behavior, DirectLlm/ExternalRag behavior, prompt contract, RAG KB, provider DTO boundary, analysis engines | MVP-3 - UX/registration readiness, не новый AI/RAG этап | Regression tests на boundaries; no adapter/export-provider coupling |

## Назначение документа

Документ фиксирует технический проект MVP-3 после требований и UI flow. Он не является реализацией, implementation plan, task breakdown, тестовым планом или командой начинать кодирование.

MVP-3 развивает уже существующий контур MVP-0/MVP-1/MVP-2: локальное ASP.NET Core Razor Pages приложение, SQLite/EF Core persistence, `IAiAnalysisEngine`, режимы `DirectLlm` и `ExternalRag`, Dify как optional concrete adapter за `IExternalRagAdapter`, структурированную `ImpactMap`, экспертную оценку, экспертное заключение и Markdown/JSON export.

Технический проект выбирает минимальные решения, которые делают пользовательский путь понятным и воспроизводимым, но не расширяют систему до RAG-платформы, workflow/taskboard, dashboard, PDF-generator или интеграционной платформы.

## Scope MVP-3

Входит:

- persisted тип проектного запроса как часть карточки анализа;
- уточнение UI-терминологии входных данных без переименования доменной модели;
- helper texts для Create/Edit и связанных страниц;
- смысловая навигация и разделение входных данных, manual context, retrieved context, preliminary AI/RAG/LLM result, оснований/ограничений, экспертной оценки и заключения;
- provider-neutral clarity на Review page;
- additive расширение Markdown/JSON export;
- безопасный документационный demo scenario;
- точечные regression tests для persistence, pages, export и architecture boundaries.

Не входит:

- новый RAG, embeddings, rerank, vector database или agentic workflow;
- новый provider или analysis engine;
- изменение Dify adapter behavior, DirectLlm/ExternalRag behavior, prompt contract или provider-specific DTO boundary;
- workflow согласования, taskboard, backlog, dashboard, уведомления, assignees или автоматическое создание задач;
- PDF export;
- user-secrets, реальные ключи, реальные provider payloads, production seed data;
- изменения `appsettings`, `.csproj`, migrations сверх одной migration для `ProjectRequestType`.

## Тип проектного запроса

Рассмотрены три варианта.

A. Persisted field in `Analysis` + migration. Тип запроса сохраняется в основном артефакте анализа, доступен на Create/Edit/Details/Review, попадает в input snapshot и export.

B. UI-only/text hint. Тип показывается только как подсказка или текст в форме, но не сохраняется отдельно.

C. Derived value. Тип вычисляется из `ProjectRequest`, `SituationDescription` или других текстов.

Выбрано A.

Причина: MVP-3 требует минимальный классификатор типов проектного запроса как устойчивую часть функционального состава. UI-only вариант не обеспечивает воспроизводимость и export. Derived value создает ложную автоматическую классификацию и может выглядеть как дополнительное AI/decision behavior, которого MVP-3 не вводит.

Техническое решение:

- добавить enum `ProjectRequestType`;
- значения: `RequirementChange`, `NewFunctionality`, `DefectFix`, `RequirementClarification`, `ApiOrIntegrationChange`, `ArchitecturalConstraintChange`, `ProjectDecisionChange`, `UserScenarioChange`, `Other`;
- добавить свойство `ProjectRequestType ProjectRequestType` в `Analysis`;
- хранить в SQLite строкой через EF conversion, как уже хранятся другие enum поля;
- задать persisted default для существующих записей как `Other`;
- для новых записей UI не должен молча выбирать `RequirementChange`, `Other` или любой другой enum value;
- форма Create должна показывать neutral placeholder вроде "Выберите тип проектного запроса", который не соответствует persisted enum value;
- создание новой записи требует осознанного выбора пользователем одного из значений `ProjectRequestType`;
- server-side validation должна отклонять отсутствие выбора в обычном Create/Edit flow;
- `Other` остается persisted default только для existing rows/backwards compatibility и defensive server-side fallback при старых/неполных данных;
- если пользователь явно выбирает `Other`, смысл запроса описывается в `ProjectRequest` и при необходимости в `SituationDescription`, без отдельной колонки `OtherText`.

Migration strategy:

- одна additive migration добавляет `ProjectRequestType` в `Analyses`;
- колонка обязательная, строковая, с default value `Other` для существующих строк;
- не переименовывать текущие поля и таблицы;
- не изменять `AiAnalysisResults`, retrieved context tables, expert tables или export tables.

Compatibility:

- существующие записи открываются как `Other`;
- новые записи не получают `RequirementChange` автоматически из UI;
- существующие JSON fields `originalDescription`, `originalRequirement`, `projectRequest`, `proposedChange`, `situationDescription`, `changeSource` сохраняются;
- новый export field добавляется additive, без удаления или переименования старых полей;
- старые результаты без типа запроса остаются валидными после migration.

## Терминология входных данных

Текущая кодовая база использует:

- `OriginalDescription`;
- `ProjectRequest`;
- `SituationDescription`;
- `ChangeSource`.

MVP-3 меняет пользовательскую терминологию, но не требует технического переименования этих C# properties или DB columns.

Выбранное решение:

- `OriginalDescription` отображать как "Текущее состояние";
- `ProjectRequest` отображать как "Проектное изменение";
- `SituationDescription` отображать как "Ситуация и причина изменения";
- `ChangeSource` отображать как "Источник изменения";
- в helper text пояснять, что текущее состояние является входным снимком для конкретного анализа, а не RAG knowledge base и не retrieved context.

Почему не переименовывать C# properties/columns:

- существующие имена уже участвуют в EF mapping, forms, input snapshot, tests и export;
- rename migration не дает новой функции, но увеличивает риск несовместимости;
- JSON export уже сохраняет дополнительные семантические aliases `OriginalRequirement` и `ProposedChange`;
- MVP-3 может достичь смысловой ясности через labels/helper texts, не ломая контракты.

## Helper texts

Подсказки должны быть централизованы в `AnalysisUiText` или близком helper-е, чтобы Razor markup не разрастался разрозненными строками.

Решение:

- добавить helper methods для labels и descriptions входных полей;
- добавить helper methods для `ProjectRequestType` labels;
- не вводить новую localization-систему;
- не добавлять подсказки в domain model;
- не хранить helper texts в БД.

Подсказки должны объяснять различие:

- текущее состояние - входной снимок;
- проектное изменение - предмет анализа, а не принятое решение;
- manual context - явно добавленные пользователем материалы;
- retrieved context - только фактически возвращенные внешним provider-ом материалы/metadata;
- preliminary analytical material - результат AI/RAG/LLM, который проверяет человек.

## Create/Edit

Create/Edit сохраняют текущую Razor Pages структуру. Новый wizard, отдельный route или frontend framework не вводятся.

Форма должна включать:

- название;
- `ProjectRequestType` с neutral placeholder, который требует явного выбора и не сохраняется как enum value;
- текущее состояние (`OriginalDescription`);
- проектное изменение (`ProjectRequest`);
- ситуацию и причину (`SituationDescription`);
- источник изменения (`ChangeSource`).

`AnalysisFormInput` расширяется полем типа запроса и применяет его к `Analysis`. Для Create flow input может представлять отсутствие выбора до validation, но сохраненная `Analysis` должна получать только выбранный enum value или defensive fallback `Other` для старых/неполных данных. Validation остается page-level/form-level и не должна вызывать AI/RAG или provider logic.

## Details

Details остается главным экраном сохраненного артефакта анализа.

Техническое решение:

- показывать тип проектного запроса рядом с входными данными;
- добавить смысловую навигацию по слоям: входные данные, manual context, preliminary result, grounds/limitations, expert evaluation, expert conclusion, export;
- разделить manual context и retrieved context текстом и структурой страницы;
- показывать карту влияния как preliminary analytical material;
- показывать блок оснований/ограничений с analysis mode, provider/adapter metadata, retrieved context state, warnings и limitation notes;
- не создавать taskboard, workflow states, approval route или dashboard.

Для `DirectLlm` не создавать искусственный retrieved context. Для `ExternalRag` показывать saved retrieved context или честное состояние `Unavailable`, `Partial` или `MetadataOnly`.

## Review page

Review page отвечает за проверку входных данных и запуск предварительного анализа через application-level service.

Решение:

- сохранить запуск только через `IAnalysisExecutionService`;
- PageModel не должен зависеть от `DifyExternalRagAdapter`, `DifyExternalRagOptions`, `IExternalRagAdapter`, `ILlmProvider`, `HttpClient` или provider DTO;
- не выполнять real health-check при GET;
- показывать provider-neutral note: `DirectLlm` локально использует настроенный LLM provider, `ExternalRag` может использовать mock fallback или внешний adapter в зависимости от application configuration;
- не выводить endpoint, API key, bearer token, cookies, CSRF, raw provider payload или secret-bearing config.

Если external AI/RAG не настроен, это не должно выглядеть как поломка direct сценария. Недоступность или fallback фиксируются только при application-level запуске и сохраненном результате.

## ExpertEvaluation и ExpertConclusion

Экспертная оценка и заключение остаются человеческим слоем.

Решение:

- ExpertEvaluation работает с сохраненной `ImpactMap` и не вызывает analysis engine;
- ExpertConclusion фиксирует `ExpertConclusionType`, комментарий, обоснование и дату;
- типы `SplitIntoSeveralTasks` и `ReturnForReanalysis` являются passive conclusion types;
- эти типы не создают задачи, уведомления, workflow, внешний request или автоматический reanalysis;
- сохранение экспертной оценки/заключения не меняет `AiAnalysisResult` и не вызывает provider.

MVP-3 может уточнить UI-тексты, чтобы пользователь видел: заключение фиксирует человек, а программа не выполняет управленческое действие.

## Export

Markdown/JSON export сохраняется как единственный обязательный export contour. PDF не входит.

Если `ProjectRequestType` хранится persisted, он должен попасть в export.

Markdown:

- добавить поле типа запроса в раздел `Input`;
- сохранить разделы export metadata, analysis result metadata, retrieved context, input, context fragments, structured impact map, expert evaluation, expert conclusion, decision boundary;
- не переименовывать существующие labels так, чтобы старые отчеты стали несопоставимы без необходимости.

JSON:

- добавить `input.projectRequestType` или эквивалентное поле в объект `input`;
- сохранить существующие поля `OriginalDescription`, `OriginalRequirement`, `ProjectRequest`, `ProposedChange`, `SituationDescription`, `ChangeSource`;
- не повышать текущий `FormatVersion`: текущий export contract трактуется как допускающий additive fields, а добавление типа запроса не удаляет и не переименовывает существующие поля;
- backward-safe parsing должен игнорировать unknown/additive fields при чтении старым потребителем и корректно обрабатывать старые exports без `input.projectRequestType`;
- не включать provider-specific DTO как stable schema.

Export строится из сохраненного артефакта, а не через повторный вызов AI/RAG/provider.

## Demo scenario

Рассмотрены варианты.

A. Кнопка "Заполнить пример" в UI. Удобно для показа, но требует production UI action, текстовых fixture-ов в коде и новых page tests.

B. Demo action/route. Изолирует демо, но добавляет route и может выглядеть как продуктовая функция.

C. Documentation checklist only. Не меняет runtime, не создает seed data и не требует внешних сервисов.

D. Seed data. Удобно для локального запуска, но риск загрязнить production/demo DB и создать ложную зависимость от fixture state.

Выбрано C как базовый механизм MVP-3.

Причина: это самый простой и безопасный способ показать сценарий без production seed, real Dify, внешних зависимостей, секретов и новых route. Документированный checklist может описать один обезличенный кейс, шаги для `DirectLlm` через demo provider и optional manual smoke для external AI/RAG/mock adapter.

На будущих этапах кнопка "Заполнить пример" может быть рассмотрена отдельно, если появится явное решение сделать demo UI feature.

## Persistence и migrations

Нужна одна migration только для `ProjectRequestType`.

Правила:

- default для существующих записей: `Other`;
- no unrelated schema changes;
- no changes to retrieved context persistence;
- no changes to expert evaluation/conclusion schema;
- no appsettings/user-secrets changes;
- migration должна быть совместима с существующей SQLite БД;
- ApplicationDbContext model snapshot обновляется только в рамках этой migration на этапе реализации.

Backwards compatibility проверяется через persistence tests: существующие записи с default открываются, сохраняются, редактируются и экспортируются.

## Tests

MVP-3 требует точечных тестов, а не новой test infrastructure.

Покрыть:

- labels/helper texts на Create/Edit/Details/Review;
- `ProjectRequestType` в `AnalysisFormInput`, persistence, required UI choice для новых записей и default `Other` только для existing records/backwards compatibility/defensive fallback;
- neutral placeholder на Create/Edit: UI не выбирает `RequirementChange` молча;
- `ProjectRequestType` в Markdown/JSON export, включая presence нового `input.projectRequestType`/аналога;
- backward-safe parsing: старые exports без `input.projectRequestType` остаются читаемыми, additive field не требует повышения `FormatVersion`;
- сохранение существующих export fields;
- Review page boundary: нет прямых зависимостей от Dify adapter/options, `IExternalRagAdapter`, `ILlmProvider`, `HttpClient`;
- ExpertEvaluation/ExpertConclusion не вызывают analysis engine/provider;
- default tests работают без real Dify, DeepSeek, network, user-secrets и secrets;
- no workflow/taskboard language там, где оно может создать ложное ожидание автоматического управления.

Manual smoke для real Dify остается отдельным optional сценарием и не является default gate.

## Security и secrets

MVP-3 не меняет secret management.

Правила:

- не трогать user-secrets;
- не выводить keys, bearer tokens, cookies, CSRF, passwords или raw secret-bearing payloads;
- не добавлять real provider payload в UI/export/tests/docs;
- не добавлять реальные корпоративные данные в demo docs;
- Dify/DeepSeek examples использовать только с placeholders;
- manual smoke real Dify остается отдельно от default tests/build.

## Неизменяемые границы

MVP-3 не должен менять:

- `DifyExternalRagAdapter` behavior;
- `DirectLlmAnalysisEngine` behavior;
- `ExternalRagAnalysisEngine` behavior;
- prompt contract и expected AI response shape;
- RAG knowledge base или retrieval pipeline;
- provider-specific DTO boundary;
- `IAiAnalysisEngine` и `IAnalysisExecutionService` как application-level границы;
- Markdown/JSON export как единственные обязательные export formats;
- snapshot protection после экспертной оценки/заключения/export.

Любое изменение этих границ требует отдельного решения вне MVP-3 technical design.

## Risks / Open Questions

- Если позже потребуется текстовое уточнение для `Other`, понадобится отдельное решение о новом поле или о сохранении пояснения только в существующих текстовых полях.
- Helper texts могут стать слишком длинными для формы; на implementation planning нужно ограничить тексты и проверить читаемость.
- Page tests не должны закрепить хрупкую верстку вместо смысловых контрактов.
- Слишком подробная provider availability на Review page может случайно раскрыть config details; нужно держать текст provider-neutral.
- Demo checklist без кнопки безопасен, но менее удобен для живого показа; если демонстрация потребует one-click setup, это отдельная будущая feature.

## Следующий шаг

После review технического проекта можно переходить к implementation planning MVP-3. Implementation planning должен разложить изменения на маленькие проверяемые task и синхронизировать порядок/scope с `mvp3-task-breakdown.md` и этим technical design: persisted `ProjectRequestType` требует migration, form validation и export updates, поэтому task order может потребовать уточнения. Правило сохраняется: одна task - один проверяемый результат - один review - один commit только по явной команде пользователя.

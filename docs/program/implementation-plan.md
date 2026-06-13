# План реализации MVP программы анализа влияния проектных запросов

## Назначение документа

Документ фиксирует план реализации MVP после завершения требований и технического проекта.

Основание:

- `docs/program/requirements-draft.md`;
- `docs/program/technical-design.md`.

Перед подготовкой плана требования прошли review и были приняты как основание для технического проектирования. Технический проект прошел review и был принят как основание для implementation planning.

Документ не является реализацией и не разрешает автоматический переход к написанию кода. Код можно начинать только после отдельного явного решения о начале реализации.

Главный принцип планирования:

```text
одна task -> один проверяемый результат -> один обозримый diff -> один review -> один commit
```

## Границы реализации MVP

В рамках MVP-0 реализуется локальное web-приложение на ASP.NET Core Razor Pages с SQLite-хранилищем. Интеллектуальный анализ выполняется как прямой LLM-анализ с вручную добавленным проектным контекстом через `DirectLlmAnalysisEngine`.

Входит:

- создание и просмотр анализов проектных запросов;
- ввод исходного описания или требования, предлагаемого изменения или запроса, ситуации и источника;
- ручное добавление контекста;
- загрузка Markdown, TXT и JSON как подготовленных фрагментов контекста;
- запуск интеллектуального анализа через `IAiAnalysisEngine`;
- первая реализация `DirectLlmAnalysisEngine`, которая формирует prompt, вызывает LLM через provider adapter и возвращает карту влияния;
- deterministic demo/mock LLM provider для локальной демонстрации, тестов и воспроизводимости без внешних ключей;
- опциональная проверка реального LLM-вызова через DeepSeek как первый планируемый real provider при наличии API key и разрешения на сетевой вызов;
- структурированная карта влияния;
- экспертная оценка и экспертное заключение;
- Markdown и JSON экспорт;
- один end-to-end smoke scenario на подготовленных данных.

Не входит:

- реальные интеграции с Jira, GitLab, Confluence, почтой, чатами;
- собственный RAG, embeddings, rerank, agentic search и vector database;
- Dify, Flowise, LangGraph, Haystack или аналогичная интеграция с внешним AI/RAG-движком;
- task board, backlog, sprint workflow;
- автоматическое изменение требований, документов, API или кода;
- автоматическое создание pull request;
- многоуровневая авторизация;
- PDF-экспорт;
- аналитические dashboards для главы 4.

Ограничения MVP не должны закрывать развитие программы. В реализации должны оставаться явные точки расширения:

- `IAiAnalysisEngine` для замены direct LLM-анализа будущим адаптером к внешнему AI/RAG-движку без переписывания программы;
- `ILlmProvider` для замены или добавления LLM-провайдеров внутри `DirectLlmAnalysisEngine`;
- `IContextImporter` или аналогичный слой для будущих источников контекста;
- `IReportExporter` или аналогичный слой для будущих форматов экспорта;
- стабильный JSON-экспорт для последующей экспериментальной обработки;
- пассивные статусы анализа, которые можно использовать в будущем, но которые в MVP не запускают workflow.

Будущие реализации `DifyAnalysisEngine`, `ExternalRagAnalysisEngine`, `CustomRagAnalysisEngine` или аналоги должны подключаться как adapter-компоненты за `IAiAnalysisEngine`. MVP-0 не реализует собственный RAG и не зависит от конкретного внешнего AI/RAG-движка.

## План task

### Task 1. Скелет solution

Цель: создать минимальный .NET solution без доменной логики.

Зависимости: отсутствуют.

Входит:

- ASP.NET Core Razor Pages project;
- test project;
- базовая структура solution;
- стартовая страница приложения.

Не входит:

- EF Core;
- доменные сущности;
- бизнес-логика;
- LLM;
- реальные страницы MVP.

Проверки:

- `dotnet build`;
- `dotnet test`;
- локальный smoke run стартовой страницы с указанием URL и видимого результата.

Done:

- solution собирается;
- тестовый проект запускается;
- приложение стартует локально;
- в отчете по task указан URL локального запуска и что именно видно на стартовой странице.

### Task 2. Базовая конфигурация и SQLite

Цель: подключить конфигурацию приложения и SQLite без доменной схемы MVP.

Входит:

- configuration для локального SQLite-файла;
- подключение EF Core;
- минимальный `DbContext`;
- проверка, что секреты не требуются для запуска.

Не входит:

- полная доменная модель;
- миграции всех сущностей;
- LLM-настройки.

Проверки:

- `dotnet build`;
- `dotnet test`;
- тест загрузки конфигурации.

Done:

- приложение знает путь к локальной SQLite базе;
- конфигурация не содержит секретов.

### Task 3. Доменная модель MVP

Цель: описать доменные сущности и enum-ы MVP без UI и без persistence-деталей.

Входит:

- `Analysis`;
- `ContextFragment`;
- `AiAnalysisResult`;
- `ImpactMap`;
- `ExpertEvaluation`;
- `ExpertConclusion`;
- enum-ы статусов, типов контекста, экспертных отметок и типов заключения.

Не входит:

- страницы;
- миграции;
- LLM provider;
- export.

Проверки:

- unit tests для базовых default/status значений;
- unit tests для стабильных id элементов карты влияния.

Done:

- доменная модель покрывает технический проект;
- модель не содержит зависимостей от конкретного LLM-провайдера.

### Task 4. Persistence MVP schema

Цель: сохранить доменную модель в SQLite.

Входит:

- Task 4a. Подготовка EF migrations tooling;
- Task 4b. EF mapping доменной модели;
- Task 4c. Первая SQLite migration;
- Task 4d. Простой persistence test;
- хранение анализов, контекста, результата интеллектуального анализа, карты влияния, экспертной оценки и заключения.

Не входит:

- UI-страницы;
- file upload;
- LLM-вызов.

Проверки:

- `dotnet build`;
- `dotnet test`;
- применение миграции к чистой базе;
- save/load test для анализа.

Подзадачи:

#### Task 4a. Подготовка EF migrations tooling

Цель: подготовить design-time EF tooling для `ApplicationDbContext` без изменения доменной схемы MVP.

Входит:

- EF Design package;
- design-time factory для `ApplicationDbContext`;
- маленький тест, подтверждающий корректность design-time создания контекста.

Не входит:

- `DbSet`;
- EF mapping доменной модели;
- migration-файлы;
- создание базы;
- UI.

#### Task 4b. EF mapping доменной модели

Цель: описать `DbSet` и EF mapping доменных сущностей MVP для последующего сохранения в SQLite.

Входит:

- `DbSet` для доменных сущностей MVP;
- EF mapping доменных сущностей и связей;
- подготовка схемы хранения анализов, контекста, результата интеллектуального анализа, карты влияния, экспертной оценки и заключения.

Не входит:

- создание migration-файлов;
- применение миграций;
- UI.

#### Task 4c. Первая SQLite migration

Цель: создать первую SQLite migration и проверить ее применение к чистой базе.

Входит:

- создание первой SQLite migration для схемы MVP;
- проверка применения migration к чистой базе;
- явная фиксация блокера, если недоступен `dotnet-ef` или отдельно согласованный локальный tooling.

Не входит:

- обходной способ вместо согласованного tooling;
- UI.

#### Task 4d. Простой persistence test

Цель: проверить, что минимальный связанный набор данных MVP сохраняется в SQLite и читается обратно.

Входит:

- простой persistence test на save/load минимального связанного набора данных MVP.

Не входит:

- UI;
- LLM.

Done:

- данные MVP сохраняются и читаются из SQLite.

### Task 5. Список и открытие сохраненных анализов

Цель: дать пользователю доступ к сохраненным анализам.

Входит:

- страница списка анализов;
- отображение названия, статуса, даты изменения;
- сортировка списка по дате изменения от новых к старым;
- переход к сохраненному анализу;
- минимальная read-only страница открытия анализа с основными полями `Analysis`;
- использование списка анализов как входной точки приложения или явная навигация к нему с главной страницы;
- тест загрузки списка из SQLite через `ApplicationDbContext`.

Не входит:

- создание анализа;
- редактирование анализа;
- контекст;
- отображение результата интеллектуального анализа, карты влияния, экспертной оценки и экспертного заключения;
- seed-данные в production startup;
- LLM.

Проверки:

- UI smoke с заранее подготовленными seed-данными в локальной SQLite-базе или тестовой временной базе;
- test загрузки списка из БД;
- test открытия существующего анализа по id;
- `dotnet test`.

Done:

- пользователь видит сохраненные анализы и может открыть анализ.
- открытие анализа в Task 5 означает read-only просмотр, а не создание или редактирование.

### Task 6. Создание и редактирование анализа

Цель: реализовать ввод минимальных полей анализа.

Входит:

- создание анализа;
- действие "новый анализ" из списка;
- редактирование названия;
- исходное требование;
- предлагаемое изменение;
- описание ситуации;
- источник изменения;
- сохранение `CreatedAt` и обновление `UpdatedAt`;
- возврат к read-only открытию анализа после сохранения;
- базовая валидация формы без расчета состояния готовности артефакта.

Не входит:

- контекст;
- запуск интеллектуального анализа;
- экспертная оценка;
- расчет `inputIncomplete` и `readyForAnalysis`;
- автоматические workflow-переходы.

Проверки:

- create/edit round-trip;
- validation tests;
- test обновления `UpdatedAt` при редактировании;
- `dotnet test`.

Done:

- анализ можно создать, сохранить, открыть и отредактировать.
- Task 6 не должна подменять Task 7: состояние готовности анализа рассчитывается отдельно.

### Task 7. Состояние артефакта анализа

Цель: реализовать пассивные статусы анализа.

Входит:

- расчет `draft`, `inputIncomplete`, `readyForAnalysis`;
- расчет состояния на основании заполненности основных полей анализа;
- отображение состояния в списке и на странице открытия анализа;
- сохранение или обновление пассивного статуса в рамках операций с анализом, если это требуется выбранной реализацией;
- запрет на трактовку статусов как workflow.

Не входит:

- маршруты согласования;
- уведомления;
- автоматические действия.
- изменение контекста;
- запуск интеллектуального анализа;
- экспертная оценка;
- новые значения enum за пределами уже согласованной модели без отдельного решения.

Проверки:

- unit tests для расчетов статуса.
- tests, что заполненный минимальный набор полей дает `readyForAnalysis`;
- tests, что неполные входные данные дают `inputIncomplete`;
- UI/page test или smoke, что статус отображается, но не запускает действий;
- `dotnet test`.

Done:

- статус отражает состояние артефакта и не запускает сторонние действия.
- статусы остаются пассивными метками состояния, а не workflow согласования.

### Task 8. Ручной ввод фрагментов контекста

Цель: добавить контекст вручную.

Зависимости: Task 5, Task 6, Task 7.

Входит:

- управление ручными фрагментами контекста из карточки сохраненного анализа;
- добавление фрагмента только для существующего анализа;
- поля ручного фрагмента: тип фрагмента, источник, текст;
- обязательность источника и текста;
- список фрагментов текущего анализа;
- отображение типа, источника, текста и даты добавления;
- удаление фрагмента только в рамках текущего `analysisId`;
- обновление `Analysis.UpdatedAt` при добавлении и удалении фрагмента;
- сохранение текущего пассивного статуса анализа без пересчета из-за контекста.

Не входит:

- file upload;
- поиск по контексту;
- интеграции;
- запуск интеллектуального анализа;
- review входных данных перед LLM;
- RAG, embeddings, индексация или поиск по добавленному контексту;
- редактирование существующего фрагмента.

Проверки:

- persistence test add/list/delete;
- page model или UI smoke add/list/delete;
- проверка, что фрагмент нельзя удалить из чужого анализа;
- проверка обновления `UpdatedAt`;
- `dotnet test`.

Done:

- пользователь может управлять ручными фрагментами контекста.

### Task 9. Загрузка Markdown/TXT/JSON

Цель: загружать подготовленные файлы как контекст.

Зависимости: Task 8.

Входит:

- upload `.md`, `.txt`, `.json` для существующего анализа;
- проверка расширения без учета регистра;
- сохранение файла в локальный каталог приложения по схеме `data/uploads/{analysisId}/{storedFileName}`;
- безопасное сохраненное имя файла без доверия к исходному имени upload;
- защита от перезаписи при совпадении имен, например через уникальное сохраненное имя;
- сохранение исходного имени файла в `ContextFragment.FileName`;
- сохранение относительного пути к сохраненному файлу в `ContextFragment.FilePath`;
- сохранение извлеченного текста файла в `ContextFragment.Text`;
- сохранение источника фрагмента: значение, введенное пользователем, или исходное имя файла по умолчанию;
- выбор смыслового типа фрагмента при загрузке;
- отображение загруженных файлов в общем списке фрагментов контекста;
- удаление file-backed фрагмента через механизм удаления Task 8 с удалением локального файла, если он есть;
- отклонение неподдерживаемых расширений без создания `ContextFragment`.

Не входит:

- парсинг сложных корпоративных форматов;
- подключение к внешним системам;
- JSON schema validation;
- смысловой парсинг JSON, Markdown или TXT;
- извлечение структуры из файлов;
- RAG, embeddings, индексация или поиск по загруженным файлам;
- загрузка нескольких файлов одним действием.

Проверки:

- tests allowed extensions;
- tests unsupported extension;
- tests сохранения файла, метаданных и текста фрагмента;
- tests безопасного имени и защиты от перезаписи;
- tests удаления file-backed фрагмента и файла;
- smoke upload;
- `dotnet test`.

Done:

- подготовленные файлы добавляются как контекст анализа.

### Task 10. Review входных данных перед LLM

Цель: показать пользователю, какие данные уйдут в анализ.

Зависимости: Task 5, Task 6, Task 7, Task 8, Task 9.

Входит:

- read-only страница review входных данных для существующего анализа;
- переход на страницу review из карточки сохраненного анализа;
- исходные поля анализа: название, исходное описание, проектный запрос, описание ситуации, источник изменения;
- текущее пассивное состояние анализа;
- состав контекста, добавленного вручную или через файлы;
- для каждого фрагмента контекста: тип, источник, имя файла при наличии, текст, дата добавления;
- явное состояние, если дополнительных фрагментов нет;
- явная пометка, что анализ можно продолжить без дополнительных фрагментов, если минимальные поля анализа заполнены;
- явная пометка, что качество будущего интеллектуального анализа зависит от полноты контекста;
- отсутствие изменений данных при открытии review страницы.

Не входит:

- LLM provider;
- prompt generation;
- request assembly для `IAiAnalysisEngine`;
- запуск интеллектуального анализа;
- сохранение результата интеллектуального анализа;
- редактирование анализа или контекста;
- добавление, удаление или загрузка контекста;
- workflow согласования;
- RAG, embeddings, поиск по контексту или интеграции.

Проверки:

- UI smoke для анализа с контекстом и без контекста;
- test ready analysis without context;
- page model test, что review загружает только данные текущего анализа;
- page model test, что отсутствие контекста не блокирует отображение review;
- page model test, что открытие review не меняет `UpdatedAt`, `Status` и связанные фрагменты;
- `dotnet test`.

Done:

- пользователь видит входные данные перед запуском анализа.
- пользователь видит, что отсутствие дополнительных фрагментов контекста не блокирует дальнейший анализ.

### Task 11. IAiAnalysisEngine contract и request assembly

Цель: ввести интерфейс `IAiAnalysisEngine`, описать внутренний контракт интеллектуального анализа и сбор входного snapshot.

Зависимости: Task 10.

Входит:

- `IAiAnalysisEngine` как application-level boundary для интеллектуального анализа;
- provider-independent request/response contract, пригодный и для DeepSeek, и для demo/mock provider;
- структура запроса на основе сохраненного анализа и его контекста;
- request assembly из `Analysis` и связанных `ContextFragment`;
- input snapshot как стабильное сериализуемое представление входных данных;
- включение только данных, явно введенных или загруженных пользователем в конкретный анализ;
- структура ожидаемого результата без конкретной LLM/provider response schema;
- marker, что результат является preliminary analytical material;
- указание, что LLM/AI не принимает управленческое решение;
- правило, что UI, controllers, pages и бизнес-логика не вызывают LLM напрямую.

Не входит:

- конкретная реализация analysis engine;
- конкретная реализация provider;
- `DirectLlmAnalysisEngine`;
- prompt generation;
- вызов внешней LLM или внешнего AI/RAG-движка;
- retry, streaming, provider configuration;
- сохранение `AiAnalysisResult`;
- UI запуска анализа;
- изменение статуса анализа;
- RAG, embeddings, поиск по контексту или интеграции.

Проверки:

- unit tests request snapshot;
- unit tests no external data beyond user-provided context.
- unit tests, что request assembly включает контекст только текущего анализа;
- unit tests, что contract не содержит зависимостей от конкретного provider SDK;
- `dotnet test`.

Done:

- приложение может построить воспроизводимый provider-independent запрос для интеллектуального анализа.
- в коде есть application-level boundary, через который будущие реализации analysis engine будут вызываться без прямого LLM-вызова из UI/pages/business logic.

### Task 12. DirectLlmAnalysisEngine и provider configuration

Цель: реализовать первую версию `IAiAnalysisEngine` как `DirectLlmAnalysisEngine` и ввести общий provider boundary без привязки доменной логики к конкретному LLM-провайдеру.

Зависимости: Task 11.

Входит:

- `DirectLlmAnalysisEngine`;
- сбор prompt из provider-independent `AiAnalysisRequest`;
- вызов нижнего provider boundary через интерфейс, например `ILlmProvider`;
- provider-independent преобразование ответа provider в `AiAnalysisResponse`;
- возврат `ImpactMap`, raw response, статуса и ошибок через существующий `AiAnalysisResponse`;
- provider interface без зависимости от конкретного SDK;
- request/response модели provider boundary, если они нужны для изоляции `DirectLlmAnalysisEngine`;
- configuration model для выбора provider;
- configuration section/options для DeepSeek как планируемого первого real provider;
- правило хранения API key только во внешней конфигурации или user secrets;
- отсутствие секретов в репозитории.

Не входит:

- реальный сетевой вызов;
- concrete DeepSeek provider implementation;
- deterministic demo/mock provider;
- регистрация demo/mock provider;
- JSON validation и fallback LLM-ответа;
- UI запуска анализа;
- сохранение `AiAnalysisResult`;
- изменение статуса анализа;
- streaming/chat UI.

Проверки:

- unit tests provider selection/configuration;
- unit tests prompt содержит исходные поля анализа, контекст и boundary notice;
- unit tests `DirectLlmAnalysisEngine` вызывает только `ILlmProvider`, а не внешний SDK;
- unit tests provider response преобразуется в `AiAnalysisResponse`;
- unit tests provider failure возвращает failed response без управленческого решения;
- test, что секреты не требуются для сборки и не попадают в репозиторий;
- `dotnet test`.

Done:

- приложение имеет `DirectLlmAnalysisEngine`, общий LLM provider boundary и конфигурационную точку для DeepSeek без зашивания провайдера в доменную модель, UI или бизнес-логику.
- реальный DeepSeek-вызов и локальный demo/mock provider остаются отдельными последующими task.

### Task 13. Deterministic demo/mock provider

Цель: позволить запускать MVP без внешних ключей и сети.

Зависимости: Task 11, Task 12.

Входит:

- deterministic demo/mock provider как реализация существующего `ILlmProvider`;
- регистрация demo/mock provider в DI/configuration selection, чтобы `DirectLlmAnalysisEngine` мог использовать его без внешних ключей и сети;
- генерация валидного `ImpactMap` через существующую доменную модель и `ImpactMap`/`ImpactMapItem` helpers;
- возврат `LlmProviderResponse` со статусом success, raw response и пустым списком ошибок для успешного demo-сценария;
- deterministic поведение: одинаковый provider request дает одинаковый результат;
- отсутствие сетевых вызовов, SDK внешних provider-ов и секретов.

Не входит:

- UI запуска анализа;
- сохранение `AiAnalysisResult`;
- изменение статуса анализа;
- real cloud provider / DeepSeek implementation;
- JSON validation и fallback LLM-ответа;
- RAG, embeddings, external AI/RAG integration;
- streaming/chat UI.

Проверки:

- unit tests, что demo/mock provider возвращает валидный `ImpactMap`;
- unit tests, что результат deterministic для одинакового запроса;
- unit tests, что provider не требует секретов и не обращается к внешней сети;
- unit/integration test provider selection/configuration для demo/mock provider;
- `dotnet test`;
- manual smoke без внешней сети, только если после Task 13 уже есть исполняемый путь через `DirectLlmAnalysisEngine`.

Done:

- демо-анализ может получить валидный LLM-like результат локально через `DirectLlmAnalysisEngine`, без внешних ключей, сети, внешних SDK и секретов.

### Task 14. DeepSeek integration spike

Цель: проверить, что реальный LLM-вызов через DeepSeek совместим с контрактом анализа, без превращения DeepSeek в обязательное условие MVP.

Зависимости: Task 11, Task 12, Task 13, явное подтверждение координатором условий внешней конфигурации и сетевого smoke-вызова.

Перед передачей Task 14 в реализацию координатор явно подтверждает:

- доступность DeepSeek API key только через внешнюю конфигурацию или user secrets, без сохранения секретов в репозитории;
- разрешение на один сетевой smoke-вызов;
- использование только подготовленного обезличенного кейса.

Если хотя бы одно условие не подтверждено, worker не начинает реализацию Task 14 и не добавляет обходной provider. В этом случае фиксируется блокер внешней конфигурации, а MVP продолжает опираться на локальный deterministic demo/mock provider из Task 13.

Входит:

- минимальная реализация `DeepSeekLlmProvider`;
- подключение `DeepSeekLlmProvider` только через существующий `ILlmProvider` / `DirectLlmAnalysisEngine` и существующую конфигурацию `AiAnalysis:DeepSeek`;
- один ручной или интеграционный smoke-запуск на подготовленном обезличенном кейсе;
- сохранение raw response;
- проверка, что ответ можно привести к минимальной структуре результата;
- документирование ограничений или расхождений ответа.

Не входит:

- production-hardening;
- retry policy;
- streaming;
- выбор коммерческой модели;
- использование реальных корпоративных данных;
- изменения UI, pages, доменной модели и контракта `IAiAnalysisEngine`;
- RAG, embeddings, rerank, agentic workflow или аналоги;
- JSON validation, fallback и обработка некорректного LLM-ответа, потому что это scope Task 15.

Проверки:

- `dotnet build`;
- `dotnet test`;
- manual smoke при наличии API key и разрешенного network access;
- интеграционный/smoke-тест с реальным DeepSeek должен быть явно gated/skipped при отсутствии API key или network permission, чтобы обычный `dotnet test` оставался воспроизводимым без секретов и сети.

Done:

- подтверждено, что реальный DeepSeek-вызов может вернуть материал, совместимый с LLM-контрактом, либо зафиксирован блокер внешней конфигурации или ограничения, которые нужно учесть до дальнейшей реализации;
- обычный `dotnet test` остается воспроизводимым без секретов и сети.

### Task 15. Validation и fallback LLM-ответа

Цель: обработать корректный, частичный и некорректный результат интеллектуального анализа.

Зависимости:

- Task 11, Task 12 и Task 13;
- Task 14 не является блокером, если по ней зафиксирован внешний блокер конфигурации или ограничения;
- Task 15 должна быть реализуема и проверяема без DeepSeek, секретов и network access.

Входит:

- validation на application/provider boundary вокруг `DirectLlmAnalysisEngine`, `LlmProviderResponse` и `AiAnalysisResponse`;
- минимально валидная структура response/result object;
- validation errors для критических и некритических проблем ответа;
- возврат raw response в response/result object без потери диагностического материала;
- состояние ошибки без экспертного заключения.

Не входит:

- сложная JSON Schema;
- автоматическое исправление ответа LLM;
- UI запуска анализа;
- persistence результата интеллектуального анализа в БД;
- отображение карты влияния;
- обязательная запись raw response в SQLite;
- расширение prompt сверх минимально необходимого для согласования формата ответа;
- новые `Analysis.Status`, если это не требуется согласованным планом.

Минимально валидный результат:

- `ImpactMap` не null;
- обязательные singleton-секции `changeSummary` и `preliminaryAssessment` присутствуют и имеют стабильные id;
- коллекции карты влияния могут быть пустыми;
- отсутствующие критические секции приводят к `Failed` или equivalent `InvalidResponse` на уровне response;
- частичные некритические проблемы приводят к `Partial` и validation errors.

Fallback:

- при raw-only, malformed JSON или невалидной структуре raw response сохраняется и возвращается как диагностика;
- приложение не создает экспертное заключение и не додумывает карту влияния;
- реализация и тесты остаются локальными, без секретов и сетевых вызовов.

Проверки:

- tests valid response;
- tests missing critical sections;
- tests raw-response fallback.
- tests partial non-critical response;
- обычные unit/integration tests не требуют API key, DeepSeek или network access.

Done:

- приложение не теряет raw response и не выдумывает решение при ошибке;
- Task 15 можно передать в реализацию без зависимости от реального DeepSeek-вызова, секретов, сети, UI запуска анализа и persistence.

### Task 16. Запуск анализа и карта влияния

Цель: связать analysis review, application-level запуск `IAiAnalysisEngine`, сохранение результата и отображение карты влияния.

Зависимости:

- Task 10, Task 11, Task 12, Task 13 и Task 15;
- Task 14 не является блокером, если по ней зафиксирован внешний блокер конфигурации или ограничения;
- Task 16 должна быть реализуема и проверяема без DeepSeek API key, user secrets и network access.

Входит:

- action запуска анализа из review/details flow через application-level service;
- service загружает текущий `Analysis` вместе с `ContextFragment`, собирает request через `IAnalysisInputAssembler`, вызывает `IAiAnalysisEngine` и сохраняет `AiAnalysisResult`;
- Razor Page/PageModel передает данные в application service и не вызывает LLM provider напрямую;
- при отсутствии минимальных входных данных запуск не выполняется, пользователь остается на review/details flow и видит понятное сообщение;
- сохранение результата в существующий `AiAnalysisResult` текущего анализа в пределах уже существующей модели: raw response, input snapshot, status, generated timestamp, impact map, errors/diagnostic text и metadata engine/provider/model/prompt version;
- mapping статусов из `AiAnalysisResponse`: `Succeeded` -> `Completed`, `Partial` -> `CompletedWithWarnings`, `Failed` -> `Failed` или `InvalidResponse` по уже согласованной validation/fallback логике;
- перезапись предыдущего AI-result только до экспертной оценки/экспорта;
- стабильные id элементов карты влияния;
- отображение всех согласованных секций `ImpactMap`;
- отображение raw/error diagnostics для failed/partial результата без создания экспертной оценки или экспертного заключения;
- UI label, что результат является предварительным аналитическим материалом и не является управленческим решением.

Не входит:

- экспертная оценка;
- export;
- DeepSeek/network/secrets scope;
- прямой вызов LLM provider из Razor Page/PageModel;
- защита evaluated/exported snapshots, потому что это scope Task 21.

Границы:

- UI/pages только передают данные в application service, а уже он обращается к `IAiAnalysisEngine`;
- обычный UI smoke и автоматические тесты используют demo/mock provider и не требуют DeepSeek API key, user secrets или network access;
- защита evaluated/exported snapshot не реализуется в Task 16 и остается в Task 21.

Проверки:

- service integration test;
- UI smoke run analysis на demo/mock provider без секретов и сети;
- test stable impact item ids;
- test no run without minimal input;
- tests status mapping from `AiAnalysisResponse`;
- tests failed/partial diagnostics display without expert assessment/conclusion.

Done:

- пользователь запускает анализ через review/details flow и видит структурированную карту влияния;
- результат сохраняется в существующий `AiAnalysisResult` текущего анализа;
- failed/partial результат сохраняет raw/error diagnostics;
- UI явно показывает предварительный, неуправленческий характер результата;
- Task 16 можно передать в реализацию без расширения scope до DeepSeek, сети, секретов и защиты evaluated/exported snapshots.

### Task 17. Экспертная оценка

Цель: позволить человеку проверить результат интеллектуального анализа.

Зависимости:

- Task 16;
- Task 17 должна быть реализуема и проверяема без DeepSeek API key, user secrets и network access.

Входит:

- форма экспертной оценки только для анализа с сохраненным `AiAnalysisResult` и непустой `ImpactMap`;
- отображение всех элементов карты влияния с их стабильными id и возможностью поставить отметку `Confirmed`, `Corrected`, `Rejected`, `NeedsClarification`;
- комментарий к отметке и `CorrectionText` для скорректированных элементов;
- фиксация пропущенных элементов как `ExpertMissedItem`: `ItemType`, `Title`, `Description`, `Severity`, `Comment`;
- фиксация дополнительных корректировок как `ExpertCorrection`, привязанных к `TargetId` стабильного элемента карты;
- общая оценка достаточности контекста через существующий `ContextSufficiencyRating`;
- общая оценка полезности результата через существующий `ResultUsefulnessRating`;
- общий комментарий экспертной оценки;
- сохранение/обновление одной `ExpertEvaluation` текущего анализа без создания экспертного заключения.

Не входит:

- экспертное заключение и статус `ExpertConclusionFixed`;
- выбор итогового решения, варианты split / return for reanalysis;
- export;
- защита evaluated/exported snapshots, потому что это scope Task 21;
- rerun AI analysis;
- DeepSeek/network/secrets scope;
- workflow согласования, назначение эксперта, роли и права доступа.

Границы:

- экспертная оценка не создается для failed/invalid результата без структурированной карты влияния;
- Task 17 не вызывает `IAiAnalysisEngine`, LLM provider или внешние сервисы;
- Task 17 не выполняет автоматических управленческих действий и не создает задач;
- если сохраняется экспертная оценка, она привязывается к текущему анализу и стабильным id элементов карты влияния;
- при повторном сохранении в рамках Task 17 допустимо обновлять существующую `ExpertEvaluation`, но не создавать несколько оценок для одного анализа;
- статус анализа не переводится в `ExpertConclusionFixed`; итоговая фиксация остается scope Task 18.

Проверки:

- save/reload test;
- UI smoke with all mark types;
- test missed item persistence.
- test no evaluation for failed/invalid result without structured impact map;
- test update existing evaluation instead of creating duplicate evaluation for the same analysis;
- tests do not require DeepSeek API key, user secrets or network access.

Done:

- экспертная оценка сохраняется или обновляется как одна `ExpertEvaluation` текущего анализа и привязана к стабильным id элементов карты;
- Task 17 можно передать в реализацию без расширения scope до экспертного заключения, DeepSeek, сети, секретов, rerun AI analysis, workflow согласования и защиты evaluated/exported snapshots.

### Task 18. Экспертное заключение

Цель: зафиксировать итоговое заключение человека.

Входит:

- тип заключения;
- комментарий;
- основание;
- дата фиксации;
- статус `expertConclusionFixed`;
- запрет на автоматические действия для вариантов "split" и "return for reanalysis".

Не входит:

- создание задач;
- отправка уведомлений;
- approval workflow.

Проверки:

- validation tests;
- save/reload test;
- status test.

Done:

- экспертное заключение зафиксировано как часть артефакта анализа.

### Task 19. Markdown export

Цель: сформировать человекочитаемый отчет.

Входит:

- исходные данные;
- контекст;
- карта влияния;
- экспертная оценка;
- экспертное заключение;
- основание;
- пояснение, что LLM не принимает управленческое решение.

Не входит:

- PDF;
- настройка шаблонов отчета пользователем.

Проверки:

- snapshot-style text test;
- manual export/open.

Done:

- Markdown-отчет можно сохранить и использовать в проектных или диссертационных материалах.

### Task 20. JSON export

Цель: сформировать структурированный экспорт для главы 4.

Входит:

- stable top-level fields: `metadata`, `input`, `contextFragments`, `aiAnalysisResult`, `impactMap`, `expertEvaluation`, `expertConclusion`, `exportMetadata`;
- стабильные id элементов карты влияния;
- expert marks;
- missed items;
- corrections;
- conclusion rationale.

Не входит:

- отдельная опубликованная JSON Schema;
- аналитический dashboard.

Проверки:

- JSON shape test;
- parse exported file;
- check required chapter 4 fields.

Done:

- JSON-экспорт пригоден для повторной обработки и экспериментальной проверки.

### Task 21. Защита воспроизводимости результата

Цель: не потерять результат, который был оценен или экспортирован.

Входит:

- правило запрета незаметной перезаписи результата после экспертной фиксации или экспорта;
- clear message при попытке повторного анализа;
- сохранение текущего evaluated/exported snapshot.

Не входит:

- полноценная история всех LLM-запусков;
- аудит экспериментов.

Проверки:

- tests rerun before expert fixation;
- tests rerun after expert fixation/export.

Done:

- результат, использованный для оценки или экспорта, остается воспроизводимым.

### Task 22. End-to-end smoke scenario

Цель: проверить полный MVP-сценарий.

Входит:

- подготовленный обезличенный кейс;
- создание анализа;
- добавление контекста;
- запуск интеллектуального анализа через `DirectLlmAnalysisEngine` с demo provider;
- экспертная оценка;
- экспертное заключение;
- Markdown export;
- JSON export.

Не входит:

- промышленные интеграционные тесты;
- нагрузочное тестирование;
- dashboard главы 4.

Проверки:

- documented smoke checklist или automated smoke test;
- `dotnet build`;
- `dotnet test`.

Done:

- один полный сценарий MVP проходит от входного изменения до экспортов.

## Порядок реализации

Рекомендуемый порядок:

1. Task 1 - скелет solution.
2. Task 2 - конфигурация и SQLite.
3. Task 3 - доменная модель.
4. Task 4a - подготовка EF migrations tooling.
5. Task 4b - EF mapping доменной модели.
6. Task 4c - первая SQLite migration.
7. Task 4d - простой persistence test.
8. Task 5 - список анализов.
9. Task 6 - создание и редактирование анализа.
10. Task 7 - состояние артефакта.
11. Task 8 - ручной контекст.
12. Task 9 - загрузка файлов.
13. Task 10 - review входных данных.
14. Task 11 - IAiAnalysisEngine contract и request assembly.
15. Task 12 - DirectLlmAnalysisEngine и provider configuration.
16. Task 13 - deterministic demo/mock provider.
17. Task 14 - DeepSeek integration spike.
18. Task 15 - validation и fallback.
19. Task 16 - запуск анализа и карта влияния.
20. Task 17 - экспертная оценка.
21. Task 18 - экспертное заключение.
22. Task 19 - Markdown export.
23. Task 20 - JSON export.
24. Task 21 - защита воспроизводимости.
25. Task 22 - end-to-end smoke scenario.

### Рабочий порядок для Task 10-14

Task 10, Task 11, Task 12, Task 13 и Task 14 выполняются строго последовательно.
Task 15 в этот пакет не входит, потому что требует отдельного решения перед началом.

Для каждой task из этого пакета перед реализацией выполняется review постановки:

- достаточно ли конкретны границы task;
- нет ли противоречий с требованиями, техническим проектом и ограничениями MVP;
- не требуется ли уточнение scope до передачи в реализацию;
- не появляется ли преждевременная LLM/RAG/интеграционная функциональность вне текущей task.

Если постановка task требует уточнения, уточнение вносится в этот implementation plan
на основании уже зафиксированных требований, решений и технического проекта.
После этого выполняется review уточнения. Если review уточнения проходит без блокеров,
уточнение фиксируется отдельным документационным commit и push, не смешиваясь с
реализацией task.

После принятия постановки task выполняется стандартный цикл:

1. реализация в отдельном субагенте;
2. review в отдельном субагенте;
3. исправление замечаний отдельным субагентом, если review их выявил;
4. повторный review после исправлений;
5. `git diff --stat`;
6. отдельный commit и push только для этой task.

## Commit и review правила

Для каждой task:

- diff должен соответствовать только scope task;
- перед commit выполняется task review;
- перед commit показывается `git diff --stat`;
- commit выполняется только после явной команды пользователя;
- если task требует build/test, результат фиксируется в ответе.

## Следующий шаг

Перед началом реализации нужно получить отдельное явное решение:

```text
начинаем реализацию Task 1
```

Без такого решения код, project files и тесты не создаются.

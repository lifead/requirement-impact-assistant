# MVP-2 Dify Agent streaming task breakdown

Этот документ фиксирует будущий implementation plan/task breakdown для подключения уже выбранного Dify Agent App как real external RAG provider через streaming Service API.

Реализация в рамках этого запуска не начинается. Ниже перечислены будущие gated tasks: каждая task должна выполняться отдельно, давать один проверяемый результат, проходить review и только после явной команды пользователя попадать в отдельный commit.

## Границы плана

Входит:

- перевод `DifyExternalRagAdapter` с текущего workflow-style blocking сценария на production path `POST /v1/chat-messages`;
- SSE streaming обработка событий `agent_message`, `agent_thought`, `message_end`;
- использование существующей секции `ExternalRag:Dify` без новой параллельной схемы `Dify:*`;
- нейтральный результат через `ExternalRagAdapterResponse`: status, impact map, metadata, retrieved context state/items, warnings/errors, sanitized diagnostic snapshot;
- unit/integration-style проверки через fake handlers без реальной сети и без секретов.

Не входит:

- выбор другой RAG-платформы;
- UI, export, migrations, embeddings, rerank, vector DB, agentic workflow внутри приложения;
- production path через Dify console API;
- изменения DirectLlm, DeepSeek и expert evaluation;
- хранение секретов в коде, документации, логах, тестах или fixtures;
- обязательный real Dify smoke в default `dotnet test`.

## Task 1. Config/endpoint contract и request contract tests

**Цель.** Зафиксировать контракт конфигурации и endpoint normalization для будущего streaming adapter: `ExternalRag:Dify:Endpoint` трактуется как base URL или как полный endpoint и нормализуется к `/v1/chat-messages`.

**Зависимости.** Утвержденные MVP-2 notes по Dify Agent; текущие `DifyExternalRagOptions`; текущий DI fallback на mock/unavailable behavior.

**Входит.**

- Contract tests для вариантов `Endpoint`: base URL, base URL со slash, полный `/v1/chat-messages`.
- Проверка, что новые config keys не требуются.
- Проверка, что `WorkflowOrAppId` остается neutral id/profile value и не становится обязательным секретом.
- Проверка, что при disabled/missing Dify config default path не требует Dify/DeepSeek/network/secrets.

**Не входит.**

- Реальный HTTP вызов в Dify.
- Изменение appsettings или добавление committed local config.
- UI-переключатели или export.

**Ожидаемый diff.**

- Тесты контракта конфигурации/endpoint normalization.
- Минимальная вспомогательная логика normalization, если ее невозможно проверить через существующий adapter без изменения production code.
- Без изменений `appsettings*.json`.

**Проверки.**

- `dotnet test` проходит без сети и секретов.
- Тесты явно проверяют, что `response_mode` будущего request contract должен быть `streaming`, а не `blocking`.
- Тесты не содержат реальных ключей, cookies, CSRF, паролей или bearer tokens.

**Критерии Done.**

- Endpoint contract покрыт тестами.
- Нет новой параллельной config-секции `Dify:*`.
- Fallback при неготовой конфигурации остается контролируемым и воспроизводимым.

**Red flags для review.**

- Появились новые обязательные committed config keys.
- Endpoint захардкожен только под один локальный URL.
- Default tests требуют поднятый Dify или реальные secrets.
- В diff попали `appsettings`, user secrets, logs или реальные diagnostic payloads.

## Task 2. Agent request DTO/body builder

**Цель.** Заменить workflow-style request body mapper на request contract для Dify Agent Service API с `response_mode = streaming`.

**Зависимости.** Task 1; текущий mapper/body builder для blocking workflow; согласованный input contract application-level анализа.

**Входит.**

- DTO или body builder для `POST /v1/chat-messages`.
- Поля `inputs`, `query`, `response_mode`, `conversation_id`, `user` в форме, совместимой с Dify Agent App.
- Маппинг существующих данных анализа в `inputs` и `query` без протекания Dify-модели наружу.
- Unit tests на JSON body и отсутствие `blocking`.

**Не входит.**

- SSE parsing.
- HTTP streaming integration.
- Новые secrets/config для app id.
- Prompt engineering внутри Dify или изменение Agent App.

**Ожидаемый diff.**

- Новый/обновленный Dify Agent request DTO или builder внутри infrastructure boundary.
- Удаление или изоляция workflow-only body contract из production path.
- Тесты сериализации request body.

**Проверки.**

- Serialized body содержит `response_mode: streaming`.
- Body не требует нового committed app secret/config.
- Authorization header не сериализуется в diagnostics или body.

**Критерии Done.**

- Production request body для Agent App описан кодом и покрыт тестами.
- Workflow-style `response_mode = blocking` не используется в новом production path.
- Existing external RAG boundary не меняет публичный application-level contract.

**Red flags для review.**

- DTO смешивает Dify-specific поля с UI/domain/export моделями.
- `WorkflowOrAppId` превращен в обязательный request secret.
- В тестах появились реальные request examples с ключами.

## Task 3. SSE parser unit-tested

**Цель.** Добавить изолированный parser SSE stream для Dify Agent events, чтобы adapter мог читать streaming response предсказуемо и тестируемо.

**Зависимости.** Task 2; Dify Agent event contract для `agent_message`, `agent_thought`, `message_end`.

**Входит.**

- Parser строк SSE `data: ...` с JSON payload.
- Поддержка multi-event stream, пустых строк, неизвестных events и частичных chunks.
- Извлечение answer fragments из `agent_message`.
- Извлечение безопасной metadata из `message_end`.
- Unit tests на happy path, unknown events, malformed JSON, partial stream.

**Не входит.**

- HTTP client integration.
- Mapping в `ExternalRagAdapterResponse`.
- Логирование raw SSE payload целиком.

**Ожидаемый diff.**

- Внутренний parser/reader Dify SSE событий.
- DTO для stream events, если нужны.
- Набор unit tests с synthetic payloads без секретов.

**Проверки.**

- Parser собирает полный answer из нескольких `agent_message`.
- `agent_thought` не ломает поток и не становится публичной моделью без sanitization.
- Malformed event дает контролируемый warning/error state, а не unhandled exception.

**Критерии Done.**

- SSE parsing покрыт unit tests без HTTP и без Dify.
- Parser не раскрывает provider payload за пределы adapter boundary.
- Есть понятное поведение для unknown/malformed/partial stream.

**Red flags для review.**

- Parser завязан на конкретный порядок всех events без fallback.
- Raw event payload сохраняется целиком в result/logs.
- Ошибка одного diagnostic event обрывает usable answer без необходимости.

## Task 4. Streaming HTTP integration в adapter с fake handlers

**Цель.** Подключить request builder и SSE parser в `DifyExternalRagAdapter` через streaming HTTP call к normalized `/v1/chat-messages`.

**Зависимости.** Tasks 1-3; текущий `DifyExternalRagAdapter`; текущая схема DI и fallback.

**Входит.**

- `POST` на normalized endpoint.
- Bearer auth из `ExternalRag:Dify:ApiKey` без логирования значения.
- Чтение response stream до `message_end`, timeout из `TimeoutSeconds`.
- Integration-style tests через fake `HttpMessageHandler`.
- Controlled behavior для HTTP error, timeout, cancellation, incomplete stream.

**Не входит.**

- Real Dify smoke как часть default tests.
- UI progress streaming.
- Новые hosted services или background queues.

**Ожидаемый diff.**

- Обновленный production path `DifyExternalRagAdapter`.
- Fake HTTP handler tests для streaming content.
- Возможное удаление зависимости adapter-а от workflow blocking response DTO из нового path.

**Проверки.**

- Fake handler получает `POST /v1/chat-messages`.
- Request содержит bearer header, но тесты проверяют только факт наличия/маску, не реальное значение.
- Default `dotnet test` не делает network calls.

**Критерии Done.**

- Adapter читает synthetic SSE stream и возвращает контролируемый intermediate result для следующих mapping tasks.
- HTTP failures и stream failures не падают наружу непойманными исключениями.
- DI fallback при disabled/missing Dify не сломан.

**Red flags для review.**

- Adapter вызывает Dify console API.
- Тесты зависят от localhost Dify.
- Authorization или raw payload попали в exception message, logs, snapshots.

## Task 5. Answer JSON parser и raw text fallback

**Цель.** Добавить parser ответа Agent: сначала full answer как JSON, затем JSON substring fallback, затем sanitized raw text fallback.

**Зависимости.** Task 3 или Task 4; ожидаемый JSON response shape из MVP-2 notes.

**Входит.**

- Парсинг полного answer text как JSON.
- Fallback: substring от первого `{` до последнего `}`.
- Controlled result при невалидном JSON.
- Warnings для partial/non-JSON ответа.
- Sanitization до сохранения raw text/diagnostics.

**Не входит.**

- Изменение Dify prompt.
- Строгий отказ от ответа, если JSON не распарсен, но есть полезный raw text.
- Хранение полного provider payload.

**Ожидаемый diff.**

- Внутренний parser/normalizer для Agent answer.
- Unit tests на valid JSON, wrapped JSON, invalid JSON, sensitive-looking fragments.

**Проверки.**

- Валидный structured answer распознается.
- Wrapped JSON распознается через substring fallback.
- Invalid answer возвращает sanitized raw analysis text и warning.
- Secrets-like values маскируются.

**Критерии Done.**

- Parser дает deterministic result без network.
- Raw text fallback сохраняет аналитическую полезность, но не раскрывает чувствительные значения.
- JSON parsing errors отражаются как warnings/errors neutral response pipeline, а не как crash.

**Red flags для review.**

- Parser требует идеальный JSON и теряет весь ответ при небольшом markdown-wrapper.
- Sanitization выполняется после записи diagnostics.
- В tests/fixtures попали реальные секреты или bearer-like значения.

## Task 6. Mapping structured answer и diagnostics в `ExternalRagAdapterResponse`

**Цель.** Нормализовать structured answer, diagnostics и raw fallback в нейтральный `ExternalRagAdapterResponse`.

**Зависимости.** Tasks 4-5; текущая модель `ExternalRagAdapterResponse`; текущая impact map модель.

**Входит.**

- Mapping expected JSON response shape в impact map / metadata текущего приложения.
- Status mapping: completed, completed with warnings, partial/unavailable/failed согласно текущим enum/моделям.
- Sanitized diagnostic snapshot: endpoint profile/base info, message id, conversation id, usage summary, warnings/errors.
- Raw text fallback в нейтральное поле/metadata, если structured answer отсутствует.

**Не входит.**

- Изменение экспертной оценки или принятие управленческого решения.
- Новые UI/export поля.
- Расширение доменной модели под Dify-specific DTO.

**Ожидаемый diff.**

- Adapter-level mapping code.
- Tests на successful structured mapping, partial mapping, raw fallback, diagnostics sanitization.
- Без изменений UI/export.

**Проверки.**

- `ExternalRagAdapterResponse` содержит status, impact map, metadata, warnings/errors.
- Dify-specific DTO не протекают в application/UI boundary.
- Diagnostic snapshot не содержит API key, Authorization header, cookies, CSRF, passwords.

**Критерии Done.**

- Structured answer превращается в usable neutral response.
- Non-JSON answer возвращает honest partial/fallback response.
- Warnings/errors помогают review, но не раскрывают секреты.

**Red flags для review.**

- LLM answer представлен как экспертное заключение.
- Dify event structure стала частью публичной application модели.
- Diagnostics сохраняют raw HTTP headers или полный stream.

## Task 7. Retriever resources и retrieved context mapping

**Цель.** Перенести `message_end.metadata.retriever_resources` в retrieved context state/items без выдумывания источников при отсутствии данных.

**Зависимости.** Tasks 3-6; текущие модели retrieved context в `ExternalRagAdapterResponse`.

**Входит.**

- Mapping доступных retriever resources в neutral retrieved context items.
- Honest state для available, empty, partial, unavailable.
- Warnings при отсутствии retriever resources или неполных полях.
- Tests на populated/empty/missing/malformed resources.

**Не входит.**

- Собственный retrieval pipeline.
- Embeddings, rerank, vector DB.
- Создание или импорт knowledge base документов в репозитории.

**Ожидаемый diff.**

- Mapping code для retriever resources.
- Unit tests с synthetic metadata.
- Возможно, уточнение neutral diagnostics/warnings для retrieved context.

**Проверки.**

- Populated resources становятся retrieved context items.
- Empty/missing resources не превращаются в фиктивные sources.
- Partial metadata дает partial/unavailable state и warning.

**Критерии Done.**

- Retrieved context честно отражает то, что вернул Dify.
- Нет invented source names, fake document ids или synthetic relevance.
- Отсутствие resources не ломает весь анализ, если answer usable.

**Red flags для review.**

- Adapter придумывает retrieved documents из answer text.
- В приложение добавлен собственный RAG/embedding механизм.
- Missing retriever resources ошибочно считаются full success без warning.

## Task 8. Regression, sanitization и manual smoke docs/checks

**Цель.** Завершить MVP-2 streaming adapter регрессионными проверками, security/sanitization checks и отдельной optional manual smoke инструкцией.

**Зависимости.** Tasks 1-7; готовый streaming adapter path.

**Входит.**

- Regression tests на disabled/missing Dify config и mock/unavailable fallback.
- Sanitization tests для diagnostics, warnings, errors, raw answer.
- Проверка, что default `dotnet test` не требует Dify, DeepSeek, network или secrets.
- Optional/manual smoke checklist для локального Dify Agent Service API без включения в default tests.
- Review checklist по ограничениям MVP-2.

**Не входит.**

- Commit без явной команды.
- Хранение локального API key в docs/tests/logs.
- Автоматический network smoke в CI/default tests.
- Изменение UI/export/migrations.

**Ожидаемый diff.**

- Дополнительные regression/sanitization tests.
- Документированный manual smoke сценарий с placeholder-only secret references.
- Возможно, обновление существующих MVP-2 notes/checklist без раскрытия секретов, если это будет отдельно разрешено scope-ом будущей task.

**Проверки.**

- `dotnet test` проходит offline.
- Поиск по diff не находит реальных tokens, cookies, CSRF, passwords, API keys.
- Manual smoke явно помечен как optional/manual и не является gate для default tests.

**Критерии Done.**

- Streaming adapter покрыт regression checks на happy path, fallback, failures, sanitization.
- Manual smoke можно выполнить локально только при внешне настроенном Dify Agent key.
- Документация не содержит секретов и не расширяет scope MVP-2.

**Red flags для review.**

- CI/default tests требуют локальный Dify.
- В manual smoke попал реальный ключ или cookie.
- Regression task незаметно меняет UI/export/DirectLlm/DeepSeek.

## Общий порядок gated delivery

1. Выполнить Task 1, review, commit только по явной команде.
2. Выполнить Task 2, review, commit только по явной команде.
3. Выполнить Task 3, review, commit только по явной команде.
4. Выполнить Task 4, review, commit только по явной команде.
5. Выполнить Task 5, review, commit только по явной команде.
6. Выполнить Task 6, review, commit только по явной команде.
7. Выполнить Task 7, review, commit только по явной команде.
8. Выполнить Task 8, review, commit только по явной команде.

До явного решения о старте конкретной task код adapter-а, тесты, project files и конфигурация не меняются.

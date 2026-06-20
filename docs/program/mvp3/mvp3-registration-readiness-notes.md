# MVP-3 registration readiness notes

## Назначение

Этот документ фиксирует краткие notes о демонстрируемом функциональном составе MVP-3 как программного артефакта. Документ не является юридической заявкой, маркетинговым описанием, PDF-материалом или обещанием функциональности за пределами реализованного MVP.

Программа рассматривается как assistant for preliminary analytical material: она помогает подготовить предварительный аналитический материал по влиянию проектного запроса, но не является субъектом управления, не принимает управленческие решения и не заменяет экспертное заключение человека.

## Демонстрируемый функциональный состав

MVP-3 демонстрирует локальное web-приложение для подготовки и просмотра анализа влияния проектных запросов:

- создание и редактирование карточки анализа с типом проектного запроса, текущим состоянием, проектным изменением, ситуацией, причиной и источником изменения;
- хранение карточки анализа, ручных фрагментов контекста, предварительного результата AI/RAG/LLM, retrieved context metadata/items, экспертной оценки и экспертного заключения;
- различение manual context, retrieved context, preliminary analytical material, expert evaluation и expert conclusion в пользовательском интерфейсе;
- запуск предварительного анализа через application-level boundary `IAnalysisExecutionService`;
- режимы анализа `DirectLlm` и `ExternalRag`, где `ExternalRag` использует adapter boundary, а Dify является только optional concrete provider за этой границей;
- сохранение результата анализа как проверяемого артефакта, включая structured impact map, warnings, limitations и metadata;
- фиксацию человеческой экспертной оценки и экспертного заключения без workflow side effects, taskboard, автоматического reanalysis или поручений;
- Markdown и JSON export сохраненного артефакта анализа, включая input, preliminary result, grounds/retrieved context, expert evaluation, expert conclusion и decision boundary;
- documentation-only demo smoke checklist на обезличенных данных, выполняемый offline/default-safe без обязательного real Dify, DeepSeek, network, user-secrets или API keys.

## Архитектурные границы, важные для готовности

- UI, Razor Pages и export не вызывают LLM/RAG/provider напрямую.
- Запуск анализа идет через `IAnalysisExecutionService`, а выбор режима и provider-ов остается внутри application layer.
- Provider-specific DTO, включая Dify DTO, не являются публичной моделью UI, domain или export.
- Export строится из сохраненного артефакта и не запускает повторный анализ.
- Dify описывается только как optional concrete external AI/RAG provider behind adapter boundary, не как ядро программы и не как обязательная публичная модель.
- Default build/tests и default smoke остаются offline и не требуют real keys, user-secrets, live endpoints или network.

## Known limitations

- Документ не является регистрационной заявкой и не содержит юридического описания объекта регистрации.
- PDF export не входит в MVP-3.
- Собственная RAG knowledge base, embeddings, rerank, vector database и agentic workflow не входят в MVP-3.
- Jira/Confluence/ALM/workflow dashboard, taskboard, assignees, notifications и автоматическое создание задач не входят в MVP-3.
- Real Dify smoke допускается только как optional manual scenario вне default gate и требует отдельной локальной конфигурации с секретами вне репозитория.
- Программа не гарантирует полноту или правильность предварительного анализа без проверки человеком.
- Экспертное заключение фиксирует человек; типы заключения не запускают автоматические управленческие действия.
- Demo checklist использует обезличенный пример и не добавляет production seed data, route, UI button или browser E2E.

## Regression assertions for MVP-3 boundary

Для защиты границ MVP-3 добавлены точечные regression/source assertions:

- pages/export sources не должны напрямую зависеть от Dify adapter/options, `IExternalRagAdapter`, `ILlmProvider`, `HttpClient` или secret-bearing provider configuration;
- UI/domain/export не должны ссылаться на provider-specific DTO;
- export sources не должны вызывать `AnalyzeAsync`, `CompleteAsync`, provider adapters, analysis engines или network APIs;
- default test infrastructure не должна добавлять browser E2E, hosted web test packages, live provider environment gates, user-secrets или network requirements.

Эти assertions являются ограниченными regression checks, а не новым architecture audit framework.

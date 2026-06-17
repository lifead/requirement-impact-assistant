# MVP-2 Dify Agent integration notes

## Dify environment

В MVP-2 provider для внешнего AI/RAG контура - именно Dify. Новый выбор RAG-платформы, сравнение альтернатив и параллельные provider-ветки в рамках этих notes не выполняются.

Базовая среда:

- `DIFY_BASE_URL=http://localhost`;
- UI link: `http://localhost/explore/installed/5ec27517-bbeb-4402-b2c2-ac3b7502b610`;
- Service API endpoint: `POST http://localhost/v1/chat-messages`;
- тип приложения: `agent-chat`.

Архитектурная граница MVP-2:

```text
ExternalRagAnalysisEngine -> IExternalRagAdapter -> DifyExternalRagAdapter -> Dify Agent Service API
```

Dify не должен протекать напрямую в UI, PageModels, domain model, export и экспертную оценку. UI и PageModels выбирают режим анализа и работают с application-level результатом; Dify-specific запросы, SSE-события, diagnostics и fallback parsing остаются внутри adapter boundary.

## app ids

Зафиксированная конфигурация Agent App:

- `DIFY_AGENT_APP_NAME=ria-mvp2-impact-analysis-agent`;
- `DIFY_AGENT_INSTALLED_APP_ID=5ec27517-bbeb-4402-b2c2-ac3b7502b610`;
- `DIFY_AGENT_APP_ID=4fba6f97-d876-4952-8410-f91a5b643571`;
- `DIFY_AGENT_APP_TYPE=agent-chat`.

На текущем этапе новые dedicated configuration keys для `AgentInstalledAppId` или `AgentAppId` не добавляются. Если код не меняется, для Agent App id используется существующий ключ `ExternalRag:Dify:WorkflowOrAppId` как workflow/app identifier.

## endpoint

Production-facing путь для приложения - Dify Service API:

```text
POST http://localhost/v1/chat-messages
```

Для `agent-chat` обязательно использовать:

```json
{
  "response_mode": "streaming"
}
```

`blocking` не использовать: для Agent Chat App он возвращает HTTP 400 `Agent Chat App does not support blocking mode`.

## knowledge base documents

В Dify knowledge base для MVP-2 ожидаются обезличенные демонстрационные документы:

- `01-requirement-create-storage-cell.md`;
- `02-requirement-list-storage-cells.md`;
- `03-api-bins.md`;
- `04-project-decision-in-memory.md`;
- `05-test-create-bin-validation.md`;
- `06-architecture-no-database.md`.

Эти документы используются как внешний knowledge context Dify. Репозиторий на этом шаге не получает собственный retrieval pipeline, embeddings, rerank, vector database или agentic workflow.

## результат анализа текущей конфигурации проекта

Текущая схема проекта уже содержит внешний RAG boundary:

- `ExternalRagAnalysisEngine` работает через `IExternalRagAdapter`;
- `DifyExternalRagAdapter` уже является Dify-specific инфраструктурной реализацией adapter-а;
- `DifyExternalRagOptions.SectionName` равен `ExternalRag:Dify`;
- текущие Dify config keys: `ExternalRag:Dify:Enabled`, `Endpoint`, `WorkflowOrAppId`, `ApiKey`, `TimeoutSeconds`, `ProfileName`;
- новые config keys для этих notes добавлять не нужно.

Причина: текущий `DifyExternalRagOptions` уже покрывает включение адаптера, endpoint, workflow/app id, API key, timeout и profile. Для Agent App id на этом этапе достаточно существующего `ExternalRag:Dify:WorkflowOrAppId`, если реализация adapter-а не меняется.

`appsettings.local.json` в репозитории не найден. Это нормально: локальные секреты и окружение должны задаваться вне tracked config files.

## где сейчас хранится DeepSeek key

DeepSeek key не хранится в репозитории. Предусмотренный configuration key:

```text
AiAnalysis:DeepSeek:ApiKey
```

Для локальной разработки используется user-secrets, environment variables или другой внешний configuration provider. Web project имеет `UserSecretsId`, поэтому локальная схема хранения секретов уже поддержана.

## где должен храниться Dify Agent key

Dify Agent Service API key должен храниться аналогично DeepSeek:

```text
ExternalRag:Dify:ApiKey
```

Для локальной разработки - через user-secrets. Для demo/deploy - через environment variables или внешний secret provider. Не добавлять Dify Agent key в `appsettings*.json`, markdown-отчеты, тестовые fixtures, browser traces, логи, export или screenshots.

## API key acquisition через UI

Ключ берется в Dify UI для установленного Agent App:

```text
http://localhost/explore/installed/5ec27517-bbeb-4402-b2c2-ac3b7502b610
```

После получения ключа он переносится только во внешний secret/configuration storage. В notes, commits, issue text и тесты не вставляются реальные keys, passwords, tokens, cookies или CSRF values.

## user-secrets/env/local config setup согласно текущей схеме проекта

Локальная настройка через user-secrets должна использовать текущие keys и не вводить параллельную секцию `Dify:*`:

```powershell
dotnet user-secrets set "ExternalRag:Dify:Enabled" "true" --project src/RequirementImpactAssistant.Web
dotnet user-secrets set "ExternalRag:Dify:Endpoint" "http://localhost/v1/chat-messages" --project src/RequirementImpactAssistant.Web
dotnet user-secrets set "ExternalRag:Dify:WorkflowOrAppId" "4fba6f97-d876-4952-8410-f91a5b643571" --project src/RequirementImpactAssistant.Web
dotnet user-secrets set "ExternalRag:Dify:ApiKey" "<DIFY_AGENT_SERVICE_API_KEY>" --project src/RequirementImpactAssistant.Web
dotnet user-secrets set "ExternalRag:Dify:TimeoutSeconds" "60" --project src/RequirementImpactAssistant.Web
dotnet user-secrets set "ExternalRag:Dify:ProfileName" "ria-mvp2-impact-analysis-agent" --project src/RequirementImpactAssistant.Web
```

Environment variable names для той же схемы:

```text
ExternalRag__Dify__Enabled=true
ExternalRag__Dify__Endpoint=http://localhost/v1/chat-messages
ExternalRag__Dify__WorkflowOrAppId=4fba6f97-d876-4952-8410-f91a5b643571
ExternalRag__Dify__ApiKey=<DIFY_AGENT_SERVICE_API_KEY>
ExternalRag__Dify__TimeoutSeconds=60
ExternalRag__Dify__ProfileName=ria-mvp2-impact-analysis-agent
```

`appsettings.local.json` не найден и не требуется для этого шага. Если локальный файл появится позже, он не должен попадать в tracked repository и не должен содержать реальные секреты в review artifacts.

## request body для Dify Agent App

Service API request body для Agent App:

```json
{
  "inputs": {
    "originalRequirement": "Система должна позволять создать ячейку хранения с уникальным кодом.",
    "situation": "Изменение требования к созданию ячейки хранения.",
    "source": "demo-api-check"
  },
  "query": "Нужно добавить проверку, что код ячейки не может быть пустым или состоять только из пробелов. Проанализируй влияние на требования, API, тесты, проектные решения и архитектурные ограничения.",
  "response_mode": "streaming",
  "conversation_id": "",
  "user": "ria-mvp2-demo"
}
```

HTTP authorization header должен формироваться из `ExternalRag:Dify:ApiKey` внутри adapter-а. Значение ключа не логируется и не сохраняется.

## streaming SSE behavior

Agent Chat App отвечает через SSE stream. Adapter должен читать события последовательно до завершения stream, собирать текст ответа из `agent_message.answer` и отдельно сохранять безопасную diagnostic metadata из финальных событий.

Минимальное ожидаемое поведение:

- stream read не должен блокировать UI напрямую;
- timeout берется из `ExternalRag:Dify:TimeoutSeconds`;
- partial stream failure возвращается как partial/failed adapter response с sanitized diagnostics;
- raw SSE payload не становится публичной моделью приложения.

## event mapping

Mapping SSE events:

- `agent_message.answer` -> собрать полный answer;
- `message_end.message_id` -> `diagnosticMetadata.messageId`;
- `message_end.conversation_id` -> `diagnosticMetadata.conversationId`;
- `message_end.metadata.usage` -> `diagnosticMetadata.usage`;
- `message_end.metadata.retriever_resources` -> `retrievedContext`, если заполнено.

Если `retriever_resources` отсутствует или пустой, retrieved context не нужно выдумывать. Нужно зафиксировать limitation/warning и вернуть честное состояние retrieved context.

## JSON parsing/fallback

Fallback parsing для Agent answer:

1. собрать полный текст из `agent_message.answer`;
2. parse full answer as JSON;
3. если не получилось, извлечь substring от первого `{` до последнего `}` и parse;
4. если JSON распарсен, map structured result / ImpactMap draft и diagnostics;
5. если нет, сохранить sanitized raw analysis text, structured result null/empty, добавить warning.

Sanitization применяется до сохранения diagnostics/raw text. Нельзя сохранять keys, passwords, bearer tokens, cookies, CSRF, session ids или полный чувствительный provider payload.

## expected JSON response shape

Ожидаемая форма JSON в `agent_message.answer`:

```json
{
  "changeSummary": "",
  "affectedRequirements": [],
  "affectedTasks": [],
  "affectedProjectDecisions": [],
  "affectedApiInterfacesDocumentsTests": [],
  "affectedArchitecturalConstraints": [],
  "affectedOrganizationalContextItems": [],
  "contradictions": [],
  "missingInformation": [],
  "clarificationQuestions": [],
  "risks": [],
  "optionsForExpertReview": [],
  "preliminaryAssessment": "",
  "usedSources": [],
  "warnings": []
}
```

Эта структура является ожидаемым output contract для MVP-2 Agent prompt/adapter normalization, но экспертное решение не подменяется LLM-ответом.

## mapping к `ExternalRagAdapterResponse` / текущим retrieved context и diagnostics моделям

Предлагаемое semantic mapping:

- успешный parse JSON + usable answer -> `ExternalRagAdapterResponse.Status = Completed` или `CompletedWithWarnings`;
- partial parse, отсутствующий retrieved context или warning от Dify -> `Status = Partial` или `CompletedWithWarnings`;
- network/API/timeout/invalid stream без usable answer -> `Status = Failed`;
- parsed structured JSON -> `ImpactMap`;
- warnings из JSON/fallback/adapter-а -> `Warnings`;
- fatal adapter issues -> `Errors`;
- безопасные `messageId`, `conversationId`, `usage`, provider status и parsing notes -> `Metadata.DiagnosticMetadata`;
- sanitized compact snapshot -> `SanitizedDiagnosticSnapshot`;
- `message_end.metadata.retriever_resources` -> `RetrievedContextItems`.

Текущие модели retrieved context позволяют фиксировать `RetrievedContextState.Available`, `MetadataOnly`, `Partial` или `Unavailable`, а элементы - как `FullText`, `ExcerptOnly`, `MetadataOnly` или `Unavailable`. Для Dify Agent на этом этапе корректнее ожидать partial retrieved context, потому что Service API может вернуть fragments/metadata, но не гарантирует полный исходный документ и не является stable export contract.

## retrieved context behavior, с честным `retrievedContextState = partial`

Для MVP-2 Dify Agent integration notes базовое ожидаемое состояние:

```text
retrievedContextState = partial
```

Причина: `message_end.metadata.retriever_resources` может содержать полезные retrieved fragments, document metadata, scores и source references, но это не гарантирует полный текст всех использованных knowledge base документов. Adapter должен:

- сохранять доступные source/document identifiers, titles, excerpts/fragments, scores и metadata;
- помечать элементы как `ExcerptOnly` или `MetadataOnly`, если full text отсутствует;
- не повышать состояние до `Available`, если нет полного и проверяемого retrieved context;
- добавлять warning/limitation о partial retrieved context;
- не скрывать отсутствие context items, если Dify не вернул `retriever_resources`.

## limitations

Ограничения notes:

- adapter implementation на этом шаге не начинается;
- новые config keys не добавляются;
- default build/test не должны требовать real Dify network, Dify API key или локальный running Dify;
- console API не является production path;
- Dify Agent response shape зависит от prompt и может требовать prompt hardening;
- Service API stream может завершиться без retrieved resources даже при успешном answer;
- retrieved context от Dify считается partial, пока не доказано наличие полного и стабильного retrieved payload;
- Dify-specific raw payload не становится stable export schema.

## security rules

Правила безопасности:

- не хранить реальные Dify/DeepSeek API keys в репозитории;
- не добавлять keys в `appsettings*.json`;
- не логировать authorization headers, bearer values, cookies, CSRF, session ids, passwords или raw secret-bearing payloads;
- diagnostics должны быть sanitized и минимальными;
- UI, PageModels, domain model, export и экспертная оценка не должны зависеть от Dify-specific DTO;
- default tests не должны требовать внешние secrets или production-like Dify session;
- для demo использовать обезличенные документы и запросы.

## console API не production path

Console API можно использовать только для diagnostic/manual investigation:

```text
GET /console/api/installed-apps/{installed_app_id}/messages?conversation_id={conversation_id}&limit=10
```

Этот путь требует user session, cookies и CSRF, поэтому он не является production path, не должен использоваться adapter-ом и не должен становиться default tests path. Production-facing integration идет через Dify Service API `POST http://localhost/v1/chat-messages`.

## self-check notes

Этот документ не требует code changes на текущем шаге. Он фиксирует интеграционные notes для будущей реализации adapter-а через уже существующую application-level boundary.

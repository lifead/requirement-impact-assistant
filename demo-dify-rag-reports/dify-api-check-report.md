# Dify API check report

## API check method

Workflow API endpoint:

```text
POST <DIFY_URL>/v1/workflows/run
```

Headers:

```text
Authorization: Bearer <WORKFLOW_API_KEY>
Content-Type: application/json; charset=utf-8
```

Important client note: JSON request bodies with Russian text must be sent as UTF-8 bytes. A PowerShell string body without explicit UTF-8 encoding produced corrupted input and incorrect LLM interpretation during the first check.

Temporary API keys were created only for local checks and then removed. No API key is stored in this report.

## Sanitized requests

### Request 1: happy path

```json
{
  "inputs": {
    "originalRequirement": "Система должна позволять создать ячейку хранения с уникальным кодом.",
    "projectRequest": "Нужно добавить проверку, что код ячейки не может быть пустым или состоять только из пробелов.",
    "situation": "Изменение требования к созданию ячейки хранения.",
    "source": "demo-api-check",
    "manualContext": ""
  },
  "response_mode": "blocking",
  "user": "ria-mvp2-demo"
}
```

Result:

- HTTP status: 200
- Workflow status: `succeeded`
- Response time: about 6.1 seconds
- `analysisResult`: present as JSON string
- `retrievedContext`: present as array, 6 items
- Relevant context included `TEST-001`, `REQ-001`, `API-001`, and `POST /bins`.

### Request 2: PostgreSQL conflict

```json
{
  "inputs": {
    "originalRequirement": "На этапе MVP допускается хранение данных в памяти приложения.",
    "projectRequest": "Нужно сохранять ячейки хранения в PostgreSQL.",
    "situation": "Предлагаемое техническое изменение хранения данных.",
    "source": "demo-api-check",
    "manualContext": ""
  },
  "response_mode": "blocking",
  "user": "ria-mvp2-demo"
}
```

Result:

- HTTP status: 200
- Workflow status: `succeeded`
- Response time: about 11.1 seconds
- `analysisResult`: present as JSON string
- `retrievedContext`: present as array, 6 items
- LLM result identified a conflict with MVP in-memory storage constraints and asked for expert review.

### Request 3: no relevant context

```json
{
  "inputs": {
    "originalRequirement": "",
    "projectRequest": "Нужно добавить интеграцию с платёжной системой для оплаты заказов.",
    "situation": "Новая функциональность вне текущего контекста базы знаний.",
    "source": "demo-api-check",
    "manualContext": ""
  },
  "response_mode": "blocking",
  "user": "ria-mvp2-demo"
}
```

Result:

- HTTP status: 200
- Workflow status: `succeeded`
- Response time: about 8.8 seconds
- `analysisResult`: present as JSON string
- `retrievedContext`: present as array, 6 low-relevance items
- LLM result did not invent payment documents and populated missing information / clarification context.

## Retrieved context state

`retrievedContextState = available`

Dify workflow API returned retrieved context through `data.outputs.retrievedContext`.

## Suitability for requirement-impact-assistant

The workflow is suitable as a demo external RAG contour for later connection through `DifyExternalRagAdapter`, with these adapter expectations:

- send UTF-8 JSON body;
- call `POST <DIFY_URL>/v1/workflows/run` with blocking mode;
- read `data.outputs.analysisResult`;
- parse `analysisResult` as JSON string;
- read `data.outputs.retrievedContext`;
- preserve warnings and raw diagnostics without secrets.

## Future changes for DifyExternalRagAdapter

- Add configuration for `<DIFY_URL>` and `<WORKFLOW_API_KEY>` outside repository files.
- Add DTOs for Dify workflow response and neutral `ExternalRagAdapterResponse`.
- Parse JSON string from `data.outputs.analysisResult`.
- Map retrieved context to neutral evidence/context items.
- Add explicit handling for no-context / low-confidence scenarios.
- Add sanitized diagnostics that never include API keys, cookies, passwords, CSRF tokens, or full request headers.

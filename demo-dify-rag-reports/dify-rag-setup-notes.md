# Dify RAG setup notes

## Scope

This note describes a demo external RAG contour for `requirement-impact-assistant`.

Dify URL: `<DIFY_URL>`

Dataset / Knowledge Base: `ria-mvp2-demo-knowledge`

Dataset id: `<DATASET_ID>`

The data is synthetic and anonymized. No corporate data, passwords, API keys, or tokens are stored here.

## Uploaded documents

- `01-requirement-create-storage-cell.md`
- `02-requirement-list-storage-cells.md`
- `03-api-bins.md`
- `04-project-decision-in-memory.md`
- `05-test-create-bin-validation.md`
- `06-architecture-no-database.md`

All 6 documents were uploaded to the Dify dataset and reached `completed` indexing status.

## Dataset settings

- Indexing technique: `high_quality`
- Embedding model: `text-embedding-v4`
- Embedding provider: configured Dify provider for OpenAI-compatible embeddings
- Chunking: automatic/default Dify process rule
- Retrieval mode: semantic search
- `top_k`: 6
- Score threshold: disabled
- Rerank: disabled

`top_k` was increased from 5 to 6 after the PostgreSQL conflict query showed that `ARCH-001` was indexed but ranked just outside the top 5.

## Retrieval checks

### Control query

Query:

```text
Что должно произойти, если code ячейки хранения состоит только из пробелов?
```

Result:

- Found `REQ-001. Создание ячейки хранения`.
- The retrieved fragment states that if the code is missing or contains only spaces, the system must return a validation error.
- No invented documents were returned.

### Query 1: validation happy path

Query:

```text
Нужно добавить проверку, что code ячейки хранения не может быть пустым или состоять только из пробелов. Какие требования, API и тесты затронуты?
```

Relevant retrieved context:

- `API-001. API ячеек хранения`
- `TEST-001. Проверка валидации создания ячейки`
- `REQ-001. Создание ячейки хранения`

Observation: expected requirement, API, and test context is present.

### Query 2: PostgreSQL conflict

Query:

```text
Нужно сохранять ячейки хранения в PostgreSQL. Какие ограничения и проектные решения затронуты?
```

Relevant retrieved context with `top_k = 6`:

- `DEC-001. Проектное решение по хранению данных`
- `ARCH-001. Архитектурное ограничение MVP`

Observation: the architecture constraint is retrievable but low-ranked. The workflow and adapter should treat this as a context-quality limitation and should keep warnings / expert-review questions.

### Query 3: no relevant context

Query:

```text
Нужно добавить интеграцию с платёжной системой для оплаты заказов.
```

Result:

- Dify still returns low-score storage-cell fragments because score threshold is disabled.
- The LLM prompt must explicitly warn that retrieved context is weak or unrelated.
- The adapter should preserve warnings and avoid presenting weak retrieval as authoritative evidence.

## Limitations

- Console API is an internal Dify API, not a stable public contract.
- Score threshold is disabled for recall during the demo, so no-context requests can return low-relevance fragments.
- `top_k = 6` is required for the PostgreSQL scenario to include `ARCH-001`.
- No Dify secrets are recorded in this report.

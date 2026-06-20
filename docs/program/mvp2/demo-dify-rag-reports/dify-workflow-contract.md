# Dify workflow contract

## Workflow identification

Application name: `ria-mvp2-impact-analysis-workflow`

Purpose: demo workflow for requirement impact analysis with external RAG.

Dify URL: `<DIFY_URL>`

Workflow API key: `<WORKFLOW_API_KEY>`

Dataset id: `<DATASET_ID>`

## Workflow structure

```text
Start -> Knowledge Retrieval -> LLM -> Output
```

## Input contract

The workflow accepts:

| Field | Type | Required |
| --- | --- | --- |
| `originalRequirement` | string | yes |
| `projectRequest` | string | yes |
| `situation` | string | yes |
| `source` | string | yes |
| `manualContext` | string | no |

Knowledge Retrieval uses `projectRequest` as the retrieval query. The other fields are passed to the LLM prompt.

## Retrieval contract

Dataset: `ria-mvp2-demo-knowledge`

Retrieval settings:

- mode: semantic search
- `top_k`: 6
- score threshold: disabled
- rerank: disabled

The Knowledge Retrieval node returns `result`, which is exposed through the Output node as `retrievedContext`.

`retrievedContextState`: `available`

## Output contract

The workflow returns:

| Field | Meaning |
| --- | --- |
| `analysisResult` | LLM result. In the tested Dify response this is a JSON document encoded as a string. |
| `retrievedContext` | Array of retrieved context objects returned by Dify Knowledge Retrieval. |

The tested service API response shape is:

```text
task_id
workflow_run_id
data.id
data.workflow_id
data.status
data.outputs.analysisResult
data.outputs.retrievedContext
data.error
data.elapsed_time
data.total_tokens
data.total_steps
data.created_at
data.finished_at
```

## Mapping to neutral adapter response

```text
Dify workflow response
  -> neutral ExternalRagAdapterResponse
  -> ImpactMap / InfluenceMap
```

Suggested mapping:

| Neutral field | Dify source |
| --- | --- |
| `analysisResult` | `data.outputs.analysisResult` |
| `retrievedContext` | `data.outputs.retrievedContext` |
| `retrievedContextState` | constant/configured as `available` for this workflow |
| `warnings` | parse from `analysisResult.warnings` after JSON string parsing |
| `metadata` | `task_id`, `workflow_run_id`, `data.workflow_id`, `data.elapsed_time`, `data.total_tokens`, `data.total_steps` |
| `rawDiagnosticSnapshot` | sanitized subset of the Dify response without API keys, cookies, passwords, or tokens |

## Error behavior

Expected adapter handling:

- non-2xx HTTP status -> external RAG call failure
- missing `data.outputs.analysisResult` -> invalid Dify response
- `analysisResult` not parseable as JSON -> preserve raw string and report JSON format warning
- missing `retrievedContext` -> set `retrievedContextState = unavailable`
- empty retrieved context -> set `retrievedContextState = available` with empty list and add warning if relevant

## Security notes

- Store `<WORKFLOW_API_KEY>` outside git and markdown reports.
- Do not log full Dify responses if they can contain secrets.
- Do not store passwords, cookies, CSRF tokens, or API keys in repository files.
- Use synthetic, anonymized documents only.

## Open questions before changing C# project

- Should `DifyExternalRagAdapter` parse `analysisResult` immediately, or pass the JSON string through a neutral DTO first?
- Should the adapter call only workflow API, or also call dataset hit-testing/retrieval API for diagnostics?
- Should low-score retrieved context be filtered by the adapter, by Dify threshold, or by the LLM prompt warning policy?
- Should `top_k = 6` remain demo-specific or become configurable?

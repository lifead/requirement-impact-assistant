# MVP-2 Dify Agent manual smoke checklist

This checklist is optional and manual. It is not part of default `dotnet test`,
CI, build verification, or the Task 8 regression gate.

## Preconditions

- A local Dify Agent App is already configured outside this repository.
- The Service API endpoint is available, for example `http://localhost/v1/chat-messages`.
- The API key is provided only from external secret storage or interactive input.
- No real key, cookie, CSRF value, bearer token, response trace, or provider payload is committed to the repo.
- Default build and test commands must still pass without Dify, DeepSeek, network, user-secrets, or local services.

## Placeholder configuration

Use placeholders in notes, scripts, issues, and review artifacts:

```text
ExternalRag:Dify:Enabled=true
ExternalRag:Dify:Endpoint=http://localhost/v1/chat-messages
ExternalRag:Dify:WorkflowOrAppId=<DIFY_AGENT_APP_ID>
ExternalRag:Dify:ApiKey=<DIFY_AGENT_API_KEY>
ExternalRag:Dify:TimeoutSeconds=60
ExternalRag:Dify:ProfileName=ria-mvp2-impact-analysis-agent
```

Do not run or paste `dotnet user-secrets list` output into docs, logs, tests, or review comments.

If using user-secrets locally, set values interactively with placeholders replaced only in your local shell:

```powershell
dotnet user-secrets set "ExternalRag:Dify:Enabled" "true" --project src/RequirementImpactAssistant.Web
dotnet user-secrets set "ExternalRag:Dify:Endpoint" "http://localhost/v1/chat-messages" --project src/RequirementImpactAssistant.Web
dotnet user-secrets set "ExternalRag:Dify:WorkflowOrAppId" "<DIFY_AGENT_APP_ID>" --project src/RequirementImpactAssistant.Web
dotnet user-secrets set "ExternalRag:Dify:ApiKey" "<DIFY_AGENT_API_KEY>" --project src/RequirementImpactAssistant.Web
dotnet user-secrets set "ExternalRag:Dify:TimeoutSeconds" "60" --project src/RequirementImpactAssistant.Web
dotnet user-secrets set "ExternalRag:Dify:ProfileName" "ria-mvp2-impact-analysis-agent" --project src/RequirementImpactAssistant.Web
```

## Manual smoke steps

1. Start the web app locally with the externally configured Dify Agent settings.
2. Create or open a demo analysis that uses anonymized requirement/context data only.
3. Run analysis in External AI/RAG mode.
4. Confirm the request completes without exposing Dify-specific DTOs in UI or export surfaces.
5. Confirm the result remains preliminary analytical material and does not present an expert decision.
6. Confirm retrieved context state is honest: `Partial`, `MetadataOnly`, or `Unavailable` unless full retrieved source text is actually available.
7. Confirm diagnostics show only sanitized provider/adapter metadata, endpoint scheme/host/path, message/conversation ids when safe, usage summary, warnings, and errors.
8. Confirm diagnostics, warnings, errors, raw fallback text, screenshots, and browser traces do not contain real API keys, bearer values, cookies, CSRF values, passwords, or raw provider payloads.

## Review checklist

- No automatic network smoke was added to default tests.
- No `appsettings*.json`, committed fixtures, logs, screenshots, or markdown files contain a real Dify or DeepSeek key.
- No real Dify call is required for `dotnet test`.
- Disabled or missing Dify config keeps the local mock/unavailable fallback path deterministic.
- Dify integration remains behind `IExternalRagAdapter` and the application-level analysis boundary.
- UI, export, migrations, DirectLlm, and DeepSeek behavior are unchanged by Task 8.
- No RAG pipeline, embeddings, rerank, vector database, Dify console API path, or agentic workflow was added inside the application.

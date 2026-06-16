# MVP-1 demo readiness review package

## Project

- Project: `requirement-impact-assistant`.
- Purpose: MVP prototype for preliminary requirement impact analysis of project change requests. The AI/LLM/RAG result is preliminary analytical material only; expert review and management consideration remain with a human.
- Branch: `mvp1`.
- Status before package preparation: `## mvp1...origin/mvp1`, working tree clean.

## Latest commits

```text
97b7b24 Remove context file upload surface
5eeb238 Configure local console logging
88bfab1 Handle context upload failures gracefully
67d7362 Stabilize SQLite schema initialization
584fefb Update MVP-1 stage 9 summary status
4dfc138 Add MVP-1 stage 9 summary
5f8c0a9 Avoid forbidden secret literal in Dify warning sanitizer
6ebf1e8 Sanitize Dify provider warnings
d63f805 Add MVP-1 stage 9 task breakdown
74a258b Add MVP-1 stage 8 summary
```

## MVP-1 scope status

Stages 1-9 are closed for the current MVP-1 line:

1. Minimal model and contracts for analysis mode and retrieved context.
2. Markdown/JSON export extensions for mode, provider/adapter metadata and retrieved context.
3. Neutral external RAG adapter contract.
4. Mock external RAG adapter for local reproducible checks.
5. Minimal Dify adapter as the first external AI/RAG adapter implementation.
6. UI support for selecting analysis mode and viewing retrieved context/limitations.
7. MVP-1 smoke scenario.
8. Architecture and reproducibility regression tests.
9. Final MVP-1 gate review.

Post-gate stabilization is also present: SQLite schema startup migration stabilization, controlled handling of context upload failure, console logging without EventLog dependency, and removal of the context file upload surface.

## Implemented for MVP-1

- `DirectLlm` and `ExternalRag` analysis modes.
- Application-level analysis boundary through `IAiAnalysisEngine` and `IAiAnalysisEngineSelector`.
- LLM provider boundary through `ILlmProvider`.
- Demo local LLM provider for reproducible local use.
- DeepSeek provider for real Direct LLM mode when configured.
- Neutral external RAG adapter contract through `IExternalRagAdapter`.
- Mock external RAG adapter for local External RAG demonstration without secrets or network.
- Dify external RAG adapter behind the adapter boundary.
- Persistence for analysis metadata and retrieved context items.
- Markdown and JSON export of saved results.
- Expert evaluation and expert conclusion remain separate from AI/LLM/RAG output.
- Regression tests for boundaries, export reproducibility, provider/adapter separation and secret sanitization.

## Local run

Recommended local demo run:

```powershell
$env:DOTNET_CLI_HOME='C:\git\requirement-impact-assistant\.dotnet_home'
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project src\RequirementImpactAssistant.Web\RequirementImpactAssistant.Web.csproj --urls http://localhost:5100
```

The launch profile also exposes `http://localhost:5297` in Development. The application applies EF Core migrations on startup and uses SQLite under `src/RequirementImpactAssistant.Web/App_Data/`, which is ignored and excluded from the review archive.

`src/RequirementImpactAssistant.Web/wwwroot/` is excluded from the review archive by package rules. It currently contains default/minimal static assets (`site.css`, `site.js`, Bootstrap/jQuery vendor assets) and no critical custom demo logic.

## Demo/mock mode

Development configuration uses:

```json
"AiAnalysis": {
  "Provider": "Demo"
}
```

In this mode Direct LLM analysis uses the local demo provider and requires no API key, real network or real corporate data. External RAG mode uses the mock external RAG adapter when Dify is not fully configured.

## DirectLlm mode

Direct LLM mode is selected in the UI before running analysis. In Development it uses `AiAnalysis:Provider=Demo`. For real DeepSeek-backed Direct LLM, set:

- `AiAnalysis:Provider=DeepSeek`;
- `AiAnalysis:DeepSeek:Model`, default `deepseek-chat`;
- `AiAnalysis:DeepSeek:BaseUrl`, default `https://api.deepseek.com`;
- `AiAnalysis:DeepSeek:ApiKey` from user secrets, environment variables or another external configuration source.

The API key must not be stored in `appsettings*.json` or committed files.

## ExternalRag mode

External RAG mode is selected in the UI before running analysis. The mode is executed through `ExternalRagAnalysisEngine` and `IExternalRagAdapter`.

Current activation behavior:

- if `ExternalRag:Dify` is fully configured, DI registers `DifyExternalRagAdapter`;
- otherwise DI falls back to `MockExternalRagAdapter`;
- default build/test and local demo do not require Dify configuration.

Required Dify settings for the real adapter:

- `ExternalRag:Dify:Enabled=true`;
- `ExternalRag:Dify:Endpoint` as an absolute HTTP/HTTPS URI;
- `ExternalRag:Dify:WorkflowOrAppId`;
- `ExternalRag:Dify:ApiKey` from user secrets, environment variables or another external configuration source;
- optional `ExternalRag:Dify:TimeoutSeconds`;
- optional `ExternalRag:Dify:ProfileName`.

Dify is used as an external AI/RAG provider through an adapter. The project does not implement its own RAG, embeddings, rerank or vector database.

## Secrets and local data

- Real keys or secrets in repository: no.
- `appsettings.json` contains provider/model/base URL defaults only, no API key.
- `appsettings.Development.json` uses local demo mode, no API key.
- SQLite database files are ignored and excluded from the archive.
- User secrets are not part of the repository and are not included.

## Build and test

Commands run:

```powershell
$env:DOTNET_CLI_HOME='C:\git\requirement-impact-assistant\.dotnet_home'
dotnet build RequirementImpactAssistant.sln

$env:DOTNET_CLI_HOME='C:\git\requirement-impact-assistant\.dotnet_home'
dotnet test RequirementImpactAssistant.sln
```

Results on 2026-06-15:

- `git diff --check`: passed, no output.
- `dotnet build RequirementImpactAssistant.sln`: passed on standalone rerun, 0 warnings, 0 errors.
- `dotnet test RequirementImpactAssistant.sln`: passed, 340 passed, 0 failed, 0 skipped, total 340.
- Note: an initial concurrent build/test launch caused a temporary `obj` file lock in build. The standalone build immediately after tests passed cleanly.

## Not manually checked

- Full browser walkthrough was not performed during package preparation.
- Real Dify network call was not performed.
- Real DeepSeek network call was not performed.
- Real corporate knowledge base data was not used.
- Load, performance, security penetration and production deployment checks were not performed.

## Suggested demonstration scenario

1. Start the app in Development on `http://localhost:5100`.
2. Create a new analysis card with anonymized project change request data.
3. Add manual context in the form.
4. Run `DirectLlm` using the local Demo provider.
5. Review impact map, questions, risks and preliminary analytical material.
6. Run `ExternalRag` with default mock adapter and compare retrieved context state.
7. Show that manual context and retrieved context are represented separately.
8. Add expert evaluation and expert conclusion.
9. Export Markdown and JSON and verify that mode, engine/provider/adapter metadata and retrieved context state are present.
10. Emphasize that the AI/LLM/RAG output is not a management decision.

## Questions before real Dify/DeepSeek connection

- Which Dify endpoint/workflow/app will be used for MVP demonstration?
- What Dify response contract is expected and which retrieved context fields are mandatory?
- Where will Dify and DeepSeek secrets be stored for the demo environment?
- What sanitized, non-corporate test data will be used for real provider checks?
- Which timeout/retry policy is acceptable for Dify and DeepSeek demonstration?
- What manual acceptance criteria should confirm that real provider output is suitable for research/demo use?
- Whether a separate optional integration test profile should be added for real providers.

## Preserved MVP-1 limitations

- Own RAG implementation: not implemented.
- Embeddings: not implemented.
- Rerank: not implemented.
- Vector database: not implemented.
- Jira/Confluence/ALM/workflow platform behavior: not implemented.
- Management decisions, task creation, requirement/code modification by AI: not implemented.
- Dify remains an external AI/RAG provider through adapter.
- DeepSeek remains an external LLM provider only when configured.
- Default test/build must not require Dify/DeepSeek secrets, real network or real corporate data.
- UI, export and page models must not directly call Dify, DeepSeek, adapter or provider layers.

## Review focus

External review should focus on:

- demo readiness of the local Development flow;
- correctness of Dify/DeepSeek configuration boundaries;
- absence of secrets and local databases in the package;
- preservation of application-level and provider/adapter boundaries;
- sufficiency of tests for further MVP development;
- remaining manual checks before showing the app with real providers.

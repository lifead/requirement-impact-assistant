# Stage 8 test inventory MVP-1

## Назначение

Документ фиксирует scope inventory и gap review существующих architecture/reproducibility tests перед добавлением Stage 8 guards.

Это Task 1 Stage 8. Документ не добавляет новые тесты, не меняет production code и не является Stage 8 summary.

## Основание

- `docs/program/mvp1/task-breakdown-stage-8.md`
- `docs/program/mvp1/stage-7-summary.md`
- `docs/program/mvp1/task-breakdown-stage-7.md`
- `docs/program/mvp1/stage-6-summary.md`
- `tests/RequirementImpactAssistant.Tests/RequirementImpactAssistant.Tests.csproj`

## Scope Task 1

Входит:

- documentation-only inventory/gap review;
- фиксация уже существующего coverage после Stage 7;
- фиксация likely gaps для Stage 8 Tasks 2-5;
- разделение smoke coverage и будущих architecture/reproducibility regression guards.

Не входит:

- новые architecture tests;
- новые или измененные production files;
- Dify/network tests;
- secrets/config;
- migrations;
- MVP-0 changes;
- `docs/program/mvp1/stage-8-summary.md`.

## Inventory existing coverage

| Категория | Уже есть после Stage 7 | Основные test files | Наблюдение для Stage 8 |
| --- | --- | --- | --- |
| Selector/registry | DI registration and application service selection are covered for direct/external paths. Page invalid mode parsing is covered before service call. | `tests/RequirementImpactAssistant.Tests/Configuration/ApplicationConfigurationTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/AnalysisExecutionServiceTests.cs`; `tests/RequirementImpactAssistant.Tests/Pages/AnalysisPagesTests.cs` | Нет отдельного dedicated selector/registry regression file. Unknown enum/unavailable engine handling mostly implicit or test-double based. |
| `DirectLlm` / `ExternalRag` boundary | Direct path persists direct metadata without retrieved context; external path persists adapter/provider metadata and retrieved context state/items. Direct engine dependency boundary is guarded. | `tests/RequirementImpactAssistant.Tests/Application/DirectLlmAnalysisEngineTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/ExternalRagAnalysisEngineTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/AnalysisExecutionServiceTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage3ArchitectureRegressionTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage4ArchitectureRegressionTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage5ArchitectureRegressionTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage6ArchitectureRegressionTests.cs` | Good coverage exists, but Stage 8 can sharpen non-substitution checks: direct mode must not require retrieved context or external adapter; external mode must remain behind adapter boundary. |
| Retrieved context states | Enum round-trip covers all four states. Mock adapter, external engine, execution service, details page, Markdown export and JSON export cover `Available`, `MetadataOnly`, `Unavailable`, `Partial` in different places. | `tests/RequirementImpactAssistant.Tests/Domain/AnalysisModeAndRetrievedContextStateTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/MockExternalRagAdapterTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/ExternalRagAnalysisEngineTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/AnalysisExecutionServiceTests.cs`; `tests/RequirementImpactAssistant.Tests/Pages/AnalysisPagesTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/AnalysisMarkdownExportServiceTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/AnalysisJsonExportServiceTests.cs` | Coverage is broad but distributed. Stage 8 Task 3 should consolidate the matrix and tie warnings/limitations to state outcomes. |
| Export matrix | Markdown/JSON export includes direct and external smoke baseline, saved mock external profiles, retrieved context states, expert layer and legacy MVP-0 fallbacks. Export architecture guards prevent engine/provider/adapter/network dependencies. | `tests/RequirementImpactAssistant.Tests/Application/AnalysisMarkdownExportServiceTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/AnalysisJsonExportServiceTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/ExportArchitectureTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage6ArchitectureRegressionTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage7SmokeRegressionTests.cs` | Strong existing coverage. Stage 8 Task 4 should make the direct/external/legacy Markdown/JSON matrix explicit and keep saved-result-only guards visible. |
| UI/PageModel boundary | Review page passes selected `AnalysisMode` to application service. Invalid mode values are rejected before service call. Details page reads persisted metadata/retrieved context without provider-specific UI. Page/source architecture guards block direct engine/adapter/provider/Dify/network dependencies. | `tests/RequirementImpactAssistant.Tests/Pages/AnalysisPagesTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage6ArchitectureRegressionTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage3ArchitectureRegressionTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage4ArchitectureRegressionTests.cs` | Existing guards are source/type based and useful. Task 2 can reference them while adding selector-focused regression guards instead of duplicating page smoke. |
| Dify config independence / no secrets | Missing or partial Dify config keeps mock adapter or disabled options. Committed production configuration is guarded against Dify endpoint/API key/secrets. Stage 7 smoke fixture records network disabled and neutral local names. | `tests/RequirementImpactAssistant.Tests/Configuration/DifyExternalRagOptionsTests.cs`; `tests/RequirementImpactAssistant.Tests/Configuration/ApplicationConfigurationTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage5ArchitectureRegressionTests.cs`; `tests/RequirementImpactAssistant.Tests/Support/Mvp1SmokeBaselineFixtureTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage7SmokeRegressionTests.cs` | Default `dotnet test` reproducibility is described by existing tests, but Task 5 should add an explicit guard for no mandatory Dify/network/secrets/manual-provider dependency. |
| External adapter error sanitization | Dify adapter tests cover disabled/incomplete config, HTTP error, timeout, malformed JSON, incomplete response, failed provider status and sensitive status text without exposing API key, endpoint, Authorization/Bearer or raw sensitive body. External engine sanitizes adapter exception diagnostics. | `tests/RequirementImpactAssistant.Tests/Application/DifyExternalRagAdapterTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/ExternalRagAnalysisEngineTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/AnalysisExecutionServiceTests.cs` | Good adapter-specific coverage already exists. Task 3 should present it as a matrix and ensure warnings/limitations remain tied to resulting state. |
| Smoke vs architecture guard overlap | Stage 7 smoke covers reproducible direct/external paths, saved metadata, expert layer and export. Stage 3-6 architecture guards cover boundaries and forbidden dependencies. | `tests/RequirementImpactAssistant.Tests/Application/Stage7SmokeRegressionTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage6ArchitectureRegressionTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage5ArchitectureRegressionTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage4ArchitectureRegressionTests.cs`; `tests/RequirementImpactAssistant.Tests/Application/Stage3ArchitectureRegressionTests.cs` | Stage 8 should avoid re-running Stage 7 as a user workflow. New checks should be targeted guards/matrices around existing boundaries. |

## Gap review for Tasks 2-5

### Task 2. Selector/registry and mode boundary

Likely gaps:

- dedicated selector/registry regression guards for `AiAnalysisEngineSelector`;
- explicit unknown enum/unavailable mode handling at selector/application boundary;
- sharper checks that `DirectLlm` and `ExternalRag` cannot substitute each other;
- direct mode guard that retrieved context and external adapter are not required;
- external mode guard that provider/adapter metadata and retrieved context state are preserved only through the external boundary;
- page/application boundary check can reuse existing `AnalysisPagesTests` and Stage 6 source/type guards instead of broad new page smoke.

Candidate existing anchors:

- `tests/RequirementImpactAssistant.Tests/Configuration/ApplicationConfigurationTests.cs`
- `tests/RequirementImpactAssistant.Tests/Application/AnalysisExecutionServiceTests.cs`
- `tests/RequirementImpactAssistant.Tests/Pages/AnalysisPagesTests.cs`
- `src/RequirementImpactAssistant.Web/Application/Analysis/AiAnalysisEngineSelector.cs`

### Task 3. Retrieved context and sanitization matrix

Likely gaps:

- one consolidated retrieved context matrix for `Available`, `MetadataOnly`, `Unavailable`, `Partial`;
- explicit mapping from state to warnings/limitations/result status;
- explicit assertion that manual context and retrieved context remain separate in state outcome tests;
- external adapter error sanitization matrix that names timeout, unavailable/config error, HTTP error, malformed response, incomplete response and failed provider status;
- clear link between sanitized diagnostics and persisted/exported state outcomes.

Candidate existing anchors:

- `tests/RequirementImpactAssistant.Tests/Application/MockExternalRagAdapterTests.cs`
- `tests/RequirementImpactAssistant.Tests/Application/ExternalRagAnalysisEngineTests.cs`
- `tests/RequirementImpactAssistant.Tests/Application/DifyExternalRagAdapterTests.cs`
- `tests/RequirementImpactAssistant.Tests/Application/AnalysisExecutionServiceTests.cs`
- `tests/RequirementImpactAssistant.Tests/Support/Mvp1SmokeBaselineFixtureTests.cs`

### Task 4. Export reproducibility matrix

Likely gaps:

- explicit direct/external/legacy export reproducibility matrix across Markdown and JSON;
- saved-result-only guard stated per format and per result type;
- direct saved result guard for unavailable retrieved context without artificial items;
- external saved result guard for retrieved context states/items/limitations;
- legacy MVP-0 saved result guard for usable export with MVP-1 fallbacks;
- no dependency on `IAiAnalysisEngine`, selector, adapter or provider during export.

Candidate existing anchors:

- `tests/RequirementImpactAssistant.Tests/Application/AnalysisMarkdownExportServiceTests.cs`
- `tests/RequirementImpactAssistant.Tests/Application/AnalysisJsonExportServiceTests.cs`
- `tests/RequirementImpactAssistant.Tests/Application/ExportArchitectureTests.cs`
- `tests/RequirementImpactAssistant.Tests/Application/Stage6ArchitectureRegressionTests.cs`

### Task 5. Default test reproducibility

Likely gaps:

- explicit default `dotnet test` guard against mandatory Dify endpoint/API key, network, secrets or real corporate data;
- guard that mock adapter path does not read secrets and does not initiate network;
- source/config guard for new test fixtures against real API keys, Authorization values and secret-like literals;
- confirmation that optional/manual Dify or DeepSeek provider checks are not required by the default suite;
- decision whether fake handler/no-network checks are enough, or whether a source-token guard is also needed.

Candidate existing anchors:

- `tests/RequirementImpactAssistant.Tests/Configuration/DifyExternalRagOptionsTests.cs`
- `tests/RequirementImpactAssistant.Tests/Configuration/ApplicationConfigurationTests.cs`
- `tests/RequirementImpactAssistant.Tests/Application/DifyExternalRagAdapterTests.cs`
- `tests/RequirementImpactAssistant.Tests/Application/DeepSeekLlmProviderTests.cs`
- `tests/RequirementImpactAssistant.Tests/Application/Stage5ArchitectureRegressionTests.cs`
- `tests/RequirementImpactAssistant.Tests/Application/Stage7SmokeRegressionTests.cs`

## Smoke vs architecture guard decision

Stage 7 smoke tests already prove one reproducible MVP-1 path through application services, saved result, expert layer and Markdown/JSON export. Stage 8 should not duplicate that as another broad workflow.

Preferred Task 2-5 shape:

- targeted unit/application tests for selector, execution service, adapter mapping and export services;
- source/type architecture guards only where they protect boundaries better than workflow assertions;
- compact matrices for retrieved context states, adapter error sanitization and export formats;
- default-suite reproducibility guards that stay local, deterministic and secret-free.

## Explicit non-goals

- No new architecture tests in Task 1.
- No production code.
- No Dify/network tests.
- No secrets/config.
- No migrations.
- No MVP-0 changes.
- No Stage 8 summary.
- No commit/push as part of Task 1.

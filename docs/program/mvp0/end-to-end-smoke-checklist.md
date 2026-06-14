# End-to-end smoke checklist for MVP flow

Task: Task 22 from `implementation-plan.md`.

This checklist verifies the full MVP path without DeepSeek, user secrets, external LLM providers, external services, or network access. The scenario uses `DirectLlmAnalysisEngine` with the local demo provider.

## Preconditions

- Work from repository root: `C:\git\requirement-impact-assistant`.
- Use Development configuration and explicitly keep `AiAnalysis:Provider=Demo`.
- Do not add API keys or user secrets for this smoke run.
- Do not switch provider to `DeepSeek`.

Verification commands:

```powershell
$env:DOTNET_CLI_HOME='C:\git\requirement-impact-assistant\.dotnet_home'; dotnet build RequirementImpactAssistant.sln
$env:DOTNET_CLI_HOME='C:\git\requirement-impact-assistant\.dotnet_home'; dotnet test RequirementImpactAssistant.sln
```

Optional manual run command:

```powershell
$env:DOTNET_CLI_HOME='C:\git\requirement-impact-assistant\.dotnet_home'
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:AiAnalysis__Provider='Demo'
dotnet run --project src\RequirementImpactAssistant.Web\RequirementImpactAssistant.Web.csproj --urls http://localhost:5297
```

If the local SQLite database has not been created yet, apply existing migrations before the manual run:

```powershell
$env:DOTNET_CLI_HOME='C:\git\requirement-impact-assistant\.dotnet_home'
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet tool restore
dotnet tool run dotnet-ef database update --project src\RequirementImpactAssistant.Web\RequirementImpactAssistant.Web.csproj --startup-project src\RequirementImpactAssistant.Web\RequirementImpactAssistant.Web.csproj
```

Expected configuration:

- `src/RequirementImpactAssistant.Web/appsettings.Development.json` contains `AiAnalysis:Provider=Demo`.
- The manual run override `AiAnalysis__Provider=Demo` is present.
- No DeepSeek API key is required.
- No user secrets are required.
- No network access is required.

## Prepared anonymized case

Use this data for the smoke scenario.

Analysis input:

- Title: `Anonymous payment status field change`
- Original description: `The existing internal payment status response contains the fields id, status, amount, and updatedAt. Downstream services use status to determine whether fulfillment can continue.`
- Project request: `Add a new optional statusReason field to the internal payment status response so support staff can distinguish manual review, fraud review, and technical retry cases.`
- Situation description: `The change is requested for an internal operations workflow. Several services read the response, and documentation, tests, and rollout notes may need updates.`
- Change source: `Anonymized change request CR-SMOKE-001`

Manual context fragment:

- Type: `ApiDescription`
- Source: `Anonymized architecture note`
- Text: `Consumers include fulfillment, support dashboard, and reconciliation jobs. Backward compatibility must be preserved. The field must be optional during rollout, documented for API consumers, and covered by contract tests.`

Expert evaluation data:

- For all generated impact map items, keep a mix of marks such as `Confirmed`, `NeedsClarification`, and `Corrected`.
- Add comments that the result is a preliminary analytical material and remains subject to human expert review.
- If a correction text is required for a corrected item, use: `Clarify rollout and contract-test impact for optional statusReason.`
- Add one missed item:
  - Type: `AffectedApiInterfaceDocumentTest`
  - Title: `Consumer contract test update`
  - Description: `Contract tests should verify that consumers tolerate the optional statusReason field.`
  - Severity: `Medium`
  - Comment: `Added by the expert during smoke review.`
- Overall ratings:
  - Context sufficiency: `Sufficient`.
  - Result usefulness: `Useful`.
  - General comment: `Demo result is useful as preliminary input, but final impact assessment is fixed by the expert.`

Expert conclusion data:

- Conclusion type: `AcceptWithLimitations`.
- Comment: `Smoke conclusion for anonymized payment status field change.`
- Rationale: `The expert reviewed the demo AI impact map, added a missing contract-test item, and fixed the final conclusion without relying on external providers.`

## Manual smoke steps and expected results

1. Open `http://localhost:5297/Analyses/New`.
2. Create an analysis using the prepared anonymized case.
   - Expected: the app redirects to the analysis details page.
   - Expected status: `ReadyForAnalysis`.
3. On the details page, add the manual context fragment.
   - Expected: the fragment appears under `Context fragments`.
   - Expected: no file upload or external service is needed.
4. Open `Review input`.
   - Expected: the prepared input and context fragment are visible.
   - Expected: the page says minimum analysis fields are filled and shows `Run preliminary analysis`.
5. Click `Run preliminary analysis`.
   - Expected: the app returns to the analysis details page with a success/info message.
   - Expected analysis status: `NeedsExpertEvaluation`.
   - Expected AI result status: `Completed` or `CompletedWithWarnings`.
   - Expected engine/provider evidence:
     - Engine: `DirectLlmAnalysisEngine`
     - Provider: `Demo`
     - Model: demo provider model value, for example `demo-deterministic`
   - Expected: a structured impact map, raw response, and input snapshot are visible.
6. Open `Expert evaluation`.
   - Expected: generated impact map items are available for human marks.
7. Save the expert evaluation using the prepared evaluation data.
   - Expected: the page shows `Expert evaluation saved.`
   - Expected status remains `NeedsExpertEvaluation` until the expert conclusion is fixed.
   - Expected: `Expert conclusion` action is available.
8. Open `Expert conclusion`.
9. Save the expert conclusion using the prepared conclusion data.
   - Expected status after details refresh: `ExpertConclusionFixed`.
   - Expected: fixed timestamp is visible.
   - Expected: `Download Markdown` and `Download JSON` actions are available.
10. Click `Download Markdown`.
    - Expected: a `.md` file is returned.
    - Expected content includes:
      - analysis title;
      - export metadata;
      - input section;
      - context fragments;
      - structured impact map;
      - expert evaluation;
      - expert conclusion.
11. Click `Download JSON`.
    - Expected: a `.json` file is returned.
    - Expected top-level fields include:
      - `metadata`;
      - `input`;
      - `contextFragments`;
      - `aiAnalysisResult`;
      - `impactMap`;
      - `expertEvaluation`;
      - `expertConclusion`;
      - `exportMetadata`.

## Pass criteria

- Full path is verified: analysis creation -> context -> demo AI analysis -> expert evaluation -> expert conclusion -> Markdown export -> JSON export.
- The provider shown in the saved result is `Demo`.
- The smoke run does not require a DeepSeek API key, user secrets, or network access.
- The run does not change analysis, expert evaluation, expert conclusion, Markdown export, JSON export, or snapshot protection business logic.
- `dotnet build RequirementImpactAssistant.sln` succeeds.
- `dotnet test RequirementImpactAssistant.sln` succeeds.

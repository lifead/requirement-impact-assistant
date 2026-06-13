using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Application.Analysis;

internal static class AiAnalysisResponseValidator
{
    public static AiAnalysisResponse Validate(
        LlmProviderResponse providerResponse,
        AnalysisBoundaryNotice boundaryNotice)
    {
        var rawResponse = providerResponse.RawResponse ?? string.Empty;
        var errors = providerResponse.Errors?.ToList() ?? [];

        if (providerResponse.Status == LlmProviderResponseStatus.Failed)
        {
            return new AiAnalysisResponse(
                AiAnalysisResponseStatus.Failed,
                ImpactMap: null,
                RawResponse: rawResponse,
                Errors: errors,
                BoundaryNotice: boundaryNotice);
        }

        var impactMap = providerResponse.ImpactMap;
        if (impactMap is null)
        {
            errors.Add("LLM response is invalid: impact map is missing.");

            return Failed(rawResponse, errors, boundaryNotice);
        }

        var criticalErrors = ValidateCriticalSections(impactMap);
        if (criticalErrors.Count > 0)
        {
            errors.AddRange(criticalErrors);

            return Failed(rawResponse, errors, boundaryNotice);
        }

        var validationErrors = ValidateCollectionItems(impactMap);
        errors.AddRange(validationErrors);

        var status = validationErrors.Count > 0
            ? AiAnalysisResponseStatus.Partial
            : MapStatus(providerResponse.Status);

        return new AiAnalysisResponse(
            status,
            impactMap,
            rawResponse,
            errors,
            boundaryNotice);
    }

    private static AiAnalysisResponse Failed(
        string rawResponse,
        IReadOnlyList<string> errors,
        AnalysisBoundaryNotice boundaryNotice) =>
        new(
            AiAnalysisResponseStatus.Failed,
            ImpactMap: null,
            RawResponse: rawResponse,
            Errors: errors,
            BoundaryNotice: boundaryNotice);

    private static List<string> ValidateCriticalSections(ImpactMap impactMap)
    {
        List<string> errors = [];

        ValidateSingleton(
            impactMap.ChangeSummary,
            ImpactMapItemType.ChangeSummary,
            "changeSummary",
            errors);
        ValidateSingleton(
            impactMap.PreliminaryAssessment,
            ImpactMapItemType.PreliminaryAssessment,
            "preliminaryAssessment",
            errors);

        return errors;
    }

    private static void ValidateSingleton(
        ImpactMapItem? item,
        ImpactMapItemType expectedItemType,
        string sectionKey,
        List<string> errors)
    {
        if (item is null)
        {
            errors.Add($"LLM response is invalid: required section '{sectionKey}' is missing.");
            return;
        }

        var expectedId = ImpactMapIds.CreateItemId(expectedItemType, 1);
        if (!string.Equals(item.Id, expectedId, StringComparison.Ordinal))
        {
            errors.Add($"LLM response is invalid: required section '{sectionKey}' must have stable id '{expectedId}'.");
        }

        if (item.ItemType != expectedItemType)
        {
            errors.Add($"LLM response is invalid: required section '{sectionKey}' must have item type '{expectedItemType}'.");
        }
    }

    private static List<string> ValidateCollectionItems(ImpactMap impactMap)
    {
        List<string> errors = [];

        ValidateCollection(impactMap.AffectedRequirements, ImpactMapItemType.AffectedRequirement, "affectedRequirements", errors);
        ValidateCollection(impactMap.AffectedTasks, ImpactMapItemType.AffectedTask, "affectedTasks", errors);
        ValidateCollection(impactMap.AffectedProjectDecisions, ImpactMapItemType.AffectedProjectDecision, "affectedProjectDecisions", errors);
        ValidateCollection(impactMap.AffectedApiInterfacesDocumentsTests, ImpactMapItemType.AffectedApiInterfaceDocumentTest, "affectedApiInterfacesDocumentsTests", errors);
        ValidateCollection(impactMap.AffectedArchitecturalConstraints, ImpactMapItemType.AffectedArchitecturalConstraint, "affectedArchitecturalConstraints", errors);
        ValidateCollection(impactMap.AffectedOrganizationalContextItems, ImpactMapItemType.AffectedOrganizationalContextItem, "affectedOrganizationalContextItems", errors);
        ValidateCollection(impactMap.Contradictions, ImpactMapItemType.Contradiction, "contradictions", errors);
        ValidateCollection(impactMap.MissingInformation, ImpactMapItemType.MissingInformation, "missingInformation", errors);
        ValidateCollection(impactMap.ClarificationQuestions, ImpactMapItemType.ClarificationQuestion, "clarificationQuestions", errors);
        ValidateCollection(impactMap.Risks, ImpactMapItemType.Risk, "risks", errors);
        ValidateCollection(impactMap.OptionsForExpertReview, ImpactMapItemType.OptionForExpertReview, "optionsForExpertReview", errors);

        return errors;
    }

    private static void ValidateCollection(
        IReadOnlyList<ImpactMapItem> items,
        ImpactMapItemType expectedItemType,
        string sectionKey,
        List<string> errors)
    {
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var ordinal = index + 1;
            var expectedId = ImpactMapIds.CreateItemId(expectedItemType, ordinal);

            if (!string.Equals(item.Id, expectedId, StringComparison.Ordinal))
            {
                errors.Add($"LLM response has a non-critical validation issue: item {ordinal} in '{sectionKey}' must have stable id '{expectedId}'.");
            }

            if (item.ItemType != expectedItemType)
            {
                errors.Add($"LLM response has a non-critical validation issue: item {ordinal} in '{sectionKey}' must have item type '{expectedItemType}'.");
            }
        }
    }

    private static AiAnalysisResponseStatus MapStatus(LlmProviderResponseStatus status) =>
        status switch
        {
            LlmProviderResponseStatus.Succeeded => AiAnalysisResponseStatus.Succeeded,
            LlmProviderResponseStatus.Partial => AiAnalysisResponseStatus.Partial,
            LlmProviderResponseStatus.Failed => AiAnalysisResponseStatus.Failed,
            _ => AiAnalysisResponseStatus.Failed
        };
}

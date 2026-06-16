using System.Globalization;
using System.Text;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;
using DomainAnalysis = RequirementImpactAssistant.Web.Domain.Analysis;

namespace RequirementImpactAssistant.Web.Application.Export;

public sealed class AnalysisMarkdownReportBuilder
{
    public string Build(DomainAnalysis analysis, DateTimeOffset exportedAt)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var builder = new StringBuilder();
        AppendHeading(builder, 1, analysis.Title);
        AppendMetadata(builder, analysis, exportedAt);
        AppendAnalysisResultMetadata(builder, analysis.AiAnalysisResult);
        AppendRetrievedContext(builder, analysis.AiAnalysisResult);
        AppendInput(builder, analysis);
        AppendContextFragments(builder, analysis.ContextFragments);
        AppendImpactMap(builder, analysis.AiAnalysisResult?.ImpactMap);
        AppendExpertEvaluation(builder, analysis.ExpertEvaluation);
        AppendExpertConclusion(builder, analysis);
        AppendBoundaryNotice(builder);

        return builder.ToString();
    }

    private static void AppendMetadata(StringBuilder builder, DomainAnalysis analysis, DateTimeOffset exportedAt)
    {
        AppendHeading(builder, 2, "Export metadata");
        AppendBullet(builder, "Exported at", FormatDate(exportedAt));
        AppendBullet(builder, "Analysis status", analysis.Status.ToString());
        builder.AppendLine();
    }

    private static void AppendAnalysisResultMetadata(StringBuilder builder, AiAnalysisResult? aiAnalysisResult)
    {
        AppendHeading(builder, 2, "Analysis result metadata");

        if (aiAnalysisResult is null)
        {
            builder.AppendLine("No AI analysis result metadata was saved.");
            builder.AppendLine();
            return;
        }

        var metadata = aiAnalysisResult.Metadata;

        AppendBullet(builder, "Analysis mode", metadata.AnalysisMode.ToString());
        AppendBullet(builder, "Engine", FirstNonBlank(metadata.EngineName, aiAnalysisResult.EngineName));
        AppendBullet(builder, "Provider", metadata.ProviderName);
        AppendBullet(builder, "Adapter", metadata.AdapterName);
        AppendBullet(builder, "Model workflow profile", metadata.ModelWorkflowProfileName);
        AppendBullet(
            builder,
            "Manual context forwarded to external AI or RAG",
            metadata.ManualContextForwardedToExternalAiOrRag.ToString());
        AppendBullet(builder, "Retrieved context state", metadata.RetrievedContextState.ToString());
        AppendWarnings(builder, metadata.Warnings);
    }

    private static void AppendRetrievedContext(StringBuilder builder, AiAnalysisResult? aiAnalysisResult)
    {
        AppendHeading(builder, 2, "Retrieved context");

        if (aiAnalysisResult is null)
        {
            builder.AppendLine("No AI analysis result was saved, so no retrieved context was saved.");
            builder.AppendLine();
            return;
        }

        var metadata = aiAnalysisResult.Metadata;
        AppendBullet(builder, "State", metadata.RetrievedContextState.ToString());
        AppendField(builder, "Limitation note", FormatRetrievedContextLimitation(metadata.RetrievedContextState));

        if (metadata.RetrievedContextItems.Count == 0)
        {
            builder.AppendLine("No retrieved context items were saved.");
            builder.AppendLine();
            return;
        }

        var itemNumber = 1;
        foreach (var item in metadata.RetrievedContextItems)
        {
            AppendHeading(builder, 3, $"Retrieved context item {itemNumber}: {EmptyIfBlank(item.SourceTitle)}");
            AppendBullet(builder, "Source title", item.SourceTitle);
            AppendBullet(builder, "Source id", item.SourceId);
            AppendBullet(builder, "External reference", item.ExternalReference);
            AppendBullet(builder, "Fragment id", item.FragmentId);
            AppendBullet(builder, "URL or reference", item.UrlOrReference);
            AppendBullet(builder, "Rank", item.Rank?.ToString(CultureInfo.InvariantCulture));
            AppendBullet(builder, "Score", FormatScore(item.Score));
            AppendBullet(builder, "Provider", item.ProviderName);
            AppendBullet(builder, "Adapter", item.AdapterName);
            AppendBullet(builder, "Completeness", item.Completeness.ToString());
            AppendBullet(builder, "Warning or limitation note", item.WarningOrLimitationNote);

            AppendOptionalFencedBlock(builder, "Text", item.Text);
            AppendOptionalFencedBlock(builder, "Excerpt", item.Excerpt);

            itemNumber++;
        }
    }

    private static void AppendWarnings(StringBuilder builder, IReadOnlyCollection<string> warnings)
    {
        AppendHeading(builder, 3, "Warnings");

        var savedWarnings = warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => warning.Trim())
            .ToList();

        if (savedWarnings.Count == 0)
        {
            builder.AppendLine("No warnings were saved.");
            builder.AppendLine();
            return;
        }

        foreach (var warning in savedWarnings)
        {
            builder.AppendLine($"- {warning}");
        }

        builder.AppendLine();
    }

    private static void AppendInput(StringBuilder builder, DomainAnalysis analysis)
    {
        AppendHeading(builder, 2, "Input");
        AppendField(builder, "Original requirement", analysis.OriginalDescription);
        AppendField(builder, "Proposed change", analysis.ProjectRequest);
        AppendField(builder, "Situation", analysis.SituationDescription);
        AppendField(builder, "Change source", analysis.ChangeSource);
    }

    private static void AppendContextFragments(StringBuilder builder, IReadOnlyCollection<ContextFragment> fragments)
    {
        AppendHeading(builder, 2, "Context fragments");

        if (fragments.Count == 0)
        {
            builder.AppendLine("No context fragments were saved.");
            builder.AppendLine();
            return;
        }

        var orderedFragments = fragments
            .OrderBy(fragment => fragment.CreatedAt)
            .ThenBy(fragment => fragment.Id)
            .ToList();

        foreach (var fragment in orderedFragments)
        {
            AppendHeading(builder, 3, $"{fragment.Type}: {EmptyIfBlank(fragment.Source)}");
            AppendBullet(builder, "Identifier", fragment.Id.ToString());
            AppendBullet(builder, "Created", FormatDate(fragment.CreatedAt));
            AppendBullet(builder, "File name", fragment.FileName ?? "Not provided");
            AppendFencedBlock(builder, "Content", fragment.Text);
        }
    }

    private static void AppendImpactMap(StringBuilder builder, ImpactMap? impactMap)
    {
        AppendHeading(builder, 2, "Structured impact map");

        if (impactMap is null)
        {
            builder.AppendLine("No structured impact map was saved.");
            builder.AppendLine();
            return;
        }

        AppendImpactSection(builder, "Change summary", [impactMap.ChangeSummary]);
        AppendImpactSection(builder, "Affected requirements", impactMap.AffectedRequirements);
        AppendImpactSection(builder, "Affected tasks", impactMap.AffectedTasks);
        AppendImpactSection(builder, "Affected project decisions", impactMap.AffectedProjectDecisions);
        AppendImpactSection(
            builder,
            "Affected API, interfaces, documents, and tests",
            impactMap.AffectedApiInterfacesDocumentsTests);
        AppendImpactSection(builder, "Affected architectural constraints", impactMap.AffectedArchitecturalConstraints);
        AppendImpactSection(builder, "Affected organizational context", impactMap.AffectedOrganizationalContextItems);
        AppendImpactSection(builder, "Risks", impactMap.Risks);
        AppendImpactSection(builder, "Contradictions", impactMap.Contradictions);
        AppendImpactSection(builder, "Clarification questions", impactMap.ClarificationQuestions);
        AppendImpactSection(builder, "Missing information", impactMap.MissingInformation);
        AppendImpactSection(builder, "Options for expert review", impactMap.OptionsForExpertReview);
        AppendImpactSection(builder, "Preliminary assessment", [impactMap.PreliminaryAssessment]);
    }

    private static void AppendImpactSection(
        StringBuilder builder,
        string title,
        IReadOnlyList<ImpactMapItem> items)
    {
        AppendHeading(builder, 3, title);

        if (items.Count == 0)
        {
            builder.AppendLine("No items.");
            builder.AppendLine();
            return;
        }

        foreach (var item in items)
        {
            builder.AppendLine($"- **{EmptyIfBlank(item.Id)}** ({item.ItemType}, severity: {item.Severity})");
            AppendNestedBullet(builder, "Title", item.Title);
            AppendNestedBullet(builder, "Description", item.Description);
            AppendNestedBullet(builder, "Related context fragments", FormatRelatedContext(item.RelatedContextFragmentIds));
            AppendNestedBullet(builder, "Notes", item.Notes);
        }

        builder.AppendLine();
    }

    private static void AppendExpertEvaluation(StringBuilder builder, ExpertEvaluation? evaluation)
    {
        AppendHeading(builder, 2, "Expert evaluation");

        if (evaluation is null)
        {
            builder.AppendLine("No expert evaluation was saved.");
            builder.AppendLine();
            return;
        }

        AppendBullet(builder, "Context sufficiency", evaluation.ContextSufficiency.ToString());
        AppendBullet(builder, "Result usefulness", evaluation.ResultUsefulness.ToString());
        AppendField(builder, "General comment", evaluation.GeneralComment);
        AppendEvaluatedItems(builder, evaluation.EvaluatedItems);
        AppendMissedItems(builder, evaluation.MissedItems);
        AppendCorrections(builder, evaluation.Corrections);
    }

    private static void AppendEvaluatedItems(StringBuilder builder, IReadOnlyList<ExpertEvaluatedItem> items)
    {
        AppendHeading(builder, 3, "Expert marks");

        if (items.Count == 0)
        {
            builder.AppendLine("No expert marks were saved.");
            builder.AppendLine();
            return;
        }

        foreach (var item in items.OrderBy(item => item.TargetId))
        {
            builder.AppendLine($"- **{EmptyIfBlank(item.TargetId)}** ({item.TargetType})");
            AppendNestedBullet(builder, "Mark", item.Mark.ToString());
            AppendNestedBullet(builder, "Comment", item.Comment);
            AppendNestedBullet(builder, "Correction", item.CorrectionText);
        }

        builder.AppendLine();
    }

    private static void AppendMissedItems(StringBuilder builder, IReadOnlyList<ExpertMissedItem> items)
    {
        AppendHeading(builder, 3, "Missed items");

        if (items.Count == 0)
        {
            builder.AppendLine("No missed items were saved.");
            builder.AppendLine();
            return;
        }

        foreach (var item in items.OrderBy(item => item.Title))
        {
            builder.AppendLine($"- **{EmptyIfBlank(item.Title)}** ({item.ItemType}, severity: {item.Severity})");
            AppendNestedBullet(builder, "Description", item.Description);
            AppendNestedBullet(builder, "Comment", item.Comment);
        }

        builder.AppendLine();
    }

    private static void AppendCorrections(StringBuilder builder, IReadOnlyList<ExpertCorrection> corrections)
    {
        AppendHeading(builder, 3, "Expert corrections");

        if (corrections.Count == 0)
        {
            builder.AppendLine("No expert corrections were saved.");
            builder.AppendLine();
            return;
        }

        foreach (var correction in corrections.OrderBy(correction => correction.TargetId))
        {
            builder.AppendLine($"- **{EmptyIfBlank(correction.TargetId)}** ({correction.TargetType}, {correction.ItemType})");
            AppendNestedBullet(builder, "Correction", correction.Text);
            AppendNestedBullet(builder, "Comment", correction.Comment);
        }

        builder.AppendLine();
    }

    private static void AppendExpertConclusion(StringBuilder builder, DomainAnalysis analysis)
    {
        AppendHeading(builder, 2, "Expert conclusion");

        var conclusion = analysis.ExpertConclusion;
        if (conclusion is null)
        {
            builder.AppendLine("No expert conclusion was saved.");
            builder.AppendLine();
            return;
        }

        AppendBullet(builder, "Conclusion", conclusion.ConclusionType.ToString());
        AppendField(builder, "Comment", conclusion.Comment);
        AppendField(builder, "Rationale", conclusion.Rationale);
        AppendBullet(builder, "Fixed at", conclusion.FixedAt is null ? "Not fixed" : FormatDate(conclusion.FixedAt.Value));
        AppendBullet(builder, "Analysis status", analysis.Status.ToString());
        builder.AppendLine();
    }

    private static void AppendBoundaryNotice(StringBuilder builder)
    {
        AppendHeading(builder, 2, "Decision boundary");
        builder.AppendLine(AnalysisBoundaryNotice.Default.ResultUseStatement);
        builder.AppendLine("The LLM formed preliminary analytical material and does not make a management decision.");
        builder.AppendLine("The expert conclusion and management consideration remain the responsibility of a human expert.");
        builder.AppendLine();
    }

    private static void AppendHeading(StringBuilder builder, int level, string title)
    {
        builder.AppendLine($"{new string('#', level)} {EmptyIfBlank(title)}");
        builder.AppendLine();
    }

    private static void AppendBullet(StringBuilder builder, string label, string? value) =>
        builder.AppendLine($"- **{label}:** {EmptyIfBlank(value)}");

    private static void AppendNestedBullet(StringBuilder builder, string label, string? value) =>
        builder.AppendLine($"  - **{label}:** {EmptyIfBlank(value)}");

    private static void AppendField(StringBuilder builder, string label, string value)
    {
        builder.AppendLine($"**{label}:**");
        builder.AppendLine();
        builder.AppendLine(EmptyIfBlank(value));
        builder.AppendLine();
    }

    private static void AppendFencedBlock(StringBuilder builder, string label, string value)
    {
        var fence = CreateMarkdownFence(value);

        builder.AppendLine($"**{label}:**");
        builder.AppendLine();
        builder.AppendLine($"{fence}text");
        builder.AppendLine(value);
        builder.AppendLine(fence);
        builder.AppendLine();
    }

    private static void AppendOptionalFencedBlock(StringBuilder builder, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AppendBullet(builder, label, null);
            builder.AppendLine();
            return;
        }

        AppendFencedBlock(builder, label, value);
    }

    private static string FormatRetrievedContextLimitation(RetrievedContextState state) =>
        state switch
        {
            RetrievedContextState.Available => "Retrieved context was saved for this analysis result.",
            RetrievedContextState.MetadataOnly =>
                "Only retrieved context metadata was saved; full text or excerpts may be unavailable.",
            RetrievedContextState.Partial =>
                "Retrieved context was saved only partially; review item completeness and limitation notes.",
            _ => "Retrieved context is unavailable for this saved analysis result."
        };

    private static string CreateMarkdownFence(string value)
    {
        const int MinimumFenceLength = 3;
        var longestBacktickRun = 0;
        var currentBacktickRun = 0;

        foreach (var character in value)
        {
            if (character == '`')
            {
                currentBacktickRun++;
                longestBacktickRun = Math.Max(longestBacktickRun, currentBacktickRun);
            }
            else
            {
                currentBacktickRun = 0;
            }
        }

        return new string('`', Math.Max(MinimumFenceLength, longestBacktickRun + 1));
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

    private static string? FormatScore(double? value) =>
        value?.ToString("0.################", CultureInfo.InvariantCulture);

    private static string FormatRelatedContext(IReadOnlyCollection<Guid> relatedContextFragmentIds) =>
        relatedContextFragmentIds.Count == 0
            ? "None"
            : string.Join(", ", relatedContextFragmentIds.OrderBy(id => id));

    private static string EmptyIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Not provided" : value.Trim();

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

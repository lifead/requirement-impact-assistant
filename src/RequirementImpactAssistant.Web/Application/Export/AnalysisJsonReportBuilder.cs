using System.Text.Json;
using System.Text.Json.Serialization;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;
using DomainAnalysis = RequirementImpactAssistant.Web.Domain.Analysis;

namespace RequirementImpactAssistant.Web.Application.Export;

public sealed class AnalysisJsonReportBuilder
{
    private const string FormatName = "requirement-impact-assistant.analysis-export";
    private const string FormatVersion = "1.0";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Build(DomainAnalysis analysis, DateTimeOffset exportedAt)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var document = new
        {
            metadata = new
            {
                analysis.Id,
                analysis.Title,
                Status = analysis.Status.ToString(),
                analysis.CreatedAt,
                analysis.UpdatedAt,
                analysis.FixedAt
            },
            input = new
            {
                OriginalDescription = analysis.OriginalDescription,
                OriginalRequirement = analysis.OriginalDescription,
                ProjectRequest = analysis.ProjectRequest,
                ProposedChange = analysis.ProjectRequest,
                analysis.SituationDescription,
                analysis.ChangeSource
            },
            contextFragments = analysis.ContextFragments
                .OrderBy(fragment => fragment.CreatedAt)
                .ThenBy(fragment => fragment.Id)
                .Select(fragment => new
                {
                    fragment.Id,
                    Type = fragment.Type.ToString(),
                    fragment.Source,
                    fragment.Text,
                    fragment.FileName,
                    fragment.FilePath,
                    fragment.CreatedAt
                }),
            aiAnalysisResult = ToAiAnalysisResult(analysis.AiAnalysisResult),
            impactMap = ToImpactMap(analysis.AiAnalysisResult?.ImpactMap),
            expertEvaluation = ToExpertEvaluation(analysis.ExpertEvaluation),
            expertConclusion = ToExpertConclusion(analysis.ExpertConclusion),
            exportMetadata = new
            {
                ExportedAt = exportedAt,
                Format = FormatName,
                Version = FormatVersion,
                BoundaryNotice = new
                {
                    AnalysisBoundaryNotice.Default.IsPreliminaryAnalyticalMaterial,
                    AnalysisBoundaryNotice.Default.AiDoesNotMakeManagementDecision,
                    AnalysisBoundaryNotice.Default.HumanDecisionAuthority,
                    AnalysisBoundaryNotice.Default.ResultUseStatement,
                    Statement = "The LLM formed preliminary analytical material and does not make a management decision."
                }
            }
        };

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    private static object? ToAiAnalysisResult(AiAnalysisResult? result)
    {
        if (result is null)
        {
            return null;
        }

        var metadata = result.Metadata;

        return new
        {
            Status = result.Status.ToString(),
            result.GeneratedAt,
            result.EngineName,
            result.ProviderName,
            result.ModelName,
            result.PromptVersion,
            result.InputSnapshot,
            AnalysisMode = metadata.AnalysisMode.ToString(),
            AnalysisEngine = new
            {
                Name = EmptyToNull(FirstNonBlank(metadata.EngineName, result.EngineName))
            },
            Provider = new
            {
                Name = EmptyToNull(FirstNonBlank(metadata.ProviderName, result.ProviderName))
            },
            Adapter = new
            {
                Name = EmptyToNull(metadata.AdapterName)
            },
            ModelWorkflowProfile = new
            {
                Name = EmptyToNull(FirstNonBlank(metadata.ModelWorkflowProfileName, result.ModelName))
            },
            ManualContextUsage = new
            {
                ForwardedToExternalAiOrRag = metadata.ManualContextForwardedToExternalAiOrRag
            },
            RetrievedContextState = metadata.RetrievedContextState.ToString(),
            RetrievedContextLimitations = ToRetrievedContextLimitations(metadata.RetrievedContextState),
            Warnings = ToWarnings(metadata.Warnings),
            RawResponse = EmptyToNull(result.RawResponse),
            ErrorMessage = EmptyToNull(result.ErrorMessage)
        };
    }

    private static IReadOnlyList<string> ToRetrievedContextLimitations(RetrievedContextState state) =>
        state switch
        {
            RetrievedContextState.Unavailable =>
            [
                "Retrieved context is unavailable for this saved analysis result."
            ],
            RetrievedContextState.MetadataOnly =>
            [
                "Only retrieved context metadata was saved for this analysis result."
            ],
            RetrievedContextState.Partial =>
            [
                "Retrieved context was saved only partially for this analysis result."
            ],
            _ => []
        };

    private static IReadOnlyList<string> ToWarnings(IEnumerable<string> warnings) =>
        warnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Select(warning => warning.Trim())
            .ToList();

    private static object? ToImpactMap(ImpactMap? impactMap)
    {
        if (impactMap is null)
        {
            return null;
        }

        return new
        {
            ChangeSummary = ToImpactItem(impactMap.ChangeSummary),
            AffectedRequirements = ToImpactItems(impactMap.AffectedRequirements),
            AffectedTasks = ToImpactItems(impactMap.AffectedTasks),
            AffectedProjectDecisions = ToImpactItems(impactMap.AffectedProjectDecisions),
            AffectedApiInterfacesDocumentsTests = ToImpactItems(impactMap.AffectedApiInterfacesDocumentsTests),
            AffectedArchitecturalConstraints = ToImpactItems(impactMap.AffectedArchitecturalConstraints),
            AffectedOrganizationalContextItems = ToImpactItems(impactMap.AffectedOrganizationalContextItems),
            Risks = ToImpactItems(impactMap.Risks),
            Contradictions = ToImpactItems(impactMap.Contradictions),
            ClarificationQuestions = ToImpactItems(impactMap.ClarificationQuestions),
            MissingInformation = ToImpactItems(impactMap.MissingInformation),
            OptionsForExpertReview = ToImpactItems(impactMap.OptionsForExpertReview),
            PreliminaryAssessment = ToImpactItem(impactMap.PreliminaryAssessment)
        };
    }

    private static IReadOnlyList<object> ToImpactItems(IReadOnlyList<ImpactMapItem> items) =>
        items
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(ToImpactItem)
            .ToList();

    private static object ToImpactItem(ImpactMapItem item) =>
        new
        {
            item.Id,
            ItemType = item.ItemType.ToString(),
            item.Title,
            item.Description,
            Severity = item.Severity.ToString(),
            RelatedContextFragmentIds = item.RelatedContextFragmentIds.OrderBy(id => id),
            item.Notes
        };

    private static object? ToExpertEvaluation(ExpertEvaluation? evaluation)
    {
        if (evaluation is null)
        {
            return null;
        }

        return new
        {
            ContextSufficiency = evaluation.ContextSufficiency.ToString(),
            ResultUsefulness = evaluation.ResultUsefulness.ToString(),
            evaluation.GeneralComment,
            ExpertMarks = evaluation.EvaluatedItems
                .OrderBy(item => item.TargetId)
                .ThenBy(item => item.Id)
                .Select(item => new
                {
                    item.Id,
                    TargetType = item.TargetType.ToString(),
                    item.TargetId,
                    Mark = item.Mark.ToString(),
                    item.Comment,
                    item.CorrectionText
                }),
            MissedItems = evaluation.MissedItems
                .OrderBy(item => item.Title)
                .ThenBy(item => item.Id)
                .Select(item => new
                {
                    item.Id,
                    ItemType = item.ItemType.ToString(),
                    item.Title,
                    item.Description,
                    Severity = item.Severity.ToString(),
                    item.Comment
                }),
            Corrections = evaluation.Corrections
                .OrderBy(correction => correction.TargetId)
                .ThenBy(correction => correction.Id)
                .Select(correction => new
                {
                    correction.Id,
                    TargetType = correction.TargetType.ToString(),
                    correction.TargetId,
                    ItemType = correction.ItemType.ToString(),
                    correction.Text,
                    correction.Comment
                })
        };
    }

    private static object? ToExpertConclusion(ExpertConclusion? conclusion)
    {
        if (conclusion is null)
        {
            return null;
        }

        return new
        {
            ConclusionType = conclusion.ConclusionType.ToString(),
            conclusion.Comment,
            conclusion.Rationale,
            conclusion.FixedAt
        };
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

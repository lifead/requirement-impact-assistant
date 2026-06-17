using System.Text.Json;
using DomainAnalysis = RequirementImpactAssistant.Web.Domain.Analysis;

namespace RequirementImpactAssistant.Web.Application.Analysis;

public sealed class AnalysisInputAssembler : IAnalysisInputAssembler
{
    private static readonly JsonSerializerOptions SnapshotJsonSerializerOptions =
        new(JsonSerializerDefaults.Web);

    public AiAnalysisRequest Assemble(DomainAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var snapshot = new AnalysisInputSnapshot(
            AnalysisId: analysis.Id,
            Analysis: new AnalysisInputFields(
                Title: analysis.Title,
                ProjectRequestType: analysis.ProjectRequestType.ToString(),
                OriginalDescription: analysis.OriginalDescription,
                ProjectRequest: analysis.ProjectRequest,
                SituationDescription: analysis.SituationDescription,
                ChangeSource: analysis.ChangeSource),
            ContextFragments: analysis.ContextFragments
                .Where(fragment => fragment.AnalysisId == analysis.Id)
                .OrderBy(fragment => fragment.CreatedAt)
                .ThenBy(fragment => fragment.Id)
                .Select(fragment => new AnalysisContextFragmentSnapshot(
                    Id: fragment.Id,
                    Type: fragment.Type.ToString(),
                    Source: fragment.Source,
                    Text: fragment.Text,
                    FileName: fragment.FileName))
                .ToList());

        return new AiAnalysisRequest(
            InputSnapshot: snapshot,
            InputSnapshotJson: JsonSerializer.Serialize(snapshot, SnapshotJsonSerializerOptions),
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default);
    }
}

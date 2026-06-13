using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Domain;

public static class AnalysisStatusCalculator
{
    public static AnalysisStatus Calculate(Analysis analysis)
    {
        var filledFields = CountFilledFields(
            analysis.Title,
            analysis.OriginalDescription,
            analysis.ProjectRequest,
            analysis.SituationDescription,
            analysis.ChangeSource);

        return filledFields switch
        {
            0 => AnalysisStatus.Draft,
            5 => AnalysisStatus.ReadyForAnalysis,
            _ => AnalysisStatus.InputIncomplete
        };
    }

    private static int CountFilledFields(params string[] values) =>
        values.Count(value => !string.IsNullOrWhiteSpace(value));
}

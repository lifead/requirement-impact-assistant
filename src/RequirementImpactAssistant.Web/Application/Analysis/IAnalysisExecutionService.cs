using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Application.Analysis;

public interface IAnalysisExecutionService
{
    Task<AnalysisExecutionOutcome> RunAsync(
        Guid analysisId,
        CancellationToken cancellationToken = default);

    Task<AnalysisExecutionOutcome> RunAsync(
        Guid analysisId,
        AnalysisMode analysisMode,
        CancellationToken cancellationToken = default);
}

public sealed record AnalysisExecutionOutcome(
    AnalysisExecutionOutcomeKind Kind,
    Guid AnalysisId,
    AiAnalysisResultStatus? ResultStatus,
    string Message)
{
    public bool Succeeded => Kind == AnalysisExecutionOutcomeKind.Completed;
}

public enum AnalysisExecutionOutcomeKind
{
    Completed = 1,
    NotFound = 2,
    InvalidInput = 3,
    SnapshotLocked = 4
}

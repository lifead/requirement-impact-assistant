namespace RequirementImpactAssistant.Web.Application.Analysis.External;

public sealed record ExternalRagAdapterRequest(
    Guid CorrelationId,
    AnalysisInputSnapshot InputSnapshot,
    ExternalRagManualContextBlock? ManualContext,
    bool CanForwardManualContextToExternalAiOrRag,
    ExpectedAnalysisResultStructure ExpectedResult,
    AnalysisBoundaryNotice BoundaryNotice,
    ExternalRagRequestMetadata ExecutionMetadata);

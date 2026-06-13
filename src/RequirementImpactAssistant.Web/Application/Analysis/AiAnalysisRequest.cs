namespace RequirementImpactAssistant.Web.Application.Analysis;

public sealed record AiAnalysisRequest(
    AnalysisInputSnapshot InputSnapshot,
    string InputSnapshotJson,
    ExpectedAnalysisResultStructure ExpectedResult,
    AnalysisBoundaryNotice BoundaryNotice);

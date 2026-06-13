namespace RequirementImpactAssistant.Web.Application.Export;

public sealed record AnalysisJsonExportResult(
    AnalysisJsonExportResultKind Kind,
    string FileName,
    string Json,
    string Message)
{
    public static AnalysisJsonExportResult NotFound() =>
        new(AnalysisJsonExportResultKind.NotFound, string.Empty, string.Empty, "Analysis was not found.");

    public static AnalysisJsonExportResult Unavailable() =>
        new(
            AnalysisJsonExportResultKind.Unavailable,
            string.Empty,
            string.Empty,
            "JSON export is available only after expert conclusion is saved.");

    public static AnalysisJsonExportResult Exported(string fileName, string json) =>
        new(AnalysisJsonExportResultKind.Exported, fileName, json, string.Empty);
}

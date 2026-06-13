namespace RequirementImpactAssistant.Web.Application.Export;

public sealed record AnalysisMarkdownExportResult(
    AnalysisMarkdownExportResultKind Kind,
    string FileName,
    string Markdown,
    string Message)
{
    public static AnalysisMarkdownExportResult NotFound() =>
        new(AnalysisMarkdownExportResultKind.NotFound, string.Empty, string.Empty, "Analysis was not found.");

    public static AnalysisMarkdownExportResult Unavailable() =>
        new(
            AnalysisMarkdownExportResultKind.Unavailable,
            string.Empty,
            string.Empty,
            "Markdown export is available only after expert conclusion is saved.");

    public static AnalysisMarkdownExportResult Exported(string fileName, string markdown) =>
        new(AnalysisMarkdownExportResultKind.Exported, fileName, markdown, string.Empty);
}

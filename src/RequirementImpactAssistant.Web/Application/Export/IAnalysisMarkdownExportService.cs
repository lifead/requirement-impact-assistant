namespace RequirementImpactAssistant.Web.Application.Export;

public interface IAnalysisMarkdownExportService
{
    Task<AnalysisMarkdownExportResult> ExportAsync(Guid analysisId, DateTimeOffset exportedAt);
}

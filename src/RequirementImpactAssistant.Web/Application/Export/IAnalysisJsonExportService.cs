namespace RequirementImpactAssistant.Web.Application.Export;

public interface IAnalysisJsonExportService
{
    Task<AnalysisJsonExportResult> ExportAsync(Guid analysisId, DateTimeOffset exportedAt);
}

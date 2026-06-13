using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using DomainAnalysis = RequirementImpactAssistant.Web.Domain.Analysis;

namespace RequirementImpactAssistant.Web.Application.Export;

public sealed class AnalysisJsonExportService(ApplicationDbContext dbContext) : IAnalysisJsonExportService
{
    public const string ContentType = "application/json; charset=utf-8";

    private readonly AnalysisJsonReportBuilder reportBuilder = new();

    public async Task<AnalysisJsonExportResult> ExportAsync(Guid analysisId, DateTimeOffset exportedAt)
    {
        var analysis = await LoadAnalysisAsync(analysisId);

        if (analysis is null)
        {
            return AnalysisJsonExportResult.NotFound();
        }

        if (analysis.ExpertConclusion is null)
        {
            return AnalysisJsonExportResult.Unavailable();
        }

        var json = reportBuilder.Build(analysis, exportedAt);

        return AnalysisJsonExportResult.Exported(CreateFileName(analysis), json);
    }

    private async Task<DomainAnalysis?> LoadAnalysisAsync(Guid analysisId) =>
        await dbContext.Analyses
            .AsNoTracking()
            .Include(analysis => analysis.ContextFragments)
            .Include(analysis => analysis.AiAnalysisResult)
            .Include(analysis => analysis.ExpertEvaluation)
                .ThenInclude(evaluation => evaluation!.EvaluatedItems)
            .Include(analysis => analysis.ExpertEvaluation)
                .ThenInclude(evaluation => evaluation!.MissedItems)
            .Include(analysis => analysis.ExpertEvaluation)
                .ThenInclude(evaluation => evaluation!.Corrections)
            .Include(analysis => analysis.ExpertConclusion)
            .SingleOrDefaultAsync(analysis => analysis.Id == analysisId);

    private static string CreateFileName(DomainAnalysis analysis)
    {
        var normalizedTitle = new string(analysis.Title
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray());
        var collapsedTitle = string.Join(
            '-',
            normalizedTitle.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var safeTitle = string.IsNullOrWhiteSpace(collapsedTitle)
            ? "analysis"
            : collapsedTitle;

        return $"{safeTitle}-{analysis.Id:N}.json";
    }
}

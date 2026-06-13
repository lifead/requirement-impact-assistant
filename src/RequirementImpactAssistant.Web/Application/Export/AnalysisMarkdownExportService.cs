using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using DomainAnalysis = RequirementImpactAssistant.Web.Domain.Analysis;

namespace RequirementImpactAssistant.Web.Application.Export;

public sealed class AnalysisMarkdownExportService(ApplicationDbContext dbContext) : IAnalysisMarkdownExportService
{
    public const string ContentType = "text/markdown; charset=utf-8";

    private readonly AnalysisMarkdownReportBuilder reportBuilder = new();

    public async Task<AnalysisMarkdownExportResult> ExportAsync(Guid analysisId, DateTimeOffset exportedAt)
    {
        var analysis = await LoadAnalysisAsync(analysisId);

        if (analysis is null)
        {
            return AnalysisMarkdownExportResult.NotFound();
        }

        if (analysis.ExpertConclusion is null)
        {
            return AnalysisMarkdownExportResult.Unavailable();
        }

        var markdown = reportBuilder.Build(analysis, exportedAt);

        return AnalysisMarkdownExportResult.Exported(CreateFileName(analysis), markdown);
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

        return $"{safeTitle}-{analysis.Id:N}.md";
    }
}

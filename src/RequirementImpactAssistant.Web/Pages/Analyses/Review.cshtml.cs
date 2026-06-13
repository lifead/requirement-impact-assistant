using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class ReviewModel(ApplicationDbContext dbContext) : PageModel
{
    public AnalysisReview? Analysis { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var analysis = await dbContext.Analyses
            .AsNoTracking()
            .Include(candidate => candidate.ContextFragments)
            .SingleOrDefaultAsync(candidate => candidate.Id == id);

        Analysis = analysis is null
            ? null
            : ToReview(analysis);

        return Analysis is null
            ? NotFound()
            : Page();
    }

    private static AnalysisReview ToReview(Analysis analysis) =>
        new(
            analysis.Id,
            analysis.Title,
            analysis.Status,
            analysis.OriginalDescription,
            analysis.ProjectRequest,
            analysis.SituationDescription,
            analysis.ChangeSource,
            analysis.CreatedAt,
            analysis.UpdatedAt,
            analysis.FixedAt,
            analysis.ContextFragments
                .OrderByDescending(fragment => fragment.CreatedAt)
                .Select(fragment => new ContextFragmentReview(
                    fragment.Id,
                    fragment.Type,
                    fragment.Source,
                    fragment.Text,
                    fragment.FileName,
                    fragment.CreatedAt))
                .ToList());

    public sealed record AnalysisReview(
        Guid Id,
        string Title,
        AnalysisStatus Status,
        string OriginalDescription,
        string ProjectRequest,
        string SituationDescription,
        string ChangeSource,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? FixedAt,
        IReadOnlyList<ContextFragmentReview> ContextFragments)
    {
        public bool HasMinimumInput =>
            !string.IsNullOrWhiteSpace(Title) &&
            !string.IsNullOrWhiteSpace(OriginalDescription) &&
            !string.IsNullOrWhiteSpace(ProjectRequest) &&
            !string.IsNullOrWhiteSpace(SituationDescription) &&
            !string.IsNullOrWhiteSpace(ChangeSource);
    }

    public sealed record ContextFragmentReview(
        Guid Id,
        ContextFragmentType Type,
        string Source,
        string Text,
        string? FileName,
        DateTimeOffset CreatedAt);
}

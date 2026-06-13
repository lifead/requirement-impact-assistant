using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class DetailsModel(ApplicationDbContext dbContext) : PageModel
{
    public AnalysisDetails? Analysis { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Analysis = await dbContext.Analyses
            .AsNoTracking()
            .Where(analysis => analysis.Id == id)
            .Select(analysis => new AnalysisDetails(
                analysis.Id,
                analysis.Title,
                analysis.Status,
                analysis.OriginalDescription,
                analysis.ProjectRequest,
                analysis.SituationDescription,
                analysis.ChangeSource,
                analysis.CreatedAt,
                analysis.UpdatedAt,
                analysis.FixedAt))
            .SingleOrDefaultAsync();

        return Analysis is null
            ? NotFound()
            : Page();
    }

    public sealed record AnalysisDetails(
        Guid Id,
        string Title,
        AnalysisStatus Status,
        string OriginalDescription,
        string ProjectRequest,
        string SituationDescription,
        string ChangeSource,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? FixedAt);
}

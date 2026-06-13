using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    public IReadOnlyList<AnalysisListItem> Analyses { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var analyses = await dbContext.Analyses
            .AsNoTracking()
            .Select(analysis => new AnalysisListItem(
                analysis.Id,
                analysis.Title,
                analysis.Status,
                analysis.UpdatedAt))
            .ToListAsync();

        Analyses = analyses
            .OrderByDescending(analysis => analysis.UpdatedAt)
            .ToList();
    }

    public sealed record AnalysisListItem(
        Guid Id,
        string Title,
        AnalysisStatus Status,
        DateTimeOffset UpdatedAt);
}

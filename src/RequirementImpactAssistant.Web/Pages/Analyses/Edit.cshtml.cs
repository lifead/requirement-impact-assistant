using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class EditModel(ApplicationDbContext dbContext) : PageModel
{
    [BindProperty]
    public AnalysisFormInput Input { get; set; } = new();

    public Guid AnalysisId { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        AnalysisId = id;

        var analysis = await dbContext.Analyses
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id);

        if (analysis is null)
        {
            return NotFound();
        }

        Input = AnalysisFormInput.FromAnalysis(analysis);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        AnalysisId = id;

        if (!Input.Validate(ModelState))
        {
            return Page();
        }

        var analysis = await dbContext.Analyses
            .SingleOrDefaultAsync(candidate => candidate.Id == id);

        if (analysis is null)
        {
            return NotFound();
        }

        Input.ApplyTo(analysis);
        analysis.Status = AnalysisStatusCalculator.Calculate(analysis);
        analysis.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync();

        return RedirectToPage("/Analyses/Details", new { id = analysis.Id });
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class CreateModel(ApplicationDbContext dbContext) : PageModel
{
    [BindProperty]
    public AnalysisFormInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Input.Validate(ModelState))
        {
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        var analysis = new Analysis
        {
            CreatedAt = now,
            UpdatedAt = now
        };
        Input.ApplyTo(analysis);
        analysis.Status = AnalysisStatusCalculator.Calculate(analysis);

        dbContext.Analyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        return RedirectToPage("/Analyses/Details", new { id = analysis.Id });
    }
}

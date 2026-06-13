using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class DetailsModel(ApplicationDbContext dbContext) : PageModel
{
    [BindProperty]
    public ManualContextFragmentInput ContextFragmentInput { get; set; } = new();

    public AnalysisDetails? Analysis { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Analysis = await LoadAnalysisDetailsAsync(id);

        return Analysis is null
            ? NotFound()
            : Page();
    }

    public async Task<IActionResult> OnPostAddContextFragmentAsync(Guid id)
    {
        var analysis = await dbContext.Analyses
            .SingleOrDefaultAsync(candidate => candidate.Id == id);

        if (analysis is null)
        {
            return NotFound();
        }

        if (!ContextFragmentInput.Validate(ModelState))
        {
            Analysis = await LoadAnalysisDetailsAsync(id);
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        dbContext.ContextFragments.Add(ContextFragmentInput.ToContextFragment(analysis.Id, now));
        analysis.UpdatedAt = now;

        await dbContext.SaveChangesAsync();

        return RedirectToPage("/Analyses/Details", new { id = analysis.Id });
    }

    public async Task<IActionResult> OnPostDeleteContextFragmentAsync(Guid id, Guid fragmentId)
    {
        var fragment = await dbContext.ContextFragments
            .SingleOrDefaultAsync(candidate => candidate.Id == fragmentId && candidate.AnalysisId == id);

        if (fragment is null)
        {
            return NotFound();
        }

        var analysis = await dbContext.Analyses
            .SingleOrDefaultAsync(candidate => candidate.Id == id);

        if (analysis is null)
        {
            return NotFound();
        }

        dbContext.ContextFragments.Remove(fragment);
        analysis.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync();

        return RedirectToPage("/Analyses/Details", new { id = analysis.Id });
    }

    private static AnalysisDetails ToDetails(Analysis analysis) =>
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
                .Select(fragment => new ContextFragmentDetails(
                    fragment.Id,
                    fragment.Type,
                    fragment.Source,
                    fragment.Text,
                    fragment.CreatedAt))
                .ToList());

    private async Task<AnalysisDetails?> LoadAnalysisDetailsAsync(Guid id)
    {
        var analysis = await dbContext.Analyses
            .AsNoTracking()
            .Include(candidate => candidate.ContextFragments)
            .SingleOrDefaultAsync(candidate => candidate.Id == id);

        return analysis is null
            ? null
            : ToDetails(analysis);
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
        DateTimeOffset? FixedAt,
        IReadOnlyList<ContextFragmentDetails> ContextFragments);

    public sealed record ContextFragmentDetails(
        Guid Id,
        ContextFragmentType Type,
        string Source,
        string Text,
        DateTimeOffset CreatedAt);

    public sealed class ManualContextFragmentInput
    {
        public ContextFragmentType Type { get; set; } = ContextFragmentType.Other;

        [Required(ErrorMessage = "Source is required.")]
        public string Source { get; set; } = string.Empty;

        [Required(ErrorMessage = "Text is required.")]
        public string Text { get; set; } = string.Empty;

        public bool Validate(ModelStateDictionary modelState)
        {
            AddRequiredError(modelState, nameof(Source), Source, "Source is required.");
            AddRequiredError(modelState, nameof(Text), Text, "Text is required.");

            return modelState.IsValid;
        }

        public ContextFragment ToContextFragment(Guid analysisId, DateTimeOffset createdAt) =>
            new()
            {
                AnalysisId = analysisId,
                Type = Type,
                Source = Source.Trim(),
                Text = Text.Trim(),
                CreatedAt = createdAt
            };

        private static void AddRequiredError(
            ModelStateDictionary modelState,
            string propertyName,
            string value,
            string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                modelState.AddModelError($"{nameof(DetailsModel.ContextFragmentInput)}.{propertyName}", errorMessage);
            }
        }
    }
}

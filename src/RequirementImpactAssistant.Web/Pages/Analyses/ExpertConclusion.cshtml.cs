using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class ExpertConclusionModel(ApplicationDbContext dbContext) : PageModel
{
    [BindProperty]
    public ExpertConclusionInput Input { get; set; } = new();

    public AnalysisConclusionDetails? Analysis { get; private set; }

    [TempData]
    public string? ExpertConclusionMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var analysis = await LoadAnalysisForConclusionAsync(id, asTracking: false);
        if (!CanFixConclusion(analysis))
        {
            return NotFound();
        }

        Analysis = ToDetails(analysis!);
        Input = ToInput(analysis!);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var analysis = await LoadAnalysisForConclusionAsync(id, asTracking: true);
        if (!CanFixConclusion(analysis))
        {
            return NotFound();
        }

        ValidateInput(ModelState);
        if (!ModelState.IsValid)
        {
            Analysis = ToDetails(analysis!);
            return Page();
        }

        var conclusion = analysis!.ExpertConclusion;
        if (conclusion is null)
        {
            conclusion = new ExpertConclusion
            {
                AnalysisId = analysis.Id
            };
            analysis.ExpertConclusion = conclusion;
            dbContext.ExpertConclusions.Add(conclusion);
        }

        var now = DateTimeOffset.UtcNow;
        conclusion.ConclusionType = Input.ConclusionType;
        conclusion.Comment = Normalize(Input.Comment);
        conclusion.Rationale = Normalize(Input.Rationale);
        conclusion.FixedAt = now;
        analysis.FixedAt = now;
        analysis.UpdatedAt = now;
        analysis.Status = AnalysisStatus.ExpertConclusionFixed;

        await dbContext.SaveChangesAsync();

        ExpertConclusionMessage = "Экспертное заключение сохранено.";

        return RedirectToPage("/Analyses/ExpertConclusion", new { id = analysis.Id });
    }

    private async Task<Analysis?> LoadAnalysisForConclusionAsync(Guid id, bool asTracking)
    {
        IQueryable<Analysis> query = dbContext.Analyses
            .Include(candidate => candidate.ExpertEvaluation)
            .Include(candidate => candidate.ExpertConclusion);

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(candidate => candidate.Id == id);
    }

    private static bool CanFixConclusion(Analysis? analysis) =>
        analysis?.ExpertEvaluation is not null;

    private static AnalysisConclusionDetails ToDetails(Analysis analysis) =>
        new(
            analysis.Id,
            analysis.Title,
            analysis.Status,
            analysis.ExpertConclusion?.FixedAt);

    private static ExpertConclusionInput ToInput(Analysis analysis)
    {
        var conclusion = analysis.ExpertConclusion;

        return new ExpertConclusionInput
        {
            ConclusionType = conclusion?.ConclusionType ?? ExpertConclusionType.NotSet,
            Comment = conclusion?.Comment ?? string.Empty,
            Rationale = conclusion?.Rationale ?? string.Empty
        };
    }

    private void ValidateInput(ModelStateDictionary modelState)
    {
        if (Input.ConclusionType == ExpertConclusionType.NotSet)
        {
            modelState.AddModelError(
                $"{nameof(Input)}.{nameof(ExpertConclusionInput.ConclusionType)}",
                "Тип заключения обязателен.");
        }

        if (string.IsNullOrWhiteSpace(Input.Rationale))
        {
            modelState.AddModelError(
                $"{nameof(Input)}.{nameof(ExpertConclusionInput.Rationale)}",
                "Обоснование обязательно.");
        }
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

    public sealed record AnalysisConclusionDetails(
        Guid Id,
        string Title,
        AnalysisStatus Status,
        DateTimeOffset? FixedAt);

    public sealed class ExpertConclusionInput
    {
        public ExpertConclusionType ConclusionType { get; set; } = ExpertConclusionType.NotSet;

        public string Comment { get; set; } = string.Empty;

        [Required(ErrorMessage = "Обоснование обязательно.")]
        public string Rationale { get; set; } = string.Empty;
    }
}

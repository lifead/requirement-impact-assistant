using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using AnalysisModeEnum = RequirementImpactAssistant.Web.Domain.Enums.AnalysisMode;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class ReviewModel(
    ApplicationDbContext dbContext,
    IAnalysisExecutionService? analysisExecutionService = null) : PageModel
{
    public AnalysisReview? Analysis { get; private set; }

    [BindProperty]
    public RunAnalysisInput Input { get; set; } = new();

    [TempData]
    public string? AnalysisRunMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Analysis = await LoadReviewAsync(id);

        return Analysis is null
            ? NotFound()
            : Page();
    }

    public async Task<IActionResult> OnPostRunAnalysisAsync(Guid id)
    {
        var analysisMode = Input.GetAnalysisMode(ModelState);
        if (!ModelState.IsValid)
        {
            Analysis = await LoadReviewAsync(id);

            return Analysis is null
                ? NotFound()
                : Page();
        }

        if (analysisExecutionService is null)
        {
            throw new InvalidOperationException("Сервис выполнения анализа не настроен.");
        }

        var cancellationToken = PageContext?.HttpContext?.RequestAborted ?? CancellationToken.None;
        var outcome = await analysisExecutionService.RunAsync(id, analysisMode, cancellationToken);

        if (outcome.Kind == AnalysisExecutionOutcomeKind.NotFound)
        {
            return NotFound();
        }

        if (outcome.Kind == AnalysisExecutionOutcomeKind.InvalidInput)
        {
            ModelState.AddModelError(string.Empty, outcome.Message);
            Analysis = await LoadReviewAsync(id);

            return Analysis is null
                ? NotFound()
                : Page();
        }

        AnalysisRunMessage = outcome.Message;

        return RedirectToPage("/Analyses/Details", new { id = outcome.AnalysisId });
    }

    private async Task<AnalysisReview?> LoadReviewAsync(Guid id)
    {
        var analysis = await dbContext.Analyses
            .AsNoTracking()
            .Include(candidate => candidate.ContextFragments)
            .SingleOrDefaultAsync(candidate => candidate.Id == id);

        return analysis is null
            ? null
            : ToReview(analysis);
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

    public sealed class RunAnalysisInput
    {
        public string? AnalysisMode { get; set; }

        public AnalysisModeEnum GetAnalysisMode(ModelStateDictionary modelState)
        {
            if (string.IsNullOrWhiteSpace(AnalysisMode))
            {
                return AnalysisModeEnum.DirectLlm;
            }

            return AnalysisMode.Trim() switch
            {
                nameof(AnalysisModeEnum.DirectLlm) => AnalysisModeEnum.DirectLlm,
                nameof(AnalysisModeEnum.ExternalRag) => AnalysisModeEnum.ExternalRag,
                _ => AddInvalidAnalysisModeError(modelState)
            };
        }

        private static AnalysisModeEnum AddInvalidAnalysisModeError(ModelStateDictionary modelState)
        {
            modelState.AddModelError(
                $"{nameof(Input)}.{nameof(AnalysisMode)}",
                "Analysis mode is invalid.");

            return AnalysisModeEnum.DirectLlm;
        }
    }
}

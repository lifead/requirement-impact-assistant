using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;
using RequirementImpactAssistant.Web.Application.Export;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class DetailsModel(
    ApplicationDbContext dbContext,
    IWebHostEnvironment? webHostEnvironment = null,
    IAnalysisMarkdownExportService? markdownExportService = null,
    IAnalysisJsonExportService? jsonExportService = null) : PageModel
{
    [BindProperty]
    public ManualContextFragmentInput ContextFragmentInput { get; set; } = new();

    public AnalysisDetails? Analysis { get; private set; }

    [TempData]
    public string? AnalysisRunMessage { get; set; }

    public Action<string> DeleteStoredUploadFile { get; set; } = System.IO.File.Delete;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Analysis = await LoadAnalysisDetailsAsync(id);

        return Analysis is null
            ? NotFound()
            : Page();
    }

    public async Task<IActionResult> OnGetExportMarkdownAsync(Guid id)
    {
        var exportService = markdownExportService ?? new AnalysisMarkdownExportService(dbContext);
        var result = await exportService.ExportAsync(id, DateTimeOffset.UtcNow);

        return result.Kind switch
        {
            AnalysisMarkdownExportResultKind.NotFound => NotFound(),
            AnalysisMarkdownExportResultKind.Unavailable => BadRequest(result.Message),
            _ => File(
                Encoding.UTF8.GetBytes(result.Markdown),
                AnalysisMarkdownExportService.ContentType,
                result.FileName)
        };
    }

    public async Task<IActionResult> OnGetExportJsonAsync(Guid id)
    {
        var exportService = jsonExportService ?? new AnalysisJsonExportService(dbContext);
        var result = await exportService.ExportAsync(id, DateTimeOffset.UtcNow);

        return result.Kind switch
        {
            AnalysisJsonExportResultKind.NotFound => NotFound(),
            AnalysisJsonExportResultKind.Unavailable => BadRequest(result.Message),
            _ => File(
                Encoding.UTF8.GetBytes(result.Json),
                AnalysisJsonExportService.ContentType,
                result.FileName)
        };
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

        DeleteStoredFileIfPresent(fragment.FilePath, analysis.Id);

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
            analysis.ExpertEvaluation is not null,
            analysis.ExpertConclusion is null
                ? null
                : new ExpertConclusionDetails(
                    analysis.ExpertConclusion.ConclusionType,
                    analysis.ExpertConclusion.Comment,
                    analysis.ExpertConclusion.Rationale,
                    analysis.ExpertConclusion.FixedAt),
            analysis.AiAnalysisResult is null
                ? null
                : ToAiAnalysisResultDetails(analysis.AiAnalysisResult),
            analysis.ContextFragments
                .OrderByDescending(fragment => fragment.CreatedAt)
                .Select(fragment => new ContextFragmentDetails(
                    fragment.Id,
                    fragment.Type,
                    fragment.Source,
                    fragment.Text,
                    fragment.FileName,
                    fragment.FilePath,
                    fragment.CreatedAt))
                .ToList());

    private static AiAnalysisResultDetails ToAiAnalysisResultDetails(AiAnalysisResult result) =>
        new(
            result.Status,
            result.GeneratedAt,
            result.PromptVersion,
            result.InputSnapshot,
            result.RawResponse,
            result.ErrorMessage,
            result.ImpactMap,
            ToAiAnalysisResultMetadataDetails(result.Metadata));

    private static AiAnalysisResultMetadataDetails ToAiAnalysisResultMetadataDetails(
        AiAnalysisResultMetadata metadata) =>
        new(
            metadata.AnalysisMode,
            metadata.EngineName,
            metadata.ProviderName,
            metadata.AdapterName,
            metadata.ModelWorkflowProfileName,
            metadata.RetrievedContextState,
            metadata.ManualContextForwardedToExternalAiOrRag,
            metadata.Warnings
                .Where(warning => !string.IsNullOrWhiteSpace(warning))
                .Select(warning => warning.Trim())
                .ToList(),
            metadata.RetrievedContextItems
                .Select(item => new RetrievedContextItemDetails(
                    item.SourceTitle,
                    item.SourceId,
                    item.ExternalReference,
                    item.FragmentId,
                    item.Text,
                    item.Excerpt,
                    item.UrlOrReference,
                    item.Rank,
                    item.Score,
                    item.Completeness,
                    item.WarningOrLimitationNote))
                .ToList());

    private async Task<AnalysisDetails?> LoadAnalysisDetailsAsync(Guid id)
    {
        var analysis = await dbContext.Analyses
            .AsNoTracking()
            .Include(candidate => candidate.ContextFragments)
            .Include(candidate => candidate.AiAnalysisResult)
                .ThenInclude(result => result!.Metadata.RetrievedContextItems)
            .Include(candidate => candidate.ExpertEvaluation)
            .Include(candidate => candidate.ExpertConclusion)
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
        bool HasExpertEvaluation,
        ExpertConclusionDetails? ExpertConclusion,
        AiAnalysisResultDetails? AiAnalysisResult,
        IReadOnlyList<ContextFragmentDetails> ContextFragments);

    public sealed record ExpertConclusionDetails(
        ExpertConclusionType ConclusionType,
        string Comment,
        string Rationale,
        DateTimeOffset? FixedAt);

    public sealed record AiAnalysisResultDetails(
        AiAnalysisResultStatus Status,
        DateTimeOffset? GeneratedAt,
        string PromptVersion,
        string InputSnapshot,
        string RawResponse,
        string ErrorMessage,
        ImpactMap? ImpactMap,
        AiAnalysisResultMetadataDetails Metadata)
    {
        public IReadOnlyList<ImpactMapSectionDetails> ImpactSections =>
            ImpactMap is null
                ? []
                :
                [
                    new("Сводка изменения", [ImpactMap.ChangeSummary]),
                    new("Затронутые требования", ImpactMap.AffectedRequirements),
                    new("Затронутые задачи", ImpactMap.AffectedTasks),
                    new("Затронутые проектные решения", ImpactMap.AffectedProjectDecisions),
                    new("Затронутые API, интерфейсы, документы и тесты", ImpactMap.AffectedApiInterfacesDocumentsTests),
                    new("Затронутые архитектурные ограничения", ImpactMap.AffectedArchitecturalConstraints),
                    new("Затронутый организационный контекст", ImpactMap.AffectedOrganizationalContextItems),
                    new("Противоречия", ImpactMap.Contradictions),
                    new("Недостающая информация", ImpactMap.MissingInformation),
                    new("Вопросы для уточнения", ImpactMap.ClarificationQuestions),
                    new("Риски", ImpactMap.Risks),
                    new("Варианты для экспертного рассмотрения", ImpactMap.OptionsForExpertReview),
                    new("Предварительная оценка", [ImpactMap.PreliminaryAssessment])
                ];
    }

    public sealed record AiAnalysisResultMetadataDetails(
        AnalysisMode AnalysisMode,
        string EngineName,
        string? ProviderName,
        string? AdapterName,
        string? ModelWorkflowProfileName,
        RetrievedContextState RetrievedContextState,
        bool ManualContextForwardedToExternalAiOrRag,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<RetrievedContextItemDetails> RetrievedContextItems);

    public sealed record RetrievedContextItemDetails(
        string SourceTitle,
        string? SourceId,
        string? ExternalReference,
        string? FragmentId,
        string? Text,
        string? Excerpt,
        string? UrlOrReference,
        int? Rank,
        double? Score,
        RetrievedContextItemCompleteness Completeness,
        string? WarningOrLimitationNote);

    public sealed record ImpactMapSectionDetails(
        string Title,
        IReadOnlyList<ImpactMapItem> Items);

    public sealed record ContextFragmentDetails(
        Guid Id,
        ContextFragmentType Type,
        string Source,
        string Text,
        string? FileName,
        string? FilePath,
        DateTimeOffset CreatedAt);

    public sealed class ManualContextFragmentInput
    {
        public ContextFragmentType Type { get; set; } = ContextFragmentType.Other;

        [Required(ErrorMessage = "Источник обязателен.")]
        public string Source { get; set; } = string.Empty;

        [Required(ErrorMessage = "Текст обязателен.")]
        public string Text { get; set; } = string.Empty;

        public bool Validate(ModelStateDictionary modelState)
        {
            AddRequiredError(modelState, nameof(Source), Source, "Источник обязателен.");
            AddRequiredError(modelState, nameof(Text), Text, "Текст обязателен.");

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

    private string ToAbsoluteUploadPath(string relativePath) =>
        Path.GetFullPath(Path.Combine(GetContentRootPath(), relativePath));

    private string GetAnalysisUploadsRootPath(Guid analysisId) =>
        Path.GetFullPath(Path.Combine(GetContentRootPath(), "data", "uploads", analysisId.ToString()));

    private string GetContentRootPath() =>
        webHostEnvironment?.ContentRootPath ?? Directory.GetCurrentDirectory();

    private void DeleteStoredFileIfPresent(string? relativePath, Guid analysisId)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var uploadsRoot = EnsureTrailingDirectorySeparator(GetAnalysisUploadsRootPath(analysisId));
        var fullPath = ToAbsoluteUploadPath(relativePath);
        if (!fullPath.StartsWith(uploadsRoot, GetPathComparison()))
        {
            return;
        }

        if (System.IO.File.Exists(fullPath))
        {
            DeleteStoredUploadFile(fullPath);
        }
    }

    private static string EnsureTrailingDirectorySeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : $"{path}{Path.DirectorySeparatorChar}";

    private static StringComparison GetPathComparison() =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

}

using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
    private const long MaxUploadFileSizeBytes = 1_048_576;

    private static readonly HashSet<string> AllowedUploadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json",
        ".md",
        ".txt"
    };

    [BindProperty]
    public ManualContextFragmentInput ContextFragmentInput { get; set; } = new();

    [BindProperty]
    public FileContextFragmentInput UploadContextFragmentInput { get; set; } = new();

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

    public async Task<IActionResult> OnPostUploadContextFragmentAsync(Guid id)
    {
        var analysis = await dbContext.Analyses
            .SingleOrDefaultAsync(candidate => candidate.Id == id);

        if (analysis is null)
        {
            return NotFound();
        }

        ClearManualContextFragmentValidation();

        if (!UploadContextFragmentInput.Validate(ModelState))
        {
            Analysis = await LoadAnalysisDetailsAsync(id);
            return Page();
        }

        var uploadedFile = UploadContextFragmentInput.File!;
        var originalFileName = SanitizeOriginalFileName(uploadedFile.FileName);
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var relativePath = BuildUploadRelativePath(analysis.Id, storedFileName);
        var absolutePath = ToAbsoluteUploadPath(relativePath);
        var now = DateTimeOffset.UtcNow;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

            await using (var output = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write))
            {
                await uploadedFile.CopyToAsync(output);
            }

            var text = await System.IO.File.ReadAllTextAsync(absolutePath);

            dbContext.ContextFragments.Add(new ContextFragment
            {
                AnalysisId = analysis.Id,
                Type = UploadContextFragmentInput.Type,
                Source = UploadContextFragmentInput.GetSource(originalFileName),
                Text = text,
                FileName = originalFileName,
                FilePath = relativePath,
                CreatedAt = now
            });
            analysis.UpdatedAt = now;

            await dbContext.SaveChangesAsync();
        }
        catch
        {
            DeleteStoredFileBestEffort(relativePath, analysis.Id);
            throw;
        }

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
                : new AiAnalysisResultDetails(
                    analysis.AiAnalysisResult.Status,
                    analysis.AiAnalysisResult.GeneratedAt,
                    analysis.AiAnalysisResult.EngineName,
                    analysis.AiAnalysisResult.ProviderName,
                    analysis.AiAnalysisResult.ModelName,
                    analysis.AiAnalysisResult.PromptVersion,
                    analysis.AiAnalysisResult.InputSnapshot,
                    analysis.AiAnalysisResult.RawResponse,
                    analysis.AiAnalysisResult.ErrorMessage,
                    analysis.AiAnalysisResult.ImpactMap),
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

    private async Task<AnalysisDetails?> LoadAnalysisDetailsAsync(Guid id)
    {
        var analysis = await dbContext.Analyses
            .AsNoTracking()
            .Include(candidate => candidate.ContextFragments)
            .Include(candidate => candidate.AiAnalysisResult)
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
        string EngineName,
        string ProviderName,
        string ModelName,
        string PromptVersion,
        string InputSnapshot,
        string RawResponse,
        string ErrorMessage,
        ImpactMap? ImpactMap)
    {
        public IReadOnlyList<ImpactMapSectionDetails> ImpactSections =>
            ImpactMap is null
                ? []
                :
                [
                    new("Change summary", [ImpactMap.ChangeSummary]),
                    new("Affected requirements", ImpactMap.AffectedRequirements),
                    new("Affected tasks", ImpactMap.AffectedTasks),
                    new("Affected project decisions", ImpactMap.AffectedProjectDecisions),
                    new("Affected API/interfaces/documents/tests", ImpactMap.AffectedApiInterfacesDocumentsTests),
                    new("Affected architectural constraints", ImpactMap.AffectedArchitecturalConstraints),
                    new("Affected organizational context", ImpactMap.AffectedOrganizationalContextItems),
                    new("Contradictions", ImpactMap.Contradictions),
                    new("Missing information", ImpactMap.MissingInformation),
                    new("Clarification questions", ImpactMap.ClarificationQuestions),
                    new("Risks", ImpactMap.Risks),
                    new("Options for expert review", ImpactMap.OptionsForExpertReview),
                    new("Preliminary assessment", [ImpactMap.PreliminaryAssessment])
                ];
    }

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

    public sealed class FileContextFragmentInput
    {
        public ContextFragmentType Type { get; set; } = ContextFragmentType.Other;

        public string? Source { get; set; }

        public IFormFile? File { get; set; }

        public bool Validate(ModelStateDictionary modelState)
        {
            if (File is null || File.Length == 0)
            {
                modelState.AddModelError($"{nameof(DetailsModel.UploadContextFragmentInput)}.{nameof(File)}", "File is required.");
                return false;
            }

            if (File.Length > MaxUploadFileSizeBytes)
            {
                modelState.AddModelError(
                    $"{nameof(DetailsModel.UploadContextFragmentInput)}.{nameof(File)}",
                    "File size must be 1 MB or less.");
            }

            var extension = Path.GetExtension(SanitizeOriginalFileName(File.FileName));
            if (!AllowedUploadExtensions.Contains(extension))
            {
                modelState.AddModelError(
                    $"{nameof(DetailsModel.UploadContextFragmentInput)}.{nameof(File)}",
                    "Only Markdown, TXT, and JSON files are supported.");
            }

            return modelState.IsValid;
        }

        public string GetSource(string fileName) =>
            string.IsNullOrWhiteSpace(Source)
                ? fileName
                : Source.Trim();
    }

    private static string BuildUploadRelativePath(Guid analysisId, string storedFileName) =>
        $"data/uploads/{analysisId}/{storedFileName}";

    private static string SanitizeOriginalFileName(string fileName)
    {
        var normalized = fileName.Replace('\\', '/');
        var lastSegment = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        return Path.GetFileName(lastSegment ?? string.Empty);
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

    private void DeleteStoredFileBestEffort(string relativePath, Guid analysisId)
    {
        try
        {
            DeleteStoredFileIfPresent(relativePath, analysisId);
        }
        catch
        {
            // Preserve the original upload or database failure for the caller.
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

    private void ClearManualContextFragmentValidation()
    {
        ModelState.Remove($"{nameof(ContextFragmentInput)}.{nameof(ManualContextFragmentInput.Source)}");
        ModelState.Remove($"{nameof(ContextFragmentInput)}.{nameof(ManualContextFragmentInput.Text)}");
    }
}

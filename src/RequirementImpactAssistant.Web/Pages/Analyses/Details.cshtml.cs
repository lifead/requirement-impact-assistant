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
            analysis.ProjectRequestType,
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
        ProjectRequestType ProjectRequestType,
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
        IReadOnlyList<ContextFragmentDetails> ContextFragments)
    {
        public IReadOnlyList<DetailsStatusSummaryItem> StatusSummaryItems =>
            BuildStatusSummaryItems();

        private IReadOnlyList<DetailsStatusSummaryItem> BuildStatusSummaryItems()
        {
            var hasPreliminaryImpactMap = AiAnalysisResult?.ImpactMap is not null &&
                AiAnalysisResult.Status is AiAnalysisResultStatus.Completed or AiAnalysisResultStatus.CompletedWithWarnings;

            return
            [
                new(
                    "input",
                    "Входные данные",
                    AnalysisUiText.AnalysisStatusLabel(Status),
                    $"{AnalysisUiText.ProjectRequestTypeLabel(ProjectRequestType)}; источник: {ChangeSource}.",
                    "analysis-input",
                    "text-bg-secondary",
                    [new("Проверить ввод", "/Analyses/Review", null)]),
                new(
                    "manual-context",
                    "Manual context",
                    ContextFragments.Count == 0 ? "Не добавлен" : $"Добавлено: {ContextFragments.Count}",
                    ContextFragments.Count == 0
                        ? "Пользователь еще не ввел manual context в карточке анализа."
                        : "Manual context введен пользователем и сохранен отдельно от retrieved context.",
                    "manual-context",
                    ContextFragments.Count == 0 ? "text-bg-light text-dark" : "text-bg-info",
                    []),
                new(
                    "retrieved-context",
                    "Retrieved context",
                    GetRetrievedContextStatusLabel(),
                    GetRetrievedContextDescription(),
                    "retrieved-context",
                    GetRetrievedContextBadgeCssClass(),
                    []),
                new(
                    "preliminary-result",
                    "Предварительный результат",
                    AiAnalysisResult is null
                        ? "Не сформирован"
                        : AnalysisUiText.AiResultStatusLabel(AiAnalysisResult.Status),
                    AiAnalysisResult is null
                        ? "Сохраненного AI/RAG/LLM результата пока нет."
                        : "Сохраненный предварительный материал доступен для экспертного рассмотрения.",
                    "preliminary-result",
                    AiAnalysisResult is null ? "text-bg-light text-dark" : "text-bg-secondary",
                    []),
                new(
                    "grounds-limitations",
                    "Основания и ограничения",
                    GetGroundsStatusLabel(),
                    GetGroundsDescription(),
                    "grounds-limitations",
                    AiAnalysisResult?.Metadata.Warnings.Count > 0 ? "text-bg-warning" : "text-bg-secondary",
                    []),
                new(
                    "expert-evaluation",
                    "Экспертная оценка",
                    HasExpertEvaluation
                        ? "Зафиксирована"
                        : hasPreliminaryImpactMap ? "Доступна" : "Недоступна",
                    HasExpertEvaluation
                        ? "Экспертная оценка сохранена как человеческий слой проверки."
                        : hasPreliminaryImpactMap
                            ? "Можно открыть существующую страницу экспертной оценки."
                            : "Нужен сохраненный предварительный результат со структурированной картой влияния.",
                    "expert-evaluation",
                    HasExpertEvaluation ? "text-bg-success" : "text-bg-light text-dark",
                    hasPreliminaryImpactMap ? [new("Открыть оценку", "/Analyses/ExpertEvaluation", null)] : []),
                new(
                    "expert-conclusion",
                    "Экспертное заключение",
                    ExpertConclusion is null ? "Не зафиксировано" : "Зафиксировано",
                    ExpertConclusion is null
                        ? "Итоговое заключение эксперта-человека пока не сохранено."
                        : $"{AnalysisUiText.ExpertConclusionTypeLabel(ExpertConclusion.ConclusionType)}; зафиксировано человеком.",
                    "expert-conclusion",
                    ExpertConclusion is null ? "text-bg-light text-dark" : "text-bg-success",
                    HasExpertEvaluation ? [new("Открыть заключение", "/Analyses/ExpertConclusion", null)] : []),
                new(
                    "export",
                    "Экспорт",
                    ExpertConclusion is null ? "Недоступен" : "Доступен",
                    ExpertConclusion is null
                        ? "Markdown/JSON выгрузка доступна после сохраненного экспертного заключения."
                        : "Можно скачать сохраненный артефакт без повторного анализа.",
                    "export",
                    ExpertConclusion is null ? "text-bg-light text-dark" : "text-bg-secondary",
                    ExpertConclusion is null
                        ? []
                        :
                        [
                            new("Скачать JSON", null, "ExportJson"),
                            new("Скачать Markdown", null, "ExportMarkdown")
                        ])
            ];
        }

        private string GetGroundsStatusLabel()
        {
            if (AiAnalysisResult is null)
            {
                return "Нет результата";
            }

            if (AiAnalysisResult.Metadata.Warnings.Count > 0)
            {
                return $"Предупреждения: {AiAnalysisResult.Metadata.Warnings.Count}";
            }

            return AnalysisUiText.RetrievedContextStateLabel(AiAnalysisResult.Metadata.RetrievedContextState);
        }

        private string GetGroundsDescription()
        {
            if (AiAnalysisResult is null)
            {
                return "Метаданные, warnings и limitation notes появятся после сохраненного результата.";
            }

            return
                $"{AnalysisUiText.AnalysisModeLabel(AiAnalysisResult.Metadata.AnalysisMode)}; " +
                AnalysisUiText.RetrievedContextStateDescription(AiAnalysisResult.Metadata.RetrievedContextState);
        }

        private string GetRetrievedContextStatusLabel()
        {
            if (AiAnalysisResult is null)
            {
                return "Нет результата";
            }

            if (AiAnalysisResult.Metadata.IsDirectLlmWithoutRetrievedContext)
            {
                return "Не создается";
            }

            return AnalysisUiText.RetrievedContextStateLabel(AiAnalysisResult.Metadata.RetrievedContextState);
        }

        private string GetRetrievedContextDescription()
        {
            if (AiAnalysisResult is null)
            {
                return "Retrieved context появится только если внешний provider вернет его для конкретного анализа.";
            }

            if (AiAnalysisResult.Metadata.IsDirectLlmWithoutRetrievedContext)
            {
                return "Direct LLM не создает искусственный retrieved context.";
            }

            if (!AiAnalysisResult.Metadata.HasRetrievedContextItems)
            {
                return "Сохраненных фрагментов или metadata retrieved context для этого результата нет.";
            }

            return
                $"Сохраненные фрагменты/metadata, возвращенные внешним provider-ом: {AiAnalysisResult.Metadata.RetrievedContextItems.Count}.";
        }

        private string GetRetrievedContextBadgeCssClass()
        {
            if (AiAnalysisResult is null || AiAnalysisResult.Metadata.IsDirectLlmWithoutRetrievedContext)
            {
                return "text-bg-light text-dark";
            }

            if (AiAnalysisResult.Metadata.RetrievedContextState == RetrievedContextState.Unavailable)
            {
                return "text-bg-warning";
            }

            return AiAnalysisResult.Metadata.HasRetrievedContextItems
                ? "text-bg-secondary"
                : "text-bg-light text-dark";
        }
    }

    public sealed record DetailsStatusSummaryItem(
        string Key,
        string Title,
        string StatusLabel,
        string Description,
        string Anchor,
        string BadgeCssClass,
        IReadOnlyList<DetailsStatusSummaryAction> Actions);

    public sealed record DetailsStatusSummaryAction(
        string Label,
        string? PageName,
        string? HandlerName);

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
            ImpactSectionGroups
                .SelectMany(group => group.Sections)
                .ToList();

        public IReadOnlyList<ImpactMapSectionGroupDetails> ImpactSectionGroups =>
            ImpactMap is null
                ? []
                :
                [
                    new(
                        "impact-map-orientation",
                        "Ориентиры анализа",
                        "Сводные элементы помогают быстро понять предмет изменения и предварительную оценку.",
                        [
                            CreateImpactSection(
                                ImpactMapItemType.ChangeSummary,
                                "Сводка изменения",
                                "Краткое описание рассматриваемого изменения.",
                                "Сводка изменения не заполнена в сохраненной карте влияния.",
                                [ImpactMap.ChangeSummary]),
                            CreateImpactSection(
                                ImpactMapItemType.PreliminaryAssessment,
                                "Предварительная оценка",
                                "Предварительная оценка для дальнейшего экспертного рассмотрения.",
                                "Предварительная оценка не заполнена в сохраненной карте влияния.",
                                [ImpactMap.PreliminaryAssessment])
                        ]),
                    new(
                        "impact-map-affected-elements",
                        "Затронутые элементы",
                        "Требования, задачи, решения, API, ограничения и организационный контекст, которые стоит проверить.",
                        [
                            CreateImpactSection(
                                ImpactMapItemType.AffectedRequirement,
                                "Требования",
                                "Требования, которые могут потребовать изменения или проверки.",
                                "Затронутые требования не указаны в предварительной карте; это не подтверждает отсутствие влияния.",
                                ImpactMap.AffectedRequirements),
                            CreateImpactSection(
                                ImpactMapItemType.AffectedTask,
                                "Задачи",
                                "Задачи и работы, которые могут измениться из-за запроса.",
                                "Затронутые задачи не указаны в предварительной карте; это не подтверждает отсутствие влияния.",
                                ImpactMap.AffectedTasks),
                            CreateImpactSection(
                                ImpactMapItemType.AffectedProjectDecision,
                                "Проектные решения",
                                "Ранее принятые решения, которые стоит сверить с изменением.",
                                "Затронутые проектные решения не указаны в предварительной карте; это не подтверждает отсутствие влияния.",
                                ImpactMap.AffectedProjectDecisions),
                            CreateImpactSection(
                                ImpactMapItemType.AffectedApiInterfaceDocumentTest,
                                "API, интерфейсы, документы и тесты",
                                "Артефакты реализации и проверки, которые могут потребовать обновления.",
                                "Затронутые API, интерфейсы, документы и тесты не указаны в предварительной карте; это не подтверждает отсутствие влияния.",
                                ImpactMap.AffectedApiInterfacesDocumentsTests),
                            CreateImpactSection(
                                ImpactMapItemType.AffectedArchitecturalConstraint,
                                "Архитектурные ограничения",
                                "Архитектурные ограничения, которые могут сузить или изменить варианты реализации.",
                                "Затронутые архитектурные ограничения не указаны в предварительной карте; это не подтверждает отсутствие влияния.",
                                ImpactMap.AffectedArchitecturalConstraints),
                            CreateImpactSection(
                                ImpactMapItemType.AffectedOrganizationalContextItem,
                                "Организационный контекст",
                                "Роли, процессы или организационные условия, которые стоит учесть.",
                                "Затронутый организационный контекст не указан в предварительной карте; это не подтверждает отсутствие влияния.",
                                ImpactMap.AffectedOrganizationalContextItems)
                        ]),
                    new(
                        "impact-map-risks-questions",
                        "Риски, вопросы и ограничения",
                        "Неопределенности, противоречия, риски и варианты, которые требуют человеческого рассмотрения.",
                        [
                            CreateImpactSection(
                                ImpactMapItemType.Contradiction,
                                "Противоречия",
                                "Потенциальные конфликты с требованиями, решениями или ограничениями.",
                                "Противоречия не указаны в предварительной карте; это не подтверждает их отсутствие.",
                                ImpactMap.Contradictions),
                            CreateImpactSection(
                                ImpactMapItemType.MissingInformation,
                                "Недостающая информация",
                                "Данные, которых не хватает для уверенного анализа.",
                                "Недостающая информация не указана в предварительной карте; это не подтверждает полноту контекста.",
                                ImpactMap.MissingInformation),
                            CreateImpactSection(
                                ImpactMapItemType.ClarificationQuestion,
                                "Вопросы для уточнения",
                                "Вопросы, которые нужно адресовать до управленческого рассмотрения.",
                                "Вопросы для уточнения не указаны в предварительной карте; это не подтверждает, что уточнения не нужны.",
                                ImpactMap.ClarificationQuestions),
                            CreateImpactSection(
                                ImpactMapItemType.Risk,
                                "Риски",
                                "Риски, которые стоит проверить и оценить эксперту.",
                                "Риски не указаны в предварительной карте; это не подтверждает их отсутствие.",
                                ImpactMap.Risks),
                            CreateImpactSection(
                                ImpactMapItemType.OptionForExpertReview,
                                "Варианты для экспертного рассмотрения",
                                "Возможные варианты дальнейшего рассмотрения человеком.",
                                "Варианты для экспертного рассмотрения не указаны в предварительной карте; эксперт может сформулировать их отдельно.",
                                ImpactMap.OptionsForExpertReview)
                        ])
                ];

        public IReadOnlyList<ImpactMapSummaryDetails> ImpactMapSummaryCards =>
            ImpactSectionGroups
                .Select(group => new ImpactMapSummaryDetails(
                    group.Title,
                    FormatItemCount(group.ItemCount),
                    group.Summary))
                .ToList();

        private static ImpactMapSectionDetails CreateImpactSection(
            ImpactMapItemType itemType,
            string title,
            string summary,
            string emptyState,
            IReadOnlyList<ImpactMapItem> items) =>
            new(
                $"impact-map-section-{ImpactMapIds.GetSectionId(itemType)}",
                title,
                summary,
                emptyState,
                items,
                FormatItemCount(items.Count));

        private static string FormatItemCount(int count)
        {
            if (count % 100 is >= 11 and <= 14)
            {
                return $"{count} элементов";
            }

            return (count % 10) switch
            {
                1 => $"{count} элемент",
                >= 2 and <= 4 => $"{count} элемента",
                _ => $"{count} элементов"
            };
        }
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
        IReadOnlyList<RetrievedContextItemDetails> RetrievedContextItems)
    {
        public bool HasRetrievedContextItems => RetrievedContextItems.Count > 0;

        public bool IsDirectLlmWithoutRetrievedContext =>
            AnalysisMode == RequirementImpactAssistant.Web.Domain.Enums.AnalysisMode.DirectLlm &&
            !HasRetrievedContextItems;
    }

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

    public sealed record ImpactMapSectionGroupDetails(
        string Anchor,
        string Title,
        string Summary,
        IReadOnlyList<ImpactMapSectionDetails> Sections)
    {
        public int ItemCount => Sections.Sum(section => section.Items.Count);
    }

    public sealed record ImpactMapSummaryDetails(
        string Title,
        string CountLabel,
        string Description);

    public sealed record ImpactMapSectionDetails(
        string Anchor,
        string Title,
        string Summary,
        string EmptyState,
        IReadOnlyList<ImpactMapItem> Items,
        string CountLabel);

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

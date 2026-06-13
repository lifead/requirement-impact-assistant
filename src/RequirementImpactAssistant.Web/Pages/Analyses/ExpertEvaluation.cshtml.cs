using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class ExpertEvaluationModel(ApplicationDbContext dbContext) : PageModel
{
    private const int BlankAdditionalRows = 3;

    [BindProperty]
    public ExpertEvaluationInput Input { get; set; } = new();

    public AnalysisEvaluationDetails? Analysis { get; private set; }

    [TempData]
    public string? ExpertEvaluationMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var analysis = await LoadAnalysisForEvaluationAsync(id, asTracking: false);
        if (!CanEvaluate(analysis))
        {
            return NotFound();
        }

        Analysis = ToDetails(analysis!);
        Input = ToInput(analysis!);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var analysis = await LoadAnalysisForEvaluationAsync(id, asTracking: true);
        if (!CanEvaluate(analysis))
        {
            return NotFound();
        }

        var impactItems = EnumerateImpactItems(analysis!.AiAnalysisResult!.ImpactMap!).ToList();
        var validTargetIds = impactItems.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);

        ValidateInput(ModelState, validTargetIds);
        if (!ModelState.IsValid)
        {
            Analysis = ToDetails(analysis);
            EnsureInputContainsCurrentImpactItems(impactItems);

            return Page();
        }

        var evaluation = analysis.ExpertEvaluation;
        if (evaluation is null)
        {
            evaluation = new ExpertEvaluation
            {
                AnalysisId = analysis.Id
            };
            analysis.ExpertEvaluation = evaluation;
            dbContext.ExpertEvaluations.Add(evaluation);
        }
        else
        {
            dbContext.ExpertEvaluatedItems.RemoveRange(evaluation.EvaluatedItems.ToList());
            dbContext.ExpertMissedItems.RemoveRange(evaluation.MissedItems.ToList());
            dbContext.ExpertCorrections.RemoveRange(evaluation.Corrections.ToList());
            await dbContext.SaveChangesAsync();
        }

        evaluation.ContextSufficiency = Input.ContextSufficiency;
        evaluation.ResultUsefulness = Input.ResultUsefulness;
        evaluation.GeneralComment = Normalize(Input.GeneralComment);

        foreach (var itemInput in Input.EvaluatedItems.Where(item => validTargetIds.Contains(item.TargetId)))
        {
            AddEvaluatedItem(evaluation.Id, new ExpertEvaluatedItem
            {
                TargetType = ExpertEvaluationTargetType.ImpactItem,
                TargetId = itemInput.TargetId,
                Mark = itemInput.Mark,
                Comment = Normalize(itemInput.Comment),
                CorrectionText = Normalize(itemInput.CorrectionText)
            });
        }

        foreach (var missedItemInput in Input.MissedItems.Where(item => item.HasAnyValue()))
        {
            AddMissedItem(evaluation.Id, new ExpertMissedItem
            {
                ItemType = missedItemInput.ItemType,
                Title = Normalize(missedItemInput.Title),
                Description = Normalize(missedItemInput.Description),
                Severity = missedItemInput.Severity,
                Comment = Normalize(missedItemInput.Comment)
            });
        }

        foreach (var correctionInput in Input.Corrections.Where(item => item.HasAnyValue() && validTargetIds.Contains(item.TargetId)))
        {
            AddCorrection(evaluation.Id, new ExpertCorrection
            {
                TargetType = ExpertEvaluationTargetType.ImpactItem,
                TargetId = correctionInput.TargetId,
                ItemType = correctionInput.ItemType,
                Text = Normalize(correctionInput.Text),
                Comment = Normalize(correctionInput.Comment)
            });
        }

        analysis.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();

        ExpertEvaluationMessage = "Expert evaluation saved.";

        return RedirectToPage("/Analyses/ExpertEvaluation", new { id = analysis.Id });
    }

    private void AddEvaluatedItem(Guid expertEvaluationId, ExpertEvaluatedItem item)
    {
        dbContext.ExpertEvaluatedItems.Add(item);
        dbContext.Entry(item).Property("ExpertEvaluationId").CurrentValue = expertEvaluationId;
    }

    private void AddMissedItem(Guid expertEvaluationId, ExpertMissedItem item)
    {
        dbContext.ExpertMissedItems.Add(item);
        dbContext.Entry(item).Property("ExpertEvaluationId").CurrentValue = expertEvaluationId;
    }

    private void AddCorrection(Guid expertEvaluationId, ExpertCorrection item)
    {
        dbContext.ExpertCorrections.Add(item);
        dbContext.Entry(item).Property("ExpertEvaluationId").CurrentValue = expertEvaluationId;
    }

    private async Task<Analysis?> LoadAnalysisForEvaluationAsync(Guid id, bool asTracking)
    {
        IQueryable<Analysis> query = dbContext.Analyses
            .AsSplitQuery()
            .Include(candidate => candidate.AiAnalysisResult)
            .Include(candidate => candidate.ExpertEvaluation)
                .ThenInclude(candidate => candidate!.EvaluatedItems)
            .Include(candidate => candidate.ExpertEvaluation)
                .ThenInclude(candidate => candidate!.MissedItems)
            .Include(candidate => candidate.ExpertEvaluation)
                .ThenInclude(candidate => candidate!.Corrections);

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(candidate => candidate.Id == id);
    }

    private static bool CanEvaluate(Analysis? analysis) =>
        analysis?.AiAnalysisResult?.ImpactMap is not null &&
        analysis.AiAnalysisResult.Status is AiAnalysisResultStatus.Completed or AiAnalysisResultStatus.CompletedWithWarnings &&
        EnumerateImpactItems(analysis.AiAnalysisResult.ImpactMap).Any();

    private static AnalysisEvaluationDetails ToDetails(Analysis analysis)
    {
        var impactSections = ToImpactSections(analysis.AiAnalysisResult!.ImpactMap!);

        return new AnalysisEvaluationDetails(
            analysis.Id,
            analysis.Title,
            analysis.Status,
            analysis.AiAnalysisResult.Status,
            impactSections,
            analysis.ExpertEvaluation is not null);
    }

    private static ExpertEvaluationInput ToInput(Analysis analysis)
    {
        var input = new ExpertEvaluationInput();
        var existingEvaluation = analysis.ExpertEvaluation;
        var existingItems = existingEvaluation?.EvaluatedItems.ToDictionary(item => item.TargetId, StringComparer.Ordinal)
            ?? new Dictionary<string, ExpertEvaluatedItem>(StringComparer.Ordinal);

        input.ContextSufficiency = existingEvaluation?.ContextSufficiency ?? ContextSufficiencyRating.NotAssessed;
        input.ResultUsefulness = existingEvaluation?.ResultUsefulness ?? ResultUsefulnessRating.NotAssessed;
        input.GeneralComment = existingEvaluation?.GeneralComment ?? string.Empty;

        foreach (var item in EnumerateImpactItems(analysis.AiAnalysisResult!.ImpactMap!))
        {
            existingItems.TryGetValue(item.Id, out var existingItem);
            input.EvaluatedItems.Add(new EvaluatedImpactItemInput
            {
                TargetId = item.Id,
                Mark = existingItem?.Mark ?? ExpertMark.NotSet,
                Comment = existingItem?.Comment ?? string.Empty,
                CorrectionText = existingItem?.CorrectionText ?? string.Empty
            });
        }

        if (existingEvaluation is not null)
        {
            input.MissedItems.AddRange(existingEvaluation.MissedItems.Select(item => new MissedItemInput
            {
                ItemType = item.ItemType,
                Title = item.Title,
                Description = item.Description,
                Severity = item.Severity,
                Comment = item.Comment
            }));

            input.Corrections.AddRange(existingEvaluation.Corrections.Select(item => new CorrectionInput
            {
                TargetId = item.TargetId,
                ItemType = item.ItemType,
                Text = item.Text,
                Comment = item.Comment
            }));
        }

        AddBlankAdditionalRows(input);

        return input;
    }

    private void EnsureInputContainsCurrentImpactItems(IReadOnlyList<ImpactMapItem> impactItems)
    {
        var submittedItems = new Dictionary<string, EvaluatedImpactItemInput>(StringComparer.Ordinal);
        foreach (var submittedItem in Input.EvaluatedItems)
        {
            submittedItems.TryAdd(submittedItem.TargetId, submittedItem);
        }

        Input.EvaluatedItems.Clear();

        foreach (var item in impactItems)
        {
            if (submittedItems.TryGetValue(item.Id, out var submittedItem))
            {
                Input.EvaluatedItems.Add(submittedItem);
            }
            else
            {
                Input.EvaluatedItems.Add(new EvaluatedImpactItemInput { TargetId = item.Id });
            }
        }
    }

    private static IReadOnlyList<ImpactMapSectionDetails> ToImpactSections(ImpactMap impactMap) =>
    [
        new("Change summary", [impactMap.ChangeSummary]),
        new("Affected requirements", impactMap.AffectedRequirements),
        new("Affected tasks", impactMap.AffectedTasks),
        new("Affected project decisions", impactMap.AffectedProjectDecisions),
        new("Affected API/interfaces/documents/tests", impactMap.AffectedApiInterfacesDocumentsTests),
        new("Affected architectural constraints", impactMap.AffectedArchitecturalConstraints),
        new("Affected organizational context", impactMap.AffectedOrganizationalContextItems),
        new("Contradictions", impactMap.Contradictions),
        new("Missing information", impactMap.MissingInformation),
        new("Clarification questions", impactMap.ClarificationQuestions),
        new("Risks", impactMap.Risks),
        new("Options for expert review", impactMap.OptionsForExpertReview),
        new("Preliminary assessment", [impactMap.PreliminaryAssessment])
    ];

    private static IEnumerable<ImpactMapItem> EnumerateImpactItems(ImpactMap impactMap) =>
        ToImpactSections(impactMap).SelectMany(section => section.Items);

    private void ValidateInput(ModelStateDictionary modelState, HashSet<string> validTargetIds)
    {
        var submittedTargetIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < Input.EvaluatedItems.Count; index++)
        {
            var item = Input.EvaluatedItems[index];
            var prefix = $"{nameof(Input)}.{nameof(Input.EvaluatedItems)}[{index}]";

            if (!submittedTargetIds.Add(item.TargetId))
            {
                modelState.AddModelError($"{prefix}.{nameof(EvaluatedImpactItemInput.TargetId)}", "Impact item target must be unique.");
            }

            if (!validTargetIds.Contains(item.TargetId))
            {
                modelState.AddModelError($"{prefix}.{nameof(EvaluatedImpactItemInput.TargetId)}", "Impact item target is not part of the current analysis result.");
            }

            if (item.Mark == ExpertMark.NotSet)
            {
                modelState.AddModelError($"{prefix}.{nameof(EvaluatedImpactItemInput.Mark)}", "Mark is required.");
            }

            if (item.Mark == ExpertMark.Corrected && string.IsNullOrWhiteSpace(item.CorrectionText))
            {
                modelState.AddModelError($"{prefix}.{nameof(EvaluatedImpactItemInput.CorrectionText)}", "Correction text is required for corrected items.");
            }
        }

        foreach (var missingTargetId in validTargetIds.Except(submittedTargetIds, StringComparer.Ordinal))
        {
            modelState.AddModelError(nameof(Input.EvaluatedItems), $"Mark is required for impact item '{missingTargetId}'.");
        }

        for (var index = 0; index < Input.MissedItems.Count; index++)
        {
            var item = Input.MissedItems[index];
            if (!item.HasAnyValue())
            {
                continue;
            }

            var prefix = $"{nameof(Input)}.{nameof(Input.MissedItems)}[{index}]";
            AddRequiredError(modelState, $"{prefix}.{nameof(MissedItemInput.Title)}", item.Title, "Title is required.");
            AddRequiredError(modelState, $"{prefix}.{nameof(MissedItemInput.Description)}", item.Description, "Description is required.");
        }

        for (var index = 0; index < Input.Corrections.Count; index++)
        {
            var item = Input.Corrections[index];
            if (!item.HasAnyValue())
            {
                continue;
            }

            var prefix = $"{nameof(Input)}.{nameof(Input.Corrections)}[{index}]";
            AddRequiredError(modelState, $"{prefix}.{nameof(CorrectionInput.TargetId)}", item.TargetId, "Target item is required.");
            AddRequiredError(modelState, $"{prefix}.{nameof(CorrectionInput.Text)}", item.Text, "Correction text is required.");

            if (!string.IsNullOrWhiteSpace(item.TargetId) && !validTargetIds.Contains(item.TargetId))
            {
                modelState.AddModelError($"{prefix}.{nameof(CorrectionInput.TargetId)}", "Correction target is not part of the current analysis result.");
            }
        }
    }

    private static void AddRequiredError(
        ModelStateDictionary modelState,
        string key,
        string value,
        string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            modelState.AddModelError(key, errorMessage);
        }
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

    private static void AddBlankAdditionalRows(ExpertEvaluationInput input)
    {
        for (var index = 0; index < BlankAdditionalRows; index++)
        {
            input.MissedItems.Add(new MissedItemInput());
            input.Corrections.Add(new CorrectionInput());
        }
    }

    public sealed record AnalysisEvaluationDetails(
        Guid Id,
        string Title,
        AnalysisStatus Status,
        AiAnalysisResultStatus AiResultStatus,
        IReadOnlyList<ImpactMapSectionDetails> ImpactSections,
        bool HasExpertEvaluation);

    public sealed record ImpactMapSectionDetails(
        string Title,
        IReadOnlyList<ImpactMapItem> Items);

    public sealed class ExpertEvaluationInput
    {
        public ContextSufficiencyRating ContextSufficiency { get; set; } = ContextSufficiencyRating.NotAssessed;

        public ResultUsefulnessRating ResultUsefulness { get; set; } = ResultUsefulnessRating.NotAssessed;

        public string GeneralComment { get; set; } = string.Empty;

        public List<EvaluatedImpactItemInput> EvaluatedItems { get; set; } = [];

        public List<MissedItemInput> MissedItems { get; set; } = [];

        public List<CorrectionInput> Corrections { get; set; } = [];
    }

    public sealed class EvaluatedImpactItemInput
    {
        [Required]
        public string TargetId { get; set; } = string.Empty;

        public ExpertMark Mark { get; set; } = ExpertMark.NotSet;

        public string Comment { get; set; } = string.Empty;

        public string CorrectionText { get; set; } = string.Empty;
    }

    public sealed class MissedItemInput
    {
        public ImpactMapItemType ItemType { get; set; } = ImpactMapItemType.Other;

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public ImpactSeverity Severity { get; set; } = ImpactSeverity.NotSpecified;

        public string Comment { get; set; } = string.Empty;

        public bool HasAnyValue() =>
            !string.IsNullOrWhiteSpace(Title) ||
            !string.IsNullOrWhiteSpace(Description) ||
            !string.IsNullOrWhiteSpace(Comment) ||
            ItemType != ImpactMapItemType.Other ||
            Severity != ImpactSeverity.NotSpecified;
    }

    public sealed class CorrectionInput
    {
        public string TargetId { get; set; } = string.Empty;

        public ImpactMapItemType ItemType { get; set; } = ImpactMapItemType.Other;

        public string Text { get; set; } = string.Empty;

        public string Comment { get; set; } = string.Empty;

        public bool HasAnyValue() =>
            !string.IsNullOrWhiteSpace(TargetId) ||
            !string.IsNullOrWhiteSpace(Text) ||
            !string.IsNullOrWhiteSpace(Comment) ||
            ItemType != ImpactMapItemType.Other;
    }
}

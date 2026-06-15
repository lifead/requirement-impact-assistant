using Microsoft.AspNetCore.Mvc.Rendering;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public static class AnalysisUiText
{
    public static IEnumerable<SelectListItem> ContextFragmentTypeItems() =>
        Enum.GetValues<ContextFragmentType>()
            .Select(value => new SelectListItem(ContextFragmentTypeLabel(value), ((int)value).ToString()));

    public static IEnumerable<SelectListItem> ExpertConclusionTypeItems() =>
        Enum.GetValues<ExpertConclusionType>()
            .Select(value => new SelectListItem(ExpertConclusionTypeLabel(value), ((int)value).ToString()));

    public static IEnumerable<SelectListItem> ExpertMarkItems() =>
        Enum.GetValues<ExpertMark>()
            .Select(value => new SelectListItem(ExpertMarkLabel(value), ((int)value).ToString()));

    public static IEnumerable<SelectListItem> ContextSufficiencyItems() =>
        Enum.GetValues<ContextSufficiencyRating>()
            .Select(value => new SelectListItem(ContextSufficiencyLabel(value), ((int)value).ToString()));

    public static IEnumerable<SelectListItem> ResultUsefulnessItems() =>
        Enum.GetValues<ResultUsefulnessRating>()
            .Select(value => new SelectListItem(ResultUsefulnessLabel(value), ((int)value).ToString()));

    public static IEnumerable<SelectListItem> ImpactMapItemTypeItems() =>
        Enum.GetValues<ImpactMapItemType>()
            .Select(value => new SelectListItem(ImpactMapItemTypeLabel(value), ((int)value).ToString()));

    public static IEnumerable<SelectListItem> ImpactSeverityItems() =>
        Enum.GetValues<ImpactSeverity>()
            .Select(value => new SelectListItem(ImpactSeverityLabel(value), ((int)value).ToString()));

    public static string AnalysisStatusLabel(AnalysisStatus status) =>
        status switch
        {
            AnalysisStatus.Draft => "Черновик",
            AnalysisStatus.InputIncomplete => "Ввод не завершен",
            AnalysisStatus.ReadyForAnalysis => "Готово к анализу",
            AnalysisStatus.LlmAnalysisRunning => "LLM-анализ выполняется",
            AnalysisStatus.LlmAnalysisCompleted => "LLM-анализ завершен",
            AnalysisStatus.LlmAnalysisFailed => "LLM-анализ не выполнен",
            AnalysisStatus.NeedsExpertEvaluation => "Требуется экспертная оценка",
            AnalysisStatus.ReturnedForClarification => "Возвращено на уточнение",
            AnalysisStatus.NeedsReanalysis => "Требуется повторный анализ",
            AnalysisStatus.ExpertConclusionFixed => "Экспертное заключение зафиксировано",
            AnalysisStatus.Exported => "Экспортировано",
            _ => status.ToString()
        };

    public static string AiResultStatusLabel(AiAnalysisResultStatus status) =>
        status switch
        {
            AiAnalysisResultStatus.NotStarted => "Не запущено",
            AiAnalysisResultStatus.Running => "Выполняется",
            AiAnalysisResultStatus.Completed => "Завершено",
            AiAnalysisResultStatus.CompletedWithWarnings => "Завершено с предупреждениями",
            AiAnalysisResultStatus.Failed => "Не выполнено",
            AiAnalysisResultStatus.InvalidResponse => "Некорректный ответ",
            _ => status.ToString()
        };

    public static string AnalysisModeLabel(AnalysisMode mode) =>
        mode switch
        {
            AnalysisMode.DirectLlm => "Direct LLM",
            AnalysisMode.ExternalRag => "External AI/RAG",
            _ => mode.ToString()
        };

    public static string RetrievedContextStateLabel(RetrievedContextState state) =>
        state switch
        {
            RetrievedContextState.Unavailable => "Контекст не сохранен",
            RetrievedContextState.Available => "Контекст доступен",
            RetrievedContextState.MetadataOnly => "Сохранены только метаданные контекста",
            RetrievedContextState.Partial => "Контекст сохранен частично",
            _ => state.ToString()
        };

    public static string ManualContextForwardingLabel(bool forwarded) =>
        forwarded
            ? "Передавался во внешний контур"
            : "Не передавался во внешний контур";

    public static string ContextFragmentTypeLabel(ContextFragmentType type) =>
        type switch
        {
            ContextFragmentType.Other => "Другое",
            ContextFragmentType.Task => "Задача",
            ContextFragmentType.DocumentFragment => "Фрагмент документа",
            ContextFragmentType.Comment => "Комментарий",
            ContextFragmentType.ApiDescription => "Описание API",
            ContextFragmentType.ArchitecturalConstraint => "Архитектурное ограничение",
            ContextFragmentType.TestCase => "Тестовый сценарий",
            ContextFragmentType.PreviousDecision => "Ранее принятое решение",
            _ => type.ToString()
        };

    public static string ExpertConclusionTypeLabel(ExpertConclusionType type) =>
        type switch
        {
            ExpertConclusionType.NotSet => "Не выбрано",
            ExpertConclusionType.Accept => "Принять",
            ExpertConclusionType.AcceptWithLimitations => "Принять с ограничениями",
            ExpertConclusionType.SendForClarification => "Отправить на уточнение",
            ExpertConclusionType.SplitIntoSeveralTasks => "Разделить на несколько задач",
            ExpertConclusionType.Reject => "Отклонить",
            ExpertConclusionType.ReturnForReanalysis => "Вернуть на повторный анализ",
            _ => type.ToString()
        };

    public static string ExpertMarkLabel(ExpertMark mark) =>
        mark switch
        {
            ExpertMark.NotSet => "Не выбрано",
            ExpertMark.Confirmed => "Подтверждено",
            ExpertMark.Corrected => "Исправлено",
            ExpertMark.Rejected => "Отклонено",
            ExpertMark.NeedsClarification => "Требует уточнения",
            _ => mark.ToString()
        };

    public static string ContextSufficiencyLabel(ContextSufficiencyRating rating) =>
        rating switch
        {
            ContextSufficiencyRating.NotAssessed => "Не оценено",
            ContextSufficiencyRating.Sufficient => "Достаточный",
            ContextSufficiencyRating.PartiallySufficient => "Частично достаточный",
            ContextSufficiencyRating.Insufficient => "Недостаточный",
            ContextSufficiencyRating.NeedsClarification => "Требует уточнения",
            _ => rating.ToString()
        };

    public static string ResultUsefulnessLabel(ResultUsefulnessRating rating) =>
        rating switch
        {
            ResultUsefulnessRating.NotAssessed => "Не оценено",
            ResultUsefulnessRating.Useful => "Полезен",
            ResultUsefulnessRating.PartiallyUseful => "Частично полезен",
            ResultUsefulnessRating.NotUseful => "Не полезен",
            ResultUsefulnessRating.NeedsClarification => "Требует уточнения",
            _ => rating.ToString()
        };

    public static string ImpactMapItemTypeLabel(ImpactMapItemType type) =>
        type switch
        {
            ImpactMapItemType.Other => "Другое",
            ImpactMapItemType.ChangeSummary => "Сводка изменения",
            ImpactMapItemType.AffectedRequirement => "Затронутое требование",
            ImpactMapItemType.AffectedTask => "Затронутая задача",
            ImpactMapItemType.AffectedProjectDecision => "Затронутое проектное решение",
            ImpactMapItemType.AffectedApiInterfaceDocumentTest => "Затронутые API, интерфейсы, документы или тесты",
            ImpactMapItemType.AffectedArchitecturalConstraint => "Затронутое архитектурное ограничение",
            ImpactMapItemType.AffectedOrganizationalContextItem => "Затронутый элемент организационного контекста",
            ImpactMapItemType.Contradiction => "Противоречие",
            ImpactMapItemType.MissingInformation => "Недостающая информация",
            ImpactMapItemType.ClarificationQuestion => "Вопрос для уточнения",
            ImpactMapItemType.Risk => "Риск",
            ImpactMapItemType.OptionForExpertReview => "Вариант для экспертного рассмотрения",
            ImpactMapItemType.PreliminaryAssessment => "Предварительная оценка",
            _ => type.ToString()
        };

    public static string ImpactSeverityLabel(ImpactSeverity severity) =>
        severity switch
        {
            ImpactSeverity.NotSpecified => "Не указана",
            ImpactSeverity.Low => "Низкая",
            ImpactSeverity.Medium => "Средняя",
            ImpactSeverity.High => "Высокая",
            ImpactSeverity.Critical => "Критическая",
            _ => severity.ToString()
        };
}

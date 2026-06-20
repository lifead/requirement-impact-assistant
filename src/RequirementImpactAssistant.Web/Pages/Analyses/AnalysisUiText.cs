using Microsoft.AspNetCore.Mvc.Rendering;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public static class AnalysisUiText
{
    public const string ProjectRequestTypeFieldLabel = "Тип проектного запроса";
    public const string ProjectRequestTypePlaceholder = "Выберите тип проектного запроса";
    public const string ProjectRequestTypeRequiredMessage = "Тип проектного запроса обязателен.";

    public const string OriginalDescriptionLabel = "Текущее состояние";
    public const string OriginalDescriptionHelpText = "Кратко опишите текущую точку отсчета: требование, процесс, API, интеграцию, ограничение или проектное решение на момент анализа.";
    public const string OriginalDescriptionRequiredMessage = "Текущее состояние обязательно.";

    public const string ProjectRequestLabel = "Проектное изменение";
    public const string ProjectRequestHelpText = "Опишите предлагаемое проектное изменение как предмет анализа, без предположения, что оно уже принято.";
    public const string ProjectRequestRequiredMessage = "Проектное изменение обязательно.";

    public const string SituationDescriptionLabel = "Ситуация и причина изменения";
    public const string SituationDescriptionHelpText = "Укажите обстоятельства и причину, из-за которых рассматривается изменение.";
    public const string SituationDescriptionRequiredMessage = "Ситуация и причина изменения обязательны.";

    public const string ChangeSourceLabel = "Источник изменения";
    public const string ChangeSourceHelpText = "Укажите источник запроса: встреча, документ, задача, письмо или другое основание.";
    public const string ChangeSourceRequiredMessage = "Источник изменения обязателен.";

    public const string ExpertConclusionHumanRecordHelpText =
        "Экспертное заключение фиксирует вывод человека по сохраненному предварительному материалу.";

    public const string ExpertConclusionReadOnlySummaryText =
        "При сохранении будет записан тип заключения, комментарий, обоснование и дата фиксации. Программа не создает задачи, уведомления, workflow, внешние запросы и не запускает повторный анализ автоматически.";

    public const string PassiveExpertConclusionTypeHelpText =
        "Типы с разделением на задачи или повторным анализом являются пассивной фиксацией экспертного вывода и не выполняют эти действия автоматически.";

    public static IEnumerable<SelectListItem> ContextFragmentTypeItems() =>
        Enum.GetValues<ContextFragmentType>()
            .Select(value => new SelectListItem(ContextFragmentTypeLabel(value), ((int)value).ToString()));

    public static IEnumerable<SelectListItem> ProjectRequestTypeItems() =>
        Enum.GetValues<ProjectRequestType>()
            .Select(value => new SelectListItem(ProjectRequestTypeLabel(value), ((int)value).ToString()));

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

    public static string AnalysisModeReviewDescription(AnalysisMode mode) =>
        mode switch
        {
            AnalysisMode.DirectLlm =>
                "Использует настроенный в приложении LLM provider без проверки внешнего AI/RAG-контура на этой странице.",
            AnalysisMode.ExternalRag =>
                "Может использовать mock fallback или внешний adapter в зависимости от конфигурации приложения; доступность фиксируется только при запуске анализа.",
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

    public static string RetrievedContextStateDescription(RetrievedContextState state) =>
        state switch
        {
            RetrievedContextState.Available => "Сохраненные элементы содержат текстовые основания внешнего AI/RAG результата.",
            RetrievedContextState.MetadataOnly => "Сохранены только сведения об источниках; полный текст и выдержки не сохранены.",
            RetrievedContextState.Partial => "Часть оснований сохранена не полностью; учитывайте ограничения для воспроизводимости.",
            RetrievedContextState.Unavailable => "Основание внешнего AI/RAG результата не сохранено, поэтому воспроизводимость по источникам ограничена.",
            _ => state.ToString()
        };

    public static string RetrievedContextItemCompletenessLabel(RetrievedContextItemCompleteness completeness) =>
        completeness switch
        {
            RetrievedContextItemCompleteness.FullText => "Полный текст",
            RetrievedContextItemCompleteness.ExcerptOnly => "Только выдержка",
            RetrievedContextItemCompleteness.MetadataOnly => "Только метаданные",
            RetrievedContextItemCompleteness.Unavailable => "Недоступно",
            _ => completeness.ToString()
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

    public static string ProjectRequestTypeLabel(ProjectRequestType type) =>
        type switch
        {
            ProjectRequestType.RequirementChange => "Изменение требования",
            ProjectRequestType.NewFunctionality => "Новая функциональность",
            ProjectRequestType.DefectFix => "Исправление дефекта",
            ProjectRequestType.RequirementClarification => "Уточнение требования",
            ProjectRequestType.ApiOrIntegrationChange => "Изменение API / интеграции",
            ProjectRequestType.ArchitecturalConstraintChange => "Изменение архитектурного ограничения",
            ProjectRequestType.ProjectDecisionChange => "Изменение проектного решения",
            ProjectRequestType.UserScenarioChange => "Изменение пользовательского сценария",
            ProjectRequestType.Other => "Другое",
            _ => type.ToString()
        };

    public static string ExpertConclusionTypeLabel(ExpertConclusionType type) =>
        type switch
        {
            ExpertConclusionType.NotSet => "Не выбрано",
            ExpertConclusionType.Accept => "Принять",
            ExpertConclusionType.AcceptWithLimitations => "Принять с ограничениями",
            ExpertConclusionType.SendForClarification => "Требуется уточнение",
            ExpertConclusionType.SplitIntoSeveralTasks => "Рекомендовать разделение на несколько задач",
            ExpertConclusionType.Reject => "Отклонить",
            ExpertConclusionType.ReturnForReanalysis => "Рекомендовать повторный анализ",
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

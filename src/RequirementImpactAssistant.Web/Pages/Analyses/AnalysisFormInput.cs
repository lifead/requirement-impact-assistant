using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class AnalysisFormInput
{
    [Required(ErrorMessage = "Название обязательно.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Тип проектного запроса обязателен.")]
    public ProjectRequestType? ProjectRequestType { get; set; }

    [Required(ErrorMessage = "Исходное описание обязательно.")]
    public string OriginalDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Проектный запрос обязателен.")]
    public string ProjectRequest { get; set; } = string.Empty;

    [Required(ErrorMessage = "Описание ситуации обязательно.")]
    public string SituationDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Источник изменения обязателен.")]
    public string ChangeSource { get; set; } = string.Empty;

    public static AnalysisFormInput FromAnalysis(Analysis analysis) =>
        new()
        {
            Title = analysis.Title,
            ProjectRequestType = analysis.ProjectRequestType,
            OriginalDescription = analysis.OriginalDescription,
            ProjectRequest = analysis.ProjectRequest,
            SituationDescription = analysis.SituationDescription,
            ChangeSource = analysis.ChangeSource
        };

    public void ApplyTo(Analysis analysis)
    {
        analysis.Title = Title.Trim();
        analysis.ProjectRequestType = ProjectRequestType
            ?? RequirementImpactAssistant.Web.Domain.Enums.ProjectRequestType.Other;
        analysis.OriginalDescription = OriginalDescription.Trim();
        analysis.ProjectRequest = ProjectRequest.Trim();
        analysis.SituationDescription = SituationDescription.Trim();
        analysis.ChangeSource = ChangeSource.Trim();
    }

    public bool Validate(ModelStateDictionary modelState)
    {
        AddRequiredError(modelState, nameof(Title), Title, "Название обязательно.");
        AddRequiredError(modelState, nameof(OriginalDescription), OriginalDescription, "Исходное описание обязательно.");
        AddRequiredError(modelState, nameof(ProjectRequest), ProjectRequest, "Проектный запрос обязателен.");
        AddRequiredError(modelState, nameof(SituationDescription), SituationDescription, "Описание ситуации обязательно.");
        AddRequiredError(modelState, nameof(ChangeSource), ChangeSource, "Источник изменения обязателен.");

        AddRequiredError(modelState, nameof(ProjectRequestType), ProjectRequestType);

        return modelState.IsValid;
    }

    private static void AddRequiredError(
        ModelStateDictionary modelState,
        string propertyName,
        string value,
        string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            modelState.AddModelError($"{nameof(AnalysisFormInput)}.{propertyName}", errorMessage);
        }
    }

    private static void AddRequiredError(
        ModelStateDictionary modelState,
        string propertyName,
        ProjectRequestType? value)
    {
        if (value is null)
        {
            modelState.AddModelError($"{nameof(AnalysisFormInput)}.{propertyName}", "Тип проектного запроса обязателен.");
        }
    }
}

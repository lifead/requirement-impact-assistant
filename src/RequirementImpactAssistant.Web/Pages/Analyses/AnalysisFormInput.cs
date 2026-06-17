using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class AnalysisFormInput
{
    [Required(ErrorMessage = "Название обязательно.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = AnalysisUiText.ProjectRequestTypeRequiredMessage)]
    public ProjectRequestType? ProjectRequestType { get; set; }

    [Required(ErrorMessage = AnalysisUiText.OriginalDescriptionRequiredMessage)]
    public string OriginalDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = AnalysisUiText.ProjectRequestRequiredMessage)]
    public string ProjectRequest { get; set; } = string.Empty;

    [Required(ErrorMessage = AnalysisUiText.SituationDescriptionRequiredMessage)]
    public string SituationDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = AnalysisUiText.ChangeSourceRequiredMessage)]
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
        AddRequiredError(modelState, nameof(OriginalDescription), OriginalDescription, AnalysisUiText.OriginalDescriptionRequiredMessage);
        AddRequiredError(modelState, nameof(ProjectRequest), ProjectRequest, AnalysisUiText.ProjectRequestRequiredMessage);
        AddRequiredError(modelState, nameof(SituationDescription), SituationDescription, AnalysisUiText.SituationDescriptionRequiredMessage);
        AddRequiredError(modelState, nameof(ChangeSource), ChangeSource, AnalysisUiText.ChangeSourceRequiredMessage);

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
            modelState.AddModelError($"{nameof(AnalysisFormInput)}.{propertyName}", AnalysisUiText.ProjectRequestTypeRequiredMessage);
        }
    }
}

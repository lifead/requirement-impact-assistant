using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RequirementImpactAssistant.Web.Domain;

namespace RequirementImpactAssistant.Web.Pages.Analyses;

public sealed class AnalysisFormInput
{
    [Required(ErrorMessage = "Title is required.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Original description is required.")]
    public string OriginalDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Project request is required.")]
    public string ProjectRequest { get; set; } = string.Empty;

    [Required(ErrorMessage = "Situation description is required.")]
    public string SituationDescription { get; set; } = string.Empty;

    [Required(ErrorMessage = "Change source is required.")]
    public string ChangeSource { get; set; } = string.Empty;

    public static AnalysisFormInput FromAnalysis(Analysis analysis) =>
        new()
        {
            Title = analysis.Title,
            OriginalDescription = analysis.OriginalDescription,
            ProjectRequest = analysis.ProjectRequest,
            SituationDescription = analysis.SituationDescription,
            ChangeSource = analysis.ChangeSource
        };

    public void ApplyTo(Analysis analysis)
    {
        analysis.Title = Title.Trim();
        analysis.OriginalDescription = OriginalDescription.Trim();
        analysis.ProjectRequest = ProjectRequest.Trim();
        analysis.SituationDescription = SituationDescription.Trim();
        analysis.ChangeSource = ChangeSource.Trim();
    }

    public bool Validate(ModelStateDictionary modelState)
    {
        AddRequiredError(modelState, nameof(Title), Title, "Title is required.");
        AddRequiredError(modelState, nameof(OriginalDescription), OriginalDescription, "Original description is required.");
        AddRequiredError(modelState, nameof(ProjectRequest), ProjectRequest, "Project request is required.");
        AddRequiredError(modelState, nameof(SituationDescription), SituationDescription, "Situation description is required.");
        AddRequiredError(modelState, nameof(ChangeSource), ChangeSource, "Change source is required.");

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
}

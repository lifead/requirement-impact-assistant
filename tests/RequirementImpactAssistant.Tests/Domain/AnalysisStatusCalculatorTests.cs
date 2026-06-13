using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Tests.Domain;

public sealed class AnalysisStatusCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsDraftWhenNoMainFieldsAreFilled()
    {
        var analysis = new Analysis();

        var status = AnalysisStatusCalculator.Calculate(analysis);

        Assert.Equal(AnalysisStatus.Draft, status);
    }

    [Fact]
    public void Calculate_ReturnsInputIncompleteWhenAnyMainFieldIsMissing()
    {
        var analysis = CreateCompleteAnalysis();
        analysis.ChangeSource = " ";

        var status = AnalysisStatusCalculator.Calculate(analysis);

        Assert.Equal(AnalysisStatus.InputIncomplete, status);
    }

    [Fact]
    public void Calculate_ReturnsReadyForAnalysisWhenMinimalInputSetIsFilled()
    {
        var analysis = CreateCompleteAnalysis();

        var status = AnalysisStatusCalculator.Calculate(analysis);

        Assert.Equal(AnalysisStatus.ReadyForAnalysis, status);
    }

    private static Analysis CreateCompleteAnalysis() =>
        new()
        {
            Title = "Payment API change",
            OriginalDescription = "Original requirement",
            ProjectRequest = "Project request",
            SituationDescription = "Current situation",
            ChangeSource = "Customer request"
        };
}

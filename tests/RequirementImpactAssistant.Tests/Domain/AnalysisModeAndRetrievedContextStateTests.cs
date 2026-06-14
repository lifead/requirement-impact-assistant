using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Tests.Domain;

public sealed class AnalysisModeAndRetrievedContextStateTests
{
    [Theory]
    [InlineData(AnalysisMode.DirectLlm, "DirectLlm")]
    [InlineData(AnalysisMode.ExternalRag, "ExternalRag")]
    public void AnalysisMode_HasStableStringRoundTrip(
        AnalysisMode mode,
        string expectedName)
    {
        Assert.Equal(expectedName, mode.ToString());
        Assert.True(Enum.TryParse<AnalysisMode>(expectedName, ignoreCase: false, out var parsedMode));
        Assert.Equal(mode, parsedMode);
    }

    [Theory]
    [InlineData(RetrievedContextState.Available, "Available")]
    [InlineData(RetrievedContextState.MetadataOnly, "MetadataOnly")]
    [InlineData(RetrievedContextState.Unavailable, "Unavailable")]
    [InlineData(RetrievedContextState.Partial, "Partial")]
    public void RetrievedContextState_HasStableStringRoundTrip(
        RetrievedContextState state,
        string expectedName)
    {
        Assert.Equal(expectedName, state.ToString());
        Assert.True(Enum.TryParse<RetrievedContextState>(expectedName, ignoreCase: false, out var parsedState));
        Assert.Equal(state, parsedState);
    }
}

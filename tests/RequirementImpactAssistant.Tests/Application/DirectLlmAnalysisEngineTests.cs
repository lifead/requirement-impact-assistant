using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Domain.Impact;
using System.Net.Http;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class DirectLlmAnalysisEngineTests
{
    [Fact]
    public async Task AnalyzeAsync_BuildsPromptFromInputContextAndBoundaryNotice()
    {
        var provider = new CapturingLlmProvider(new LlmProviderResponse(
            LlmProviderResponseStatus.Succeeded,
            new ImpactMap(),
            "raw provider response",
            []));
        var engine = CreateEngine(provider);
        var request = CreateRequest();

        await engine.AnalyzeAsync(request);

        Assert.Equal(1, provider.CallCount);
        Assert.NotNull(provider.LastRequest);
        Assert.Equal(LlmProviderNames.DeepSeek, provider.LastRequest.Provider);
        Assert.Contains("Gateway migration", provider.LastRequest.Prompt);
        Assert.Contains("Original requirement", provider.LastRequest.Prompt);
        Assert.Contains("Project request", provider.LastRequest.Prompt);
        Assert.Contains("Current situation", provider.LastRequest.Prompt);
        Assert.Contains("Change source", provider.LastRequest.Prompt);
        Assert.Contains("Task tracker", provider.LastRequest.Prompt);
        Assert.Contains("Task context", provider.LastRequest.Prompt);
        Assert.Contains("Do not make management decisions", provider.LastRequest.Prompt);
        Assert.Contains("AI/LLM output is preliminary analytical material", provider.LastRequest.Prompt);
        Assert.Contains("changeSummary", provider.LastRequest.Prompt);
        Assert.Contains("preliminaryAssessment", provider.LastRequest.Prompt);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsProviderImpactMapRawResponseStatusAndErrors()
    {
        var impactMap = new ImpactMap
        {
            ChangeSummary =
            {
                Title = "Potential migration impact",
                Description = "Authentication gateway may need an update.",
                Severity = ImpactSeverity.Medium
            }
        };
        var provider = new CapturingLlmProvider(new LlmProviderResponse(
            LlmProviderResponseStatus.Partial,
            impactMap,
            "raw partial response",
            ["missing optional organizational context"]));
        var engine = CreateEngine(provider);

        var response = await engine.AnalyzeAsync(CreateRequest());

        Assert.Equal(AiAnalysisResponseStatus.Partial, response.Status);
        Assert.Same(impactMap, response.ImpactMap);
        Assert.Equal("raw partial response", response.RawResponse);
        Assert.Equal(["missing optional organizational context"], response.Errors);
        Assert.True(response.BoundaryNotice.AiDoesNotMakeManagementDecision);
    }

    [Fact]
    public async Task AnalyzeAsync_ProviderFailureReturnsFailedResponseWithoutManagementDecision()
    {
        var provider = new CapturingLlmProvider(new LlmProviderResponse(
            LlmProviderResponseStatus.Failed,
            null,
            "provider failure raw response",
            ["provider unavailable"]));
        var engine = CreateEngine(provider);

        var response = await engine.AnalyzeAsync(CreateRequest());

        Assert.Equal(AiAnalysisResponseStatus.Failed, response.Status);
        Assert.Null(response.ImpactMap);
        Assert.Equal("provider failure raw response", response.RawResponse);
        Assert.Equal(["provider unavailable"], response.Errors);
        Assert.True(response.BoundaryNotice.IsPreliminaryAnalyticalMaterial);
        Assert.True(response.BoundaryNotice.AiDoesNotMakeManagementDecision);
    }

    [Fact]
    public async Task AnalyzeAsync_ProviderExceptionReturnsFailedResponseWithoutImpactMap()
    {
        var engine = CreateEngine(new ThrowingLlmProvider());

        var response = await engine.AnalyzeAsync(CreateRequest());

        Assert.Equal(AiAnalysisResponseStatus.Failed, response.Status);
        Assert.Null(response.ImpactMap);
        Assert.Empty(response.RawResponse);
        Assert.Contains("LLM provider call failed before an analytical result was returned.", response.Errors);
        Assert.Contains("network disabled for test", response.Errors);
        Assert.True(response.BoundaryNotice.AiDoesNotMakeManagementDecision);
    }

    [Fact]
    public void DirectLlmAnalysisEngine_DependsOnProviderBoundaryOnly()
    {
        var constructorParameters = typeof(DirectLlmAnalysisEngine)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToList();

        Assert.Contains(typeof(ILlmProvider), constructorParameters);
        Assert.Contains(typeof(IOptions<AiAnalysisOptions>), constructorParameters);
        Assert.DoesNotContain(typeof(HttpClient), constructorParameters);
        Assert.DoesNotContain(constructorParameters, type => type.FullName?.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(constructorParameters, type => type.FullName?.Contains("DeepSeek", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static DirectLlmAnalysisEngine CreateEngine(ILlmProvider provider) =>
        new(
            provider,
            Options.Create(new AiAnalysisOptions
            {
                Provider = LlmProviderNames.DeepSeek
            }));

    private static AiAnalysisRequest CreateRequest()
    {
        var analysisId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var fragmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var snapshot = new AnalysisInputSnapshot(
            AnalysisId: analysisId,
            Analysis: new AnalysisInputFields(
                Title: "Gateway migration",
                OriginalDescription: "Original requirement",
                ProjectRequest: "Project request",
                SituationDescription: "Current situation",
                ChangeSource: "Change source"),
            ContextFragments:
            [
                new AnalysisContextFragmentSnapshot(
                    Id: fragmentId,
                    Type: "Task",
                    Source: "Task tracker",
                    Text: "Task context",
                    FileName: null)
            ]);

        return new AiAnalysisRequest(
            InputSnapshot: snapshot,
            InputSnapshotJson:
                "{\"analysisId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"analysis\":{\"title\":\"Gateway migration\",\"originalDescription\":\"Original requirement\",\"projectRequest\":\"Project request\",\"situationDescription\":\"Current situation\",\"changeSource\":\"Change source\"},\"contextFragments\":[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"type\":\"Task\",\"source\":\"Task tracker\",\"text\":\"Task context\",\"fileName\":null}]}",
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default);
    }

    private sealed class CapturingLlmProvider(LlmProviderResponse response) : ILlmProvider
    {
        public int CallCount { get; private set; }

        public LlmProviderRequest? LastRequest { get; private set; }

        public Task<LlmProviderResponse> CompleteAsync(
            LlmProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;

            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingLlmProvider : ILlmProvider
    {
        public Task<LlmProviderResponse> CompleteAsync(
            LlmProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("network disabled for test");
        }
    }
}

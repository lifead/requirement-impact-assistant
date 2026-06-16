using System.Text.Json;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class DifyAgentRequestContractTests
{
    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://localhost/")]
    [InlineData("http://localhost/v1/chat-messages")]
    [InlineData("http://localhost/v1/chat-messages/")]
    public void NormalizeChatMessagesEndpoint_AcceptsBaseUrlOrFullEndpoint(string configuredEndpoint)
    {
        var endpoint = DifyAgentRequestContract.NormalizeChatMessagesEndpoint(configuredEndpoint);

        Assert.NotNull(endpoint);
        Assert.Equal("http://localhost/v1/chat-messages", endpoint.ToString());
    }

    [Fact]
    public void SerializeRequest_CreatesDifyAgentStreamingBodyContract()
    {
        var request = CreateRequest();

        var body = DifyAgentRequestContract.SerializeRequest(request);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var inputs = root.GetProperty("inputs");

        Assert.Equal("Original requirement text", inputs.GetProperty("originalRequirement").GetString());
        Assert.Equal("Situation text", inputs.GetProperty("situation").GetString());
        Assert.Equal("demo-api-check", inputs.GetProperty("source").GetString());
        Assert.Equal("Analyze the requested change.", root.GetProperty("query").GetString());
        Assert.Equal("streaming", root.GetProperty("response_mode").GetString());
        Assert.Equal(string.Empty, root.GetProperty("conversation_id").GetString());
        Assert.Equal("analysis-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", root.GetProperty("user").GetString());

        Assert.DoesNotContain("blocking", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateRequest_DoesNotExposeWorkflowOrAppIdOrApiKeyInAgentBody()
    {
        var body = DifyAgentRequestContract.SerializeRequest(CreateRequest());

        Assert.DoesNotContain("WorkflowOrAppId", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiKey", body, StringComparison.Ordinal);
    }

    private static ExternalRagAdapterRequest CreateRequest()
    {
        var snapshot = new AnalysisInputSnapshot(
            AnalysisId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Analysis: new AnalysisInputFields(
                Title: "Demo analysis",
                OriginalDescription: "Original requirement text",
                ProjectRequest: "Analyze the requested change.",
                SituationDescription: "Situation text",
                ChangeSource: "demo-api-check"),
            ContextFragments: []);

        return new ExternalRagAdapterRequest(
            CorrelationId: snapshot.AnalysisId,
            InputSnapshot: snapshot,
            ManualContext: null,
            CanForwardManualContextToExternalAiOrRag: false,
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default,
            ExecutionMetadata: new ExternalRagRequestMetadata(
                EngineName: nameof(ExternalRagAnalysisEngine),
                RequestedProfileName: "ria-mvp2-demo",
                SanitizedProperties: new Dictionary<string, string>()));
    }
}

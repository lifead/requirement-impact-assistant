using System.Text.Json;
using RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class DifyAgentSseStreamParserTests
{
    [Fact]
    public void Parse_CollectsAnswerFragmentsAndMessageEndMetadata()
    {
        var payload = """
            data: {"event":"agent_message","answer":"{\"changeSummary\":\""}

            data: {"event":"agent_thought","thought":"Internal reasoning is ignored by the parser."}
            data: {"event":"agent_message","answer":"Storage cell validation\"}"}
            data: {"event":"message_end","message_id":"msg-123","conversation_id":"conv-456","metadata":{"usage":{"prompt_tokens":12,"completion_tokens":5,"total_tokens":17,"currency":"USD"}}}

            """;

        var result = DifyAgentSseStreamParser.Parse(payload);

        Assert.True(result.IsComplete);
        Assert.False(result.HasMalformedEvents);
        Assert.Empty(result.Warnings);
        Assert.Equal("{\"changeSummary\":\"Storage cell validation\"}", result.Answer);
        Assert.Equal(
            ["{\"changeSummary\":\"", "Storage cell validation\"}"],
            result.AnswerFragments);
        Assert.Equal(1, result.AgentThoughtEventCount);
        Assert.Equal(0, result.UnknownEventCount);

        Assert.NotNull(result.MessageEndMetadata);
        Assert.Equal("msg-123", result.MessageEndMetadata.MessageId);
        Assert.Equal("conv-456", result.MessageEndMetadata.ConversationId);
        Assert.Equal("12", result.MessageEndMetadata.Usage["prompt_tokens"]);
        Assert.Equal("5", result.MessageEndMetadata.Usage["completion_tokens"]);
        Assert.Equal("17", result.MessageEndMetadata.Usage["total_tokens"]);
        Assert.Equal("USD", result.MessageEndMetadata.Usage["currency"]);
        Assert.False(result.MessageEndMetadata.HasRetrieverResourcesMetadata);
        Assert.Empty(result.MessageEndMetadata.RetrieverResources);
    }

    [Fact]
    public void Parse_MessageEndCollectsRetrieverResourcesWithoutRawProviderPayload()
    {
        var payload = """
            data: {"event":"message_end","message_id":"msg-123","conversation_id":"conv-456","metadata":{"retriever_resources":[{"position":1,"dataset_id":"dataset-1","dataset_name":"Requirements knowledge base","document_id":"document-1","document_name":"Storage cell requirement","segment_id":"segment-1","score":0.875,"content":"Storage cell code must be non-empty."}]}}
            """;

        var result = DifyAgentSseStreamParser.Parse(payload);

        Assert.True(result.IsComplete);
        Assert.NotNull(result.MessageEndMetadata);
        Assert.True(result.MessageEndMetadata.HasRetrieverResourcesMetadata);
        var resource = Assert.Single(result.MessageEndMetadata.RetrieverResources);
        Assert.Equal("Storage cell requirement", resource.SourceTitle);
        Assert.Equal("document-1", resource.SourceId);
        Assert.Equal("dataset-1", resource.ExternalReference);
        Assert.Equal("segment-1", resource.FragmentId);
        Assert.Equal("Storage cell code must be non-empty.", resource.Excerpt);
        Assert.Equal(1, resource.Rank);
        Assert.Equal(0.875, resource.Score);
    }

    [Fact]
    public void Parse_MessageEndMalformedRetrieverResourcesReturnsWarningAndKeepsUsableMetadata()
    {
        var payload = """
            data: {"event":"message_end","message_id":"msg-123","conversation_id":"conv-456","metadata":{"usage":{"total_tokens":17},"retriever_resources":{"not":"an array"}}}
            """;

        var result = DifyAgentSseStreamParser.Parse(payload);

        Assert.True(result.IsComplete);
        Assert.NotNull(result.MessageEndMetadata);
        Assert.True(result.MessageEndMetadata.HasRetrieverResourcesMetadata);
        Assert.Empty(result.MessageEndMetadata.RetrieverResources);
        Assert.Equal("17", result.MessageEndMetadata.Usage["total_tokens"]);
        Assert.Contains(
            "Dify message_end retriever_resources metadata was not an array and was ignored.",
            result.Warnings);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"provider_private_field":"ignored"}""")]
    public void Parse_MessageEndIgnoresRetrieverResourcesWithoutRecognizedMappableFields(
        string retrieverResourceJson)
    {
        var payload =
            "data: {\"event\":\"message_end\",\"message_id\":\"msg-123\",\"conversation_id\":\"conv-456\"," +
            $"\"metadata\":{{\"retriever_resources\":[{retrieverResourceJson}]}}}}";

        var result = DifyAgentSseStreamParser.Parse(payload);

        Assert.True(result.IsComplete);
        Assert.NotNull(result.MessageEndMetadata);
        Assert.True(result.MessageEndMetadata.HasRetrieverResourcesMetadata);
        Assert.Equal(1, result.MessageEndMetadata.RetrieverResourceMetadataItemCount);
        Assert.Empty(result.MessageEndMetadata.RetrieverResources);
        Assert.Contains(
            "Dify message_end retriever_resources contained an empty or unrecognized item that was ignored.",
            result.Warnings);
    }

    [Fact]
    public void Parse_IgnoresUnknownEventsWithoutLosingUsableAnswer()
    {
        var payload = """
            data: {"event":"agent_message","answer":"Known answer"}
            data: {"event":"custom_dify_event","value":"ignored provider detail"}
            data: {"event":"message_end","message_id":"msg-123","conversation_id":"conv-456","metadata":{"usage":{"total_tokens":3}}}
            """;

        var result = DifyAgentSseStreamParser.Parse(payload);

        Assert.True(result.IsComplete);
        Assert.Equal("Known answer", result.Answer);
        Assert.Equal(1, result.UnknownEventCount);
        Assert.Contains(
            "Dify SSE stream contained an unsupported event that was ignored.",
            result.Warnings);
    }

    [Fact]
    public void Parse_MalformedJsonReturnsWarningAndKeepsFollowingEvents()
    {
        var payload = """
            data: {"event":"agent_message","answer":"Hello "}
            data: {"event":"agent_message","answer":
            data: {"event":"agent_message","answer":"world"}
            data: {"event":"message_end","message_id":"msg-123","conversation_id":"conv-456"}
            """;

        var result = DifyAgentSseStreamParser.Parse(payload);

        Assert.True(result.IsComplete);
        Assert.True(result.HasMalformedEvents);
        Assert.Equal("Hello world", result.Answer);
        Assert.Contains("Dify SSE data event could not be parsed as JSON.", result.Warnings);
    }

    [Fact]
    public void Parse_PartialStreamReturnsCollectedAnswerAndIncompleteWarning()
    {
        var payload = """
            data: {"event":"agent_message","answer":"Partial "}
            data: {"event":"agent_message","answer":"answer"}
            """;

        var result = DifyAgentSseStreamParser.Parse(payload);

        Assert.False(result.IsComplete);
        Assert.False(result.HasMalformedEvents);
        Assert.Equal("Partial answer", result.Answer);
        Assert.Null(result.MessageEndMetadata);
        Assert.Contains("Dify SSE stream ended before message_end event.", result.Warnings);
    }

    [Fact]
    public void Parse_DoesNotExposeAgentThoughtPayload()
    {
        const string hiddenThought = "Provider-only thought must stay inside the parser boundary.";
        var payload = $$"""
            data: {"event":"agent_thought","thought":"{{hiddenThought}}"}
            data: {"event":"message_end","message_id":"msg-123","conversation_id":"conv-456"}
            """;

        var result = DifyAgentSseStreamParser.Parse(payload);

        Assert.True(result.IsComplete);
        Assert.Equal(1, result.AgentThoughtEventCount);
        Assert.DoesNotContain(hiddenThought, JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_MessageEndKeepsOnlySafeScalarUsageMetadata()
    {
        var payload = """
            data: {"event":"message_end","message_id":"msg-123","conversation_id":"conv-456","metadata":{"usage":{"total_tokens":17,"total_price":"0.00042","currency":"USD","latency":0.42,"nested":{"value":"ignored"}}}}
            """;

        var result = DifyAgentSseStreamParser.Parse(payload);

        Assert.True(result.IsComplete);
        Assert.NotNull(result.MessageEndMetadata);
        Assert.Equal("17", result.MessageEndMetadata.Usage["total_tokens"]);
        Assert.Equal("0.00042", result.MessageEndMetadata.Usage["total_price"]);
        Assert.Equal("USD", result.MessageEndMetadata.Usage["currency"]);
        Assert.Equal("0.42", result.MessageEndMetadata.Usage["latency"]);
        Assert.False(result.MessageEndMetadata.Usage.ContainsKey("nested"));
    }

    [Fact]
    public void Parse_MessageEndBlocksRawPayloadLikeUsageMetadata()
    {
        const string rawPayload = "raw provider payload should not be retained";
        var payload =
            "data: {\"event\":\"message_end\",\"message_id\":\"msg-123\",\"conversation_id\":\"conv-456\"," +
            "\"metadata\":{\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":5,\"total_tokens\":17," +
            $"\"raw_payload\":\"{rawPayload}\",\"raw_response\":\"raw response\"," +
            "\"provider_payload\":\"provider payload\",\"request_body\":\"request body\"," +
            "\"response_content\":\"response content\"}}}";

        var result = DifyAgentSseStreamParser.Parse(payload);

        Assert.True(result.IsComplete);
        Assert.NotNull(result.MessageEndMetadata);
        Assert.Equal("12", result.MessageEndMetadata.Usage["prompt_tokens"]);
        Assert.Equal("5", result.MessageEndMetadata.Usage["completion_tokens"]);
        Assert.Equal("17", result.MessageEndMetadata.Usage["total_tokens"]);
        Assert.False(result.MessageEndMetadata.Usage.ContainsKey("raw_payload"));
        Assert.False(result.MessageEndMetadata.Usage.ContainsKey("raw_response"));
        Assert.False(result.MessageEndMetadata.Usage.ContainsKey("provider_payload"));
        Assert.False(result.MessageEndMetadata.Usage.ContainsKey("request_body"));
        Assert.False(result.MessageEndMetadata.Usage.ContainsKey("response_content"));
        Assert.Contains(
            "Dify message_end usage metadata contained an unsupported key that was ignored.",
            result.Warnings);
        Assert.DoesNotContain(rawPayload, JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }
}

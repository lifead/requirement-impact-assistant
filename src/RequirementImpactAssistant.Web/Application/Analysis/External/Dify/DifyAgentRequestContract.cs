using System.Text.Json;
using System.Text.Json.Serialization;

namespace RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

internal static class DifyAgentRequestContract
{
    public const string ChatMessagesPath = "/v1/chat-messages";
    public const string StreamingResponseMode = "streaming";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Uri? NormalizeChatMessagesEndpoint(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return new UriBuilder(endpointUri)
        {
            Path = ChatMessagesPath.TrimStart('/'),
            Query = string.Empty,
            Fragment = string.Empty
        }.Uri;
    }

    public static DifyAgentChatMessagesRequestDto CreateRequest(ExternalRagAdapterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var analysis = request.InputSnapshot.Analysis;

        return new DifyAgentChatMessagesRequestDto
        {
            Inputs = new DifyAgentInputsDto
            {
                OriginalRequirement = analysis.OriginalDescription,
                Situation = analysis.SituationDescription,
                Source = analysis.ChangeSource
            },
            Query = analysis.ProjectRequest,
            ResponseMode = StreamingResponseMode,
            ConversationId = string.Empty,
            User = $"analysis-{request.CorrelationId:N}"
        };
    }

    public static string SerializeRequest(ExternalRagAdapterRequest request) =>
        JsonSerializer.Serialize(CreateRequest(request), JsonOptions);
}

internal sealed class DifyAgentChatMessagesRequestDto
{
    [JsonPropertyName("inputs")]
    public DifyAgentInputsDto Inputs { get; init; } = new();

    [JsonPropertyName("query")]
    public string Query { get; init; } = string.Empty;

    [JsonPropertyName("response_mode")]
    public string ResponseMode { get; init; } = DifyAgentRequestContract.StreamingResponseMode;

    [JsonPropertyName("conversation_id")]
    public string ConversationId { get; init; } = string.Empty;

    [JsonPropertyName("user")]
    public string User { get; init; } = string.Empty;
}

internal sealed class DifyAgentInputsDto
{
    [JsonPropertyName("originalRequirement")]
    public string OriginalRequirement { get; init; } = string.Empty;

    [JsonPropertyName("situation")]
    public string Situation { get; init; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;
}

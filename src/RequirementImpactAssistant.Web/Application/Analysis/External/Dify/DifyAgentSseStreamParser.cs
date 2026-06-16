using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

internal static class DifyAgentSseStreamParser
{
    private const string AgentMessageEvent = "agent_message";
    private const string AgentThoughtEvent = "agent_thought";
    private const string MessageEndEvent = "message_end";
    private const int MaxSafeMetadataValueLength = 200;
    private const string UnsupportedUsageMetadataWarning =
        "Dify message_end usage metadata contained an unsupported key that was ignored.";

    private static readonly HashSet<string> AllowedUsageMetadataKeys = new(StringComparer.Ordinal)
    {
        "completion_price",
        "completion_price_unit",
        "completion_tokens",
        "completion_unit_price",
        "currency",
        "latency",
        "prompt_price",
        "prompt_price_unit",
        "prompt_tokens",
        "prompt_unit_price",
        "total_price",
        "total_tokens"
    };

    private static readonly Regex SensitiveDiagnosticPattern = new(
        @"(authorization\s*:|bearer\s+\S+|\bapi[-_\s]*key\b|\b(?:access|refresh|auth|api)?[-_\s]*token\b|\bcookie\b|\bcsrf\b|\bsecret\b|https?://\S+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static DifyAgentSseParseResult Parse(string? ssePayload)
    {
        var answerFragments = new List<string>();
        var warnings = new List<string>();
        var hasMalformedEvents = false;
        var isComplete = false;
        var agentThoughtEventCount = 0;
        var unknownEventCount = 0;
        DifyAgentMessageEndMetadata? messageEndMetadata = null;

        using var reader = new StringReader(ssePayload ?? string.Empty);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].TrimStart();
            if (string.IsNullOrWhiteSpace(data) || string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var document = TryParseDataEvent(data, warnings, ref hasMalformedEvents);
            if (document is null)
            {
                continue;
            }

            var root = document.RootElement;
            var eventName = GetStringProperty(root, "event");
            switch (eventName)
            {
                case AgentMessageEvent:
                    AddAnswerFragment(root, answerFragments, warnings);
                    break;
                case AgentThoughtEvent:
                    agentThoughtEventCount++;
                    break;
                case MessageEndEvent:
                    if (isComplete)
                    {
                        warnings.Add("Dify SSE stream contained more than one message_end event; the last safe metadata was used.");
                    }

                    isComplete = true;
                    messageEndMetadata = CreateMessageEndMetadata(root, warnings);
                    break;
                default:
                    unknownEventCount++;
                    warnings.Add("Dify SSE stream contained an unsupported event that was ignored.");
                    break;
            }
        }

        if (!isComplete)
        {
            warnings.Add("Dify SSE stream ended before message_end event.");
        }

        return new DifyAgentSseParseResult(
            Answer: string.Concat(answerFragments),
            AnswerFragments: answerFragments.ToArray(),
            MessageEndMetadata: messageEndMetadata,
            IsComplete: isComplete,
            HasMalformedEvents: hasMalformedEvents,
            AgentThoughtEventCount: agentThoughtEventCount,
            UnknownEventCount: unknownEventCount,
            Warnings: warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static JsonDocument? TryParseDataEvent(
        string data,
        List<string> warnings,
        ref bool hasMalformedEvents)
    {
        try
        {
            return JsonDocument.Parse(data);
        }
        catch (JsonException)
        {
            hasMalformedEvents = true;
            warnings.Add("Dify SSE data event could not be parsed as JSON.");
            return null;
        }
    }

    private static void AddAnswerFragment(
        JsonElement root,
        List<string> answerFragments,
        List<string> warnings)
    {
        var answer = GetStringProperty(root, "answer");
        if (answer is null)
        {
            warnings.Add("Dify agent_message event did not contain an answer fragment.");
            return;
        }

        if (answer.Length > 0)
        {
            answerFragments.Add(answer);
        }
    }

    private static DifyAgentMessageEndMetadata CreateMessageEndMetadata(
        JsonElement root,
        List<string> warnings)
    {
        var messageId = CreateSafeMetadataValue(GetStringProperty(root, "message_id"), warnings);
        var conversationId = CreateSafeMetadataValue(GetStringProperty(root, "conversation_id"), warnings);
        var usage = CreateSafeUsage(root, warnings);

        return new DifyAgentMessageEndMetadata(
            MessageId: messageId,
            ConversationId: conversationId,
            Usage: usage);
    }

    private static IReadOnlyDictionary<string, string> CreateSafeUsage(
        JsonElement root,
        List<string> warnings)
    {
        if (!root.TryGetProperty("metadata", out var metadata) ||
            metadata.ValueKind != JsonValueKind.Object ||
            !metadata.TryGetProperty("usage", out var usage) ||
            usage.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>();
        }

        var safeUsage = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var usageProperty in usage.EnumerateObject())
        {
            if (!TryCreateSafeUsageValue(usageProperty, warnings, out var value))
            {
                continue;
            }

            safeUsage[usageProperty.Name] = value;
        }

        return safeUsage;
    }

    private static bool TryCreateSafeUsageValue(
        JsonProperty usageProperty,
        List<string> warnings,
        out string value)
    {
        value = string.Empty;
        if (!IsSafeMetadataKey(usageProperty.Name))
        {
            warnings.Add("Dify message_end usage metadata contained an unsafe key that was ignored.");
            return false;
        }

        if (!AllowedUsageMetadataKeys.Contains(usageProperty.Name))
        {
            warnings.Add(UnsupportedUsageMetadataWarning);
            return false;
        }

        var rawValue = usageProperty.Value.ValueKind switch
        {
            JsonValueKind.String => usageProperty.Value.GetString(),
            JsonValueKind.Number => usageProperty.Value.GetRawText(),
            JsonValueKind.True => bool.TrueString.ToLower(CultureInfo.InvariantCulture),
            JsonValueKind.False => bool.FalseString.ToLower(CultureInfo.InvariantCulture),
            _ => null
        };

        var safeValue = CreateSafeMetadataValue(rawValue, warnings);
        if (safeValue is null)
        {
            return false;
        }

        value = safeValue;
        return true;
    }

    private static bool IsSafeMetadataKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || SensitiveDiagnosticPattern.IsMatch(key))
        {
            return false;
        }

        return key.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '_' or '-' or '.');
    }

    private static string? CreateSafeMetadataValue(string? value, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();
        if (SensitiveDiagnosticPattern.IsMatch(normalizedValue))
        {
            warnings.Add("Dify message_end metadata contained unsafe diagnostic content that was ignored.");
            return null;
        }

        return normalizedValue.Length <= MaxSafeMetadataValueLength
            ? normalizedValue
            : normalizedValue[..MaxSafeMetadataValueLength];
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }
}

internal sealed record DifyAgentSseParseResult(
    string Answer,
    IReadOnlyList<string> AnswerFragments,
    DifyAgentMessageEndMetadata? MessageEndMetadata,
    bool IsComplete,
    bool HasMalformedEvents,
    int AgentThoughtEventCount,
    int UnknownEventCount,
    IReadOnlyList<string> Warnings);

internal sealed record DifyAgentMessageEndMetadata(
    string? MessageId,
    string? ConversationId,
    IReadOnlyDictionary<string, string> Usage);

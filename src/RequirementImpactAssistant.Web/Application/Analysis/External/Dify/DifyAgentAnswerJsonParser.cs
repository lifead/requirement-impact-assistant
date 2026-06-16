using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

internal static class DifyAgentAnswerJsonParser
{
    private const int MaxSanitizedRawTextLength = 4000;
    private const string SensitiveKeyPattern =
        @"(?:apiKey|api[-_\s]*key|key|accessToken|refreshToken|authToken|auth|token|password|pass[-_\s]*word|secret|cookie|csrf|sessionId|session[-_\s]*id|session)";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ExpectedCollectionPropertyNames =
    [
        "affectedRequirements",
        "affectedTasks",
        "affectedProjectDecisions",
        "affectedApiInterfacesDocumentsTests",
        "affectedArchitecturalConstraints",
        "affectedOrganizationalContextItems",
        "contradictions",
        "missingInformation",
        "clarificationQuestions",
        "risks",
        "optionsForExpertReview",
        "usedSources",
        "warnings"
    ];
    private static readonly Regex BearerTokenPattern = new(
        @"\bbearer\s+[A-Za-z0-9._~+\-/]+=*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex AuthorizationHeaderPattern = new(
        @"\bauthorization\s*[:=]\s*[^\s,;}\]]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex JsonSecretPropertyPattern = new(
        @"([""']?" + SensitiveKeyPattern + @"[""']?\s*:\s*)(?:""[^""]*""|'[^']*'|[^,\s;}\]]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex AssignmentSecretPattern = new(
        @"\b" + SensitiveKeyPattern + @"\b\s*=\s*[^\s,;}\]]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex ColonSecretPattern = new(
        @"\b" + SensitiveKeyPattern + @"\b\s*:\s*[^\s,;}\]]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex UrlPattern = new(
        @"https?://\S+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly Regex SchemeLessUrlPattern = new(
        @"(?<![@\w.-])(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,}(?::\d{2,5})?(?:/[^\s,;}\]]*)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    public static DifyAgentAnswerParseResult Parse(string? answerText)
    {
        if (string.IsNullOrWhiteSpace(answerText))
        {
            return new DifyAgentAnswerParseResult(
                StructuredAnswer: null,
                SanitizedRawText: null,
                ParseMode: DifyAgentAnswerParseMode.None,
                Warnings: ["Dify Agent answer was empty and could not be parsed as JSON."]);
        }

        var normalizedAnswer = answerText.Trim();
        if (TryParseStructuredAnswer(normalizedAnswer, out var structuredAnswer, out var invalidShapeWarning))
        {
            return new DifyAgentAnswerParseResult(
                StructuredAnswer: structuredAnswer,
                SanitizedRawText: null,
                ParseMode: DifyAgentAnswerParseMode.FullAnswerJson,
                Warnings: []);
        }

        var fallbackJson = ExtractObjectSubstring(normalizedAnswer);
        string? fallbackInvalidShapeWarning = null;
        if (fallbackJson is not null &&
            TryParseStructuredAnswer(fallbackJson, out structuredAnswer, out fallbackInvalidShapeWarning))
        {
            return new DifyAgentAnswerParseResult(
                StructuredAnswer: structuredAnswer,
                SanitizedRawText: null,
                ParseMode: DifyAgentAnswerParseMode.JsonSubstringFallback,
                Warnings: ["Dify Agent answer contained non-JSON wrapper text; JSON substring fallback was used."]);
        }

        var warnings = new List<string>
        {
            "Dify Agent answer could not be parsed as JSON; sanitized raw answer text was retained as fallback."
        };
        if (invalidShapeWarning is not null || fallbackInvalidShapeWarning is not null)
        {
            warnings.Add(invalidShapeWarning ?? fallbackInvalidShapeWarning!);
        }

        if (fallbackJson is null)
        {
            warnings.Add("Dify Agent answer did not contain a complete JSON object boundary.");
        }

        return new DifyAgentAnswerParseResult(
            StructuredAnswer: null,
            SanitizedRawText: SanitizeRawText(normalizedAnswer, warnings),
            ParseMode: DifyAgentAnswerParseMode.RawTextFallback,
            Warnings: warnings.ToArray());
    }

    private static bool TryParseStructuredAnswer(
        string answerText,
        out DifyAgentAnswerDto? structuredAnswer,
        out string? invalidShapeWarning)
    {
        structuredAnswer = null;
        invalidShapeWarning = null;

        try
        {
            using var document = JsonDocument.Parse(answerText);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            structuredAnswer = JsonSerializer.Deserialize<DifyAgentAnswerDto>(answerText, JsonOptions);
            if (structuredAnswer is null)
            {
                return false;
            }

            if (!HasExpectedStructuredShape(document.RootElement))
            {
                structuredAnswer = null;
                invalidShapeWarning =
                    "Dify Agent answer JSON did not match the expected structured answer shape; sanitized raw answer text was retained as fallback.";
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasExpectedStructuredShape(JsonElement root)
    {
        if (!HasStringProperty(root, "changeSummary") ||
            !HasStringProperty(root, "preliminaryAssessment"))
        {
            return false;
        }

        foreach (var collectionPropertyName in ExpectedCollectionPropertyNames)
        {
            if (!root.TryGetProperty(collectionPropertyName, out var property) ||
                property.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasStringProperty(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(property.GetString());

    private static string? ExtractObjectSubstring(string answerText)
    {
        var start = answerText.IndexOf('{', StringComparison.Ordinal);
        var end = answerText.LastIndexOf('}');

        return start >= 0 && end > start
            ? answerText[start..(end + 1)]
            : null;
    }

    private static string SanitizeRawText(string rawText, List<string> warnings)
    {
        var sanitized = BearerTokenPattern.Replace(rawText, "Bearer [REDACTED]");
        sanitized = AuthorizationHeaderPattern.Replace(sanitized, "[REDACTED_AUTH_HEADER]");
        sanitized = JsonSecretPropertyPattern.Replace(sanitized, "$1\"[REDACTED]\"");
        sanitized = AssignmentSecretPattern.Replace(sanitized, match =>
        {
            var separatorIndex = match.Value.IndexOf('=', StringComparison.Ordinal);
            return separatorIndex >= 0
                ? string.Concat(match.Value.AsSpan(0, separatorIndex + 1), "[REDACTED]")
                : "[REDACTED]";
        });
        sanitized = ColonSecretPattern.Replace(sanitized, match =>
        {
            var separatorIndex = match.Value.IndexOf(':', StringComparison.Ordinal);
            return separatorIndex >= 0
                ? string.Concat(match.Value.AsSpan(0, separatorIndex + 1), " [REDACTED]")
                : "[REDACTED]";
        });
        sanitized = UrlPattern.Replace(sanitized, "[REDACTED_URL]");
        sanitized = SchemeLessUrlPattern.Replace(sanitized, "[REDACTED_URL]");

        if (sanitized.Length <= MaxSanitizedRawTextLength)
        {
            return sanitized;
        }

        warnings.Add("Dify Agent raw answer fallback was truncated for diagnostics.");
        return sanitized[..MaxSanitizedRawTextLength];
    }
}

internal sealed record DifyAgentAnswerParseResult(
    DifyAgentAnswerDto? StructuredAnswer,
    string? SanitizedRawText,
    DifyAgentAnswerParseMode ParseMode,
    IReadOnlyList<string> Warnings)
{
    public bool HasStructuredJson => StructuredAnswer is not null;
}

internal enum DifyAgentAnswerParseMode
{
    None,
    FullAnswerJson,
    JsonSubstringFallback,
    RawTextFallback
}

internal sealed class DifyAgentAnswerDto
{
    [JsonPropertyName("changeSummary")]
    public string? ChangeSummary { get; init; }

    [JsonPropertyName("affectedRequirements")]
    public IReadOnlyList<JsonElement> AffectedRequirements { get; init; } = [];

    [JsonPropertyName("affectedTasks")]
    public IReadOnlyList<JsonElement> AffectedTasks { get; init; } = [];

    [JsonPropertyName("affectedProjectDecisions")]
    public IReadOnlyList<JsonElement> AffectedProjectDecisions { get; init; } = [];

    [JsonPropertyName("affectedApiInterfacesDocumentsTests")]
    public IReadOnlyList<JsonElement> AffectedApiInterfacesDocumentsTests { get; init; } = [];

    [JsonPropertyName("affectedArchitecturalConstraints")]
    public IReadOnlyList<JsonElement> AffectedArchitecturalConstraints { get; init; } = [];

    [JsonPropertyName("affectedOrganizationalContextItems")]
    public IReadOnlyList<JsonElement> AffectedOrganizationalContextItems { get; init; } = [];

    [JsonPropertyName("contradictions")]
    public IReadOnlyList<JsonElement> Contradictions { get; init; } = [];

    [JsonPropertyName("missingInformation")]
    public IReadOnlyList<JsonElement> MissingInformation { get; init; } = [];

    [JsonPropertyName("clarificationQuestions")]
    public IReadOnlyList<JsonElement> ClarificationQuestions { get; init; } = [];

    [JsonPropertyName("risks")]
    public IReadOnlyList<JsonElement> Risks { get; init; } = [];

    [JsonPropertyName("optionsForExpertReview")]
    public IReadOnlyList<JsonElement> OptionsForExpertReview { get; init; } = [];

    [JsonPropertyName("preliminaryAssessment")]
    public string? PreliminaryAssessment { get; init; }

    [JsonPropertyName("usedSources")]
    public IReadOnlyList<JsonElement> UsedSources { get; init; } = [];

    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

using System.Text.Json;
using RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class DifyAgentAnswerJsonParserTests
{
    [Fact]
    public void Parse_WhenAnswerIsValidJson_ReturnsStructuredAnswer()
    {
        var answer = """
            {
              "changeSummary": "Storage cell validation",
              "affectedRequirements": [],
              "affectedTasks": [],
              "affectedProjectDecisions": [],
              "affectedApiInterfacesDocumentsTests": [],
              "affectedArchitecturalConstraints": [],
              "affectedOrganizationalContextItems": [],
              "contradictions": [],
              "missingInformation": [],
              "clarificationQuestions": [],
              "risks": [],
              "optionsForExpertReview": [],
              "preliminaryAssessment": "Needs expert review",
              "usedSources": [],
              "warnings": []
            }
            """;

        var result = DifyAgentAnswerJsonParser.Parse(answer);

        Assert.True(result.HasStructuredJson);
        Assert.Equal(DifyAgentAnswerParseMode.FullAnswerJson, result.ParseMode);
        Assert.NotNull(result.StructuredAnswer);
        Assert.Equal("Storage cell validation", result.StructuredAnswer.ChangeSummary);
        Assert.Equal("Needs expert review", result.StructuredAnswer.PreliminaryAssessment);
        Assert.Null(result.SanitizedRawText);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Parse_WhenAnswerWrapsJson_UsesJsonSubstringFallback()
    {
        var answer = """
            Agent response:
            {
              "changeSummary": "Wrapped JSON",
              "affectedRequirements": [],
              "preliminaryAssessment": "Usable after fallback",
              "warnings": []
            }
            End.
            """;

        var result = DifyAgentAnswerJsonParser.Parse(answer);

        Assert.True(result.HasStructuredJson);
        Assert.Equal(DifyAgentAnswerParseMode.JsonSubstringFallback, result.ParseMode);
        Assert.NotNull(result.StructuredAnswer);
        Assert.Equal("Wrapped JSON", result.StructuredAnswer.ChangeSummary);
        Assert.Null(result.SanitizedRawText);
        Assert.Contains(
            "Dify Agent answer contained non-JSON wrapper text; JSON substring fallback was used.",
            result.Warnings);
    }

    [Fact]
    public void Parse_WhenAnswerIsInvalidJson_ReturnsSanitizedRawTextFallback()
    {
        const string answer = "Preliminary analysis text without JSON.";

        var result = DifyAgentAnswerJsonParser.Parse(answer);

        Assert.False(result.HasStructuredJson);
        Assert.Equal(DifyAgentAnswerParseMode.RawTextFallback, result.ParseMode);
        Assert.Null(result.StructuredAnswer);
        Assert.Equal(answer, result.SanitizedRawText);
        Assert.Contains(
            "Dify Agent answer could not be parsed as JSON; sanitized raw answer text was retained as fallback.",
            result.Warnings);
        Assert.Contains(
            "Dify Agent answer did not contain a complete JSON object boundary.",
            result.Warnings);
    }

    [Fact]
    public void Parse_WhenInvalidAnswerContainsSensitiveLookingFragments_RedactsRawText()
    {
        const string answer =
            "Use apiKey=synthetic-key, Authorization: Bearer synthetic-token, cookie=session-value, " +
            "csrf=csrf-value, sessionId=session-id, auth=auth-assignment, auth: auth-colon, " +
            "session=session-assignment, password=password-assignment, password: password-colon, " +
            "https://dify.invalid/v1/chat-messages and dify.invalid/private";

        var result = DifyAgentAnswerJsonParser.Parse(answer);

        Assert.False(result.HasStructuredJson);
        Assert.Equal(DifyAgentAnswerParseMode.RawTextFallback, result.ParseMode);
        Assert.NotNull(result.SanitizedRawText);
        Assert.Contains("[REDACTED]", result.SanitizedRawText);
        Assert.Contains("[REDACTED_URL]", result.SanitizedRawText);

        var serializedResult = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("synthetic-key", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-token", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("session-value", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("csrf-value", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("session-id", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-assignment", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-colon", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("session-assignment", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("password-assignment", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("password-colon", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("https://dify.invalid", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("dify.invalid/private", serializedResult, StringComparison.Ordinal);
    }
}

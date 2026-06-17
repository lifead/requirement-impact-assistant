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
        var answer = $"Agent response:{Environment.NewLine}{CreateStructuredAnswer("Wrapped JSON", "Usable after fallback")}{Environment.NewLine}End.";

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

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"answer\":\"not the expected structured shape\"}")]
    public void Parse_WhenJsonObjectDoesNotMatchExpectedShape_ReturnsSanitizedRawTextFallback(string answer)
    {
        var result = DifyAgentAnswerJsonParser.Parse(answer);

        Assert.False(result.HasStructuredJson);
        Assert.Equal(DifyAgentAnswerParseMode.RawTextFallback, result.ParseMode);
        Assert.Null(result.StructuredAnswer);
        Assert.Equal(answer, result.SanitizedRawText);
        Assert.Contains(
            "Dify Agent answer JSON did not match the expected structured answer shape; sanitized raw answer text was retained as fallback.",
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
            "key=generic-key-assignment, key: generic-key-colon, " +
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
        Assert.DoesNotContain("generic-key-assignment", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("generic-key-colon", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("session-assignment", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("password-assignment", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("password-colon", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("https://dify.invalid", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("dify.invalid/private", serializedResult, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_WhenJsonLikeRawFallbackContainsSecretProperties_RedactsRawText()
    {
        const string answer = """
            {
              "answer": "not expected shape",
              "apiKey": "synthetic-json-key",
              "password": "synthetic-json-password",
              "cookie": "synthetic-json-cookie",
              "csrf": "synthetic-json-csrf",
              "serviceUrl": "https://dify.invalid/private"
            }
            """;

        var result = DifyAgentAnswerJsonParser.Parse(answer);

        Assert.False(result.HasStructuredJson);
        Assert.Equal(DifyAgentAnswerParseMode.RawTextFallback, result.ParseMode);
        Assert.NotNull(result.SanitizedRawText);
        Assert.Contains("[REDACTED]", result.SanitizedRawText);
        Assert.Contains("[REDACTED_URL]", result.SanitizedRawText);

        var serializedResult = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("synthetic-json-key", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-json-password", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-json-cookie", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-json-csrf", serializedResult, StringComparison.Ordinal);
        Assert.DoesNotContain("https://dify.invalid/private", serializedResult, StringComparison.Ordinal);
    }

    private static string CreateStructuredAnswer(string changeSummary, string preliminaryAssessment) =>
        $$"""
          {
            "changeSummary": "{{changeSummary}}",
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
            "preliminaryAssessment": "{{preliminaryAssessment}}",
            "usedSources": [],
            "warnings": []
          }
          """;
}

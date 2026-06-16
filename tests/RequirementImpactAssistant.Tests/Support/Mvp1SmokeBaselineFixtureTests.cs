using System.Collections;
using System.Reflection;
using System.Text.Json;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Tests.Support;

public sealed class Mvp1SmokeBaselineFixtureTests
{
    private static readonly JsonSerializerOptions StableJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

    [Fact]
    public void Create_ReturnsDeterministicStableBaseline()
    {
        var first = Mvp1SmokeBaselineFixture.Create();
        var second = Mvp1SmokeBaselineFixture.Create();

        Assert.Equal(
            JsonSerializer.Serialize(first, StableJsonOptions),
            JsonSerializer.Serialize(second, StableJsonOptions));

        Assert.Equal(Mvp1SmokeBaselineFixture.AnalysisId, first.Analysis.Id);
        Assert.Equal(Mvp1SmokeBaselineFixture.CreatedAt, first.Analysis.CreatedAt);
        Assert.Equal(Mvp1SmokeBaselineFixture.UpdatedAt, first.Analysis.UpdatedAt);
        Assert.Equal("local demo request - example integration boundary", first.Analysis.Title);
        Assert.Contains("example integration boundary", first.Analysis.ProjectRequest);
    }

    [Fact]
    public void Create_KeepsManualContextAndRetrievedContextSeparate()
    {
        var baseline = Mvp1SmokeBaselineFixture.Create();
        var manualContextIds = baseline.ManualContextFragments
            .Select(fragment => fragment.Id.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(
            baseline.ManualContextFragments,
            fragment => Assert.IsType<ContextFragment>(fragment));
        Assert.All(
            baseline.ExternalHappyPathResponse.RetrievedContextItems,
            item => Assert.IsType<RetrievedContextItem>(item));
        Assert.NotNull(baseline.ExternalHappyPathRequest.ManualContext);
        Assert.Equal(
            baseline.ManualContextFragments.Select(fragment => fragment.Id),
            baseline.ExternalHappyPathRequest.ManualContext.ContextFragments.Select(fragment => fragment.Id));
        Assert.DoesNotContain(
            baseline.ExternalHappyPathResponse.RetrievedContextItems,
            item => item.FragmentId is not null && manualContextIds.Contains(item.FragmentId));
        Assert.All(
            baseline.ExternalHappyPathResponse.RetrievedContextItems,
            item => Assert.DoesNotContain("Manual context:", item.Text ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void Create_IncludesExpectedExpertEvaluationAndConclusionFields()
    {
        var baseline = Mvp1SmokeBaselineFixture.Create();
        var evaluation = baseline.ExpectedExpertEvaluation;
        var conclusion = baseline.ExpectedExpertConclusion;

        Assert.Equal(Mvp1SmokeBaselineFixture.AnalysisId, evaluation.AnalysisId);
        Assert.NotEqual(ContextSufficiencyRating.NotAssessed, evaluation.ContextSufficiency);
        Assert.NotEqual(ResultUsefulnessRating.NotAssessed, evaluation.ResultUsefulness);
        Assert.False(string.IsNullOrWhiteSpace(evaluation.GeneralComment));
        Assert.NotEmpty(evaluation.EvaluatedItems);
        Assert.NotEmpty(evaluation.MissedItems);
        Assert.NotEmpty(evaluation.Corrections);

        Assert.Equal(Mvp1SmokeBaselineFixture.AnalysisId, conclusion.AnalysisId);
        Assert.NotEqual(ExpertConclusionType.NotSet, conclusion.ConclusionType);
        Assert.False(string.IsNullOrWhiteSpace(conclusion.Comment));
        Assert.False(string.IsNullOrWhiteSpace(conclusion.Rationale));
        Assert.Equal(Mvp1SmokeBaselineFixture.ExpertFixedAt, conclusion.FixedAt);
    }

    [Fact]
    public void Create_IncludesImpactMapContentForLaterSmokeAssertions()
    {
        var impactMap = Mvp1SmokeBaselineFixture.Create().ExpectedImpactMap;

        Assert.Equal("Example integration boundary change", impactMap.ChangeSummary.Title);
        Assert.Equal(ImpactSeverity.Medium, impactMap.ChangeSummary.Severity);
        Assert.Single(impactMap.AffectedRequirements);
        Assert.Single(impactMap.AffectedProjectDecisions);
        Assert.Single(impactMap.AffectedApiInterfacesDocumentsTests);
        Assert.Single(impactMap.MissingInformation);
        Assert.Single(impactMap.ClarificationQuestions);
        Assert.Single(impactMap.Risks);
        Assert.Single(impactMap.OptionsForExpertReview);
        Assert.Equal("Requires expert review", impactMap.PreliminaryAssessment.Title);
        Assert.Contains(
            Mvp1SmokeBaselineFixture.ManualRequirementContextFragmentId,
            impactMap.AffectedRequirements.Single().RelatedContextFragmentIds);
    }

    [Fact]
    public void Create_UsesNeutralAnonymizedDataWithoutSensitiveTokensOrRealEndpoints()
    {
        var baseline = Mvp1SmokeBaselineFixture.Create();
        var allText = string.Join(
            "\n",
            ExtractStrings(baseline).Where(value => !string.IsNullOrWhiteSpace(value)));

        string[] forbiddenFragments =
        [
            "http://",
            "https://",
            "www.",
            ".com",
            "localhost",
            "127.0.0.1",
            "api_key",
            "apikey",
            "access_token",
            "bearer ",
            "password",
            "secret",
            "customer",
            "corp",
            "inc.",
            " llc",
            " ltd",
            "jira",
            "confluence",
            "deepseek",
            "dify"
        ];

        foreach (var forbidden in forbiddenFragments)
        {
            Assert.DoesNotContain(forbidden, allText, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("local demo request", allText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example integration boundary", allText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("demo requirement catalogue", allText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_ExternalBaselineHasRetrievedContextAvailableHappyPath()
    {
        var response = Mvp1SmokeBaselineFixture.Create().ExternalHappyPathResponse;

        Assert.Equal(ExternalRagAdapterResponseStatus.Completed, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Equal(RetrievedContextState.Available, response.RetrievedContextState);
        Assert.Empty(response.Warnings);
        Assert.Empty(response.Errors);
        Assert.Equal("LocalMockKnowledgeSource", response.Metadata.ProviderName);
        Assert.Equal("LocalSmokeFixtureAdapter", response.Metadata.AdapterName);
        Assert.Equal("happy-path", response.Metadata.ProfileName);
        Assert.Equal("disabled", response.Metadata.SanitizedProperties["network"]);
        Assert.All(
            response.RetrievedContextItems,
            item =>
            {
                Assert.Equal(RetrievedContextItemCompleteness.FullText, item.Completeness);
                Assert.False(string.IsNullOrWhiteSpace(item.Text));
                Assert.StartsWith("local-reference:", item.UrlOrReference, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void Create_UsesOnlyLocalFakesAndNoRealNetworkReferences()
    {
        var baseline = Mvp1SmokeBaselineFixture.Create();

        Assert.Equal("disabled", baseline.ExternalHappyPathRequest.ExecutionMetadata.SanitizedProperties["network"]);
        Assert.Equal("disabled", baseline.ExternalHappyPathResponse.Metadata.SanitizedProperties["network"]);
        Assert.StartsWith("local-", baseline.ExternalHappyPathRequest.ExecutionMetadata.EngineName, StringComparison.Ordinal);
        Assert.StartsWith("Local", baseline.ExternalHappyPathResponse.Metadata.ProviderName, StringComparison.Ordinal);
        Assert.StartsWith("Local", baseline.ExternalHappyPathResponse.Metadata.AdapterName, StringComparison.Ordinal);
        Assert.All(
            baseline.ExternalHappyPathResponse.RetrievedContextItems,
            item =>
            {
                Assert.DoesNotContain("://", item.UrlOrReference ?? string.Empty, StringComparison.Ordinal);
                Assert.StartsWith("local-reference:", item.UrlOrReference, StringComparison.Ordinal);
            });
    }

    private static IEnumerable<string> ExtractStrings(object? value)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return ExtractStrings(value, visited);
    }

    private static IEnumerable<string> ExtractStrings(object? value, ISet<object> visited)
    {
        if (value is null)
        {
            yield break;
        }

        if (value is string text)
        {
            yield return text;
            yield break;
        }

        var type = value.GetType();

        if (type.IsValueType || type.IsEnum)
        {
            yield break;
        }

        if (!visited.Add(value))
        {
            yield break;
        }

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                foreach (var extractedText in ExtractStrings(entry.Key, visited))
                {
                    yield return extractedText;
                }

                foreach (var extractedText in ExtractStrings(entry.Value, visited))
                {
                    yield return extractedText;
                }
            }

            yield break;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                foreach (var extractedText in ExtractStrings(item, visited))
                {
                    yield return extractedText;
                }
            }

            yield break;
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            foreach (var extractedText in ExtractStrings(property.GetValue(value), visited))
            {
                yield return extractedText;
            }
        }
    }
}

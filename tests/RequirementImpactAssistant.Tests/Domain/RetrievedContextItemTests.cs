using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Tests.Domain;

public sealed class RetrievedContextItemTests
{
    [Theory]
    [InlineData(RetrievedContextItemCompleteness.FullText, "FullText")]
    [InlineData(RetrievedContextItemCompleteness.ExcerptOnly, "ExcerptOnly")]
    [InlineData(RetrievedContextItemCompleteness.MetadataOnly, "MetadataOnly")]
    [InlineData(RetrievedContextItemCompleteness.Unavailable, "Unavailable")]
    public void RetrievedContextItemCompleteness_HasStableStringRoundTrip(
        RetrievedContextItemCompleteness completeness,
        string expectedName)
    {
        Assert.Equal(expectedName, completeness.ToString());
        Assert.True(Enum.TryParse<RetrievedContextItemCompleteness>(expectedName, ignoreCase: false, out var parsedCompleteness));
        Assert.Equal(completeness, parsedCompleteness);
    }

    [Fact]
    public void RetrievedContextItem_CanRepresentFullTextSource()
    {
        var item = new RetrievedContextItem
        {
            SourceTitle = "API change request",
            SourceId = "REQ-17",
            ExternalReference = "external-doc-17",
            FragmentId = "chunk-03",
            Text = "Full retrieved fragment text.",
            UrlOrReference = "kb://requirements/REQ-17",
            Rank = 1,
            Score = 0.94,
            ProviderName = "ExternalKnowledgeProvider",
            AdapterName = "NeutralExternalAdapter",
            Completeness = RetrievedContextItemCompleteness.FullText
        };

        Assert.Equal("API change request", item.SourceTitle);
        Assert.Equal("REQ-17", item.SourceId);
        Assert.Equal("external-doc-17", item.ExternalReference);
        Assert.Equal("chunk-03", item.FragmentId);
        Assert.Equal("Full retrieved fragment text.", item.Text);
        Assert.Null(item.Excerpt);
        Assert.Equal("kb://requirements/REQ-17", item.UrlOrReference);
        Assert.Equal(1, item.Rank);
        Assert.Equal(0.94, item.Score);
        Assert.Equal("ExternalKnowledgeProvider", item.ProviderName);
        Assert.Equal("NeutralExternalAdapter", item.AdapterName);
        Assert.Equal(RetrievedContextItemCompleteness.FullText, item.Completeness);
        Assert.Null(item.WarningOrLimitationNote);
    }

    [Fact]
    public void RetrievedContextItem_CanRepresentMetadataOnlySource()
    {
        var item = new RetrievedContextItem
        {
            SourceTitle = "Integration inventory",
            ExternalReference = "inventory-record-42",
            UrlOrReference = "kb://inventory/42",
            Rank = 2,
            Score = 0.81,
            ProviderName = "ExternalKnowledgeProvider",
            AdapterName = "NeutralExternalAdapter",
            Completeness = RetrievedContextItemCompleteness.MetadataOnly
        };

        Assert.Equal("Integration inventory", item.SourceTitle);
        Assert.Null(item.SourceId);
        Assert.Equal("inventory-record-42", item.ExternalReference);
        Assert.Null(item.FragmentId);
        Assert.Null(item.Text);
        Assert.Null(item.Excerpt);
        Assert.Equal("kb://inventory/42", item.UrlOrReference);
        Assert.Equal(2, item.Rank);
        Assert.Equal(0.81, item.Score);
        Assert.Equal(RetrievedContextItemCompleteness.MetadataOnly, item.Completeness);
    }

    [Fact]
    public void RetrievedContextItem_CanRepresentUnavailableSource()
    {
        var item = new RetrievedContextItem
        {
            SourceTitle = "Knowledge base",
            ProviderName = "ExternalKnowledgeProvider",
            AdapterName = "NeutralExternalAdapter",
            Completeness = RetrievedContextItemCompleteness.Unavailable,
            WarningOrLimitationNote = "Retrieved context was unavailable from the external engine."
        };

        Assert.Equal("Knowledge base", item.SourceTitle);
        Assert.Null(item.SourceId);
        Assert.Null(item.ExternalReference);
        Assert.Null(item.FragmentId);
        Assert.Null(item.Text);
        Assert.Null(item.Excerpt);
        Assert.Null(item.UrlOrReference);
        Assert.Null(item.Rank);
        Assert.Null(item.Score);
        Assert.Equal(RetrievedContextItemCompleteness.Unavailable, item.Completeness);
        Assert.Equal("Retrieved context was unavailable from the external engine.", item.WarningOrLimitationNote);
    }

    [Fact]
    public void AiAnalysisResultMetadata_CanRepresentPartialRetrievedContext()
    {
        var metadata = new AiAnalysisResultMetadata
        {
            AnalysisMode = AnalysisMode.ExternalRag,
            EngineName = "ExternalRagAnalysisEngine",
            AdapterName = "NeutralExternalAdapter",
            RetrievedContextState = RetrievedContextState.Partial,
            RetrievedContextItems =
            [
                new RetrievedContextItem
                {
                    SourceTitle = "Requirement summary",
                    SourceId = "REQ-21",
                    Excerpt = "Only an excerpt was returned.",
                    Rank = 1,
                    Completeness = RetrievedContextItemCompleteness.ExcerptOnly,
                    WarningOrLimitationNote = "Full text was not returned by the external engine."
                }
            ],
            Warnings = ["Retrieved context is partial."]
        };

        var item = Assert.Single(metadata.RetrievedContextItems);
        Assert.Equal(RetrievedContextState.Partial, metadata.RetrievedContextState);
        Assert.Equal(RetrievedContextItemCompleteness.ExcerptOnly, item.Completeness);
        Assert.Equal("Only an excerpt was returned.", item.Excerpt);
        Assert.Null(item.Text);
        Assert.Equal(["Retrieved context is partial."], metadata.Warnings);
    }

    [Fact]
    public void DirectLlmDefaultMetadata_DoesNotCreateSyntheticRetrievedContext()
    {
        var result = new AiAnalysisResult
        {
            EngineName = "DirectLlmAnalysisEngine",
            ProviderName = "Demo",
            ModelName = "demo-deterministic"
        };

        Assert.Equal(AnalysisMode.DirectLlm, result.Metadata.AnalysisMode);
        Assert.Equal(RetrievedContextState.Unavailable, result.Metadata.RetrievedContextState);
        Assert.Empty(result.Metadata.RetrievedContextItems);
    }

    [Fact]
    public void RetrievedContextItem_IsSeparateFromManualContextFragment()
    {
        Assert.False(typeof(ContextFragment).IsAssignableFrom(typeof(RetrievedContextItem)));
        Assert.False(typeof(RetrievedContextItem).IsAssignableFrom(typeof(ContextFragment)));
    }
}

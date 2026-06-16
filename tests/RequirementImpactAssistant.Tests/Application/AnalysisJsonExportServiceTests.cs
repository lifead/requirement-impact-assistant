using System.Text;
using System.Text.Json;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Application.Export;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;
using RequirementImpactAssistant.Web.Pages.Analyses;
using RequirementImpactAssistant.Tests.Support;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class AnalysisJsonExportServiceTests
{
    public enum SavedResultReproducibilityCase
    {
        DirectLlm,
        ExternalRag,
        LegacyWithoutMvp1Metadata
    }

    [Fact]
    public async Task ExportAsync_BuildsJsonWithStableTopLevelFields()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();
            var exportedAt = new DateTimeOffset(2026, 06, 13, 14, 15, 16, TimeSpan.Zero);

            await SaveAnalysisAsync(options, analysis);

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);

            var result = await service.ExportAsync(analysis.Id, exportedAt);

            Assert.Equal(AnalysisJsonExportResultKind.Exported, result.Kind);
            Assert.EndsWith(".json", result.FileName, StringComparison.Ordinal);
            Assert.Contains("payment-api-change-", result.FileName, StringComparison.Ordinal);

            using var document = JsonDocument.Parse(result.Json);
            Assert.Equal(
                [
                    "metadata",
                    "input",
                    "contextFragments",
                    "aiAnalysisResult",
                    "impactMap",
                    "expertEvaluation",
                    "expertConclusion",
                    "exportMetadata"
                ],
                document.RootElement.EnumerateObject().Select(property => property.Name).ToArray());

            Assert.Equal(analysis.Id, document.RootElement.GetProperty("metadata").GetProperty("id").GetGuid());
            Assert.Equal("Payment API change", document.RootElement.GetProperty("metadata").GetProperty("title").GetString());
            Assert.Equal(
                "ExpertConclusionFixed",
                document.RootElement.GetProperty("metadata").GetProperty("status").GetString());
            Assert.Equal(
                "Change payment API response.",
                document.RootElement.GetProperty("input").GetProperty("originalDescription").GetString());
            Assert.Equal(
                "Add new status field to payment API response.",
                document.RootElement.GetProperty("input").GetProperty("proposedChange").GetString());
            Assert.Equal(
                "2026-06-13T14:15:16+00:00",
                document.RootElement.GetProperty("exportMetadata").GetProperty("exportedAt").GetString());
            Assert.Equal(
                "requirement-impact-assistant.analysis-export",
                document.RootElement.GetProperty("exportMetadata").GetProperty("format").GetString());

            var aiAnalysisResult = document.RootElement.GetProperty("aiAnalysisResult");
            Assert.Equal("demo-engine", aiAnalysisResult.GetProperty("engineName").GetString());
            Assert.Equal("demo-provider", aiAnalysisResult.GetProperty("providerName").GetString());
            Assert.Equal("demo-model", aiAnalysisResult.GetProperty("modelName").GetString());
            Assert.Equal("mvp-v1", aiAnalysisResult.GetProperty("promptVersion").GetString());
            Assert.Equal("{ \"request\": \"payment-api\" }", aiAnalysisResult.GetProperty("inputSnapshot").GetString());
            Assert.Equal("{ \"status\": \"completed\" }", aiAnalysisResult.GetProperty("rawResponse").GetString());
            Assert.Equal(JsonValueKind.Null, aiAnalysisResult.GetProperty("errorMessage").ValueKind);
            Assert.Equal("DirectLlm", aiAnalysisResult.GetProperty("analysisMode").GetString());
            Assert.Equal(
                "demo-engine",
                aiAnalysisResult.GetProperty("analysisEngine").GetProperty("name").GetString());
            Assert.Equal(
                "demo-provider",
                aiAnalysisResult.GetProperty("provider").GetProperty("name").GetString());
            Assert.Equal(JsonValueKind.Null, aiAnalysisResult.GetProperty("adapter").GetProperty("name").ValueKind);
            Assert.Equal(
                "demo-model",
                aiAnalysisResult.GetProperty("modelWorkflowProfile").GetProperty("name").GetString());
            Assert.False(
                aiAnalysisResult.GetProperty("manualContextUsage").GetProperty("forwardedToExternalAiOrRag").GetBoolean());
            Assert.Equal("Unavailable", aiAnalysisResult.GetProperty("retrievedContextState").GetString());
            Assert.Equal(
                "Retrieved context is unavailable for this saved analysis result.",
                Assert.Single(aiAnalysisResult.GetProperty("retrievedContextLimitations").EnumerateArray()).GetString());
            Assert.Empty(aiAnalysisResult.GetProperty("warnings").EnumerateArray());
            var retrievedContext = aiAnalysisResult.GetProperty("retrievedContext");
            Assert.Equal("Unavailable", retrievedContext.GetProperty("state").GetString());
            Assert.Empty(retrievedContext.GetProperty("items").EnumerateArray());
            Assert.Equal(
                "Retrieved context is unavailable for this saved analysis result.",
                Assert.Single(retrievedContext.GetProperty("limitations").EnumerateArray()).GetString());
            Assert.Empty(retrievedContext.GetProperty("warnings").EnumerateArray());
            Assert.False(aiAnalysisResult.TryGetProperty("items", out _));
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Theory]
    [InlineData(SavedResultReproducibilityCase.DirectLlm)]
    [InlineData(SavedResultReproducibilityCase.ExternalRag)]
    [InlineData(SavedResultReproducibilityCase.LegacyWithoutMvp1Metadata)]
    public async Task ExportAsync_ExportsSavedResultReproducibilityMatrixFromStoredGraph(
        SavedResultReproducibilityCase testCase)
    {
        var databasePath = CreateDatabasePath();
        var exportedAt = new DateTimeOffset(2026, 06, 14, 09, 10, 11, TimeSpan.Zero);

        try
        {
            var options = CreateOptions(databasePath);
            var baseline = Mvp1SmokeBaselineFixture.Create();
            var analysis = testCase switch
            {
                SavedResultReproducibilityCase.DirectLlm => Mvp1SmokeBaselineFixture.CreateSavedDirectLlmAnalysis(),
                SavedResultReproducibilityCase.ExternalRag => Mvp1SmokeBaselineFixture.CreateSavedExternalRagAnalysis(),
                SavedResultReproducibilityCase.LegacyWithoutMvp1Metadata => CreateAnalysisGraph(),
                _ => throw new ArgumentOutOfRangeException(nameof(testCase), testCase, "Unsupported export matrix case.")
            };

            await SaveAnalysisAsync(options, analysis);

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);

            var result = await service.ExportAsync(analysis.Id, exportedAt);

            Assert.Equal(AnalysisJsonExportResultKind.Exported, result.Kind);

            using var document = JsonDocument.Parse(result.Json);
            var root = document.RootElement;

            AssertJsonContainsCommonReproducibilityFields(root, exportedAt);

            switch (testCase)
            {
                case SavedResultReproducibilityCase.DirectLlm:
                    AssertJsonContainsSavedDirectLlmReproducibilityFields(root, baseline);
                    break;
                case SavedResultReproducibilityCase.ExternalRag:
                    AssertJsonContainsSavedExternalRagReproducibilityFields(root, baseline);
                    break;
                case SavedResultReproducibilityCase.LegacyWithoutMvp1Metadata:
                    AssertJsonContainsLegacyReproducibilityFields(root, analysis);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(testCase), testCase, "Unsupported export matrix case.");
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExportAsync_ExportsSavedMvp1DirectLlmSmokeBaselineHumanLayerAndImpactMap()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var baseline = Mvp1SmokeBaselineFixture.Create();
            var analysis = Mvp1SmokeBaselineFixture.CreateSavedDirectLlmAnalysis();

            await SaveAnalysisAsync(options, analysis);

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);

            var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

            Assert.Equal(AnalysisJsonExportResultKind.Exported, result.Kind);

            using var document = JsonDocument.Parse(result.Json);
            var root = document.RootElement;
            var aiAnalysisResult = root.GetProperty("aiAnalysisResult");
            var retrievedContext = aiAnalysisResult.GetProperty("retrievedContext");

            Assert.Equal(baseline.Analysis.Title, root.GetProperty("metadata").GetProperty("title").GetString());
            Assert.Equal(AnalysisMode.DirectLlm.ToString(), aiAnalysisResult.GetProperty("analysisMode").GetString());
            Assert.Equal(
                Mvp1SmokeBaselineFixture.DirectLlmEngineName,
                aiAnalysisResult.GetProperty("analysisEngine").GetProperty("name").GetString());
            Assert.Equal(
                Mvp1SmokeBaselineFixture.DirectLlmProviderName,
                aiAnalysisResult.GetProperty("provider").GetProperty("name").GetString());
            Assert.Equal(JsonValueKind.Null, aiAnalysisResult.GetProperty("adapter").GetProperty("name").ValueKind);
            Assert.Equal(
                Mvp1SmokeBaselineFixture.DirectLlmModelWorkflowProfileName,
                aiAnalysisResult.GetProperty("modelWorkflowProfile").GetProperty("name").GetString());
            Assert.False(
                aiAnalysisResult.GetProperty("manualContextUsage").GetProperty("forwardedToExternalAiOrRag").GetBoolean());
            Assert.Equal(
                RetrievedContextState.Unavailable.ToString(),
                aiAnalysisResult.GetProperty("retrievedContextState").GetString());
            Assert.Equal(RetrievedContextState.Unavailable.ToString(), retrievedContext.GetProperty("state").GetString());
            Assert.Empty(retrievedContext.GetProperty("items").EnumerateArray());
            Assert.Equal(
                "Retrieved context is unavailable for this saved analysis result.",
                Assert.Single(retrievedContext.GetProperty("limitations").EnumerateArray()).GetString());
            AssertJsonContainsSavedMvp1HumanLayerAndImpactMap(root, baseline);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExportAsync_ExportsSavedMvp1ExternalRagSmokeBaselineHumanLayerRetrievedContextAndImpactMap()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var baseline = Mvp1SmokeBaselineFixture.Create();
            var analysis = Mvp1SmokeBaselineFixture.CreateSavedExternalRagAnalysis();
            var metadata = analysis.AiAnalysisResult!.Metadata;

            await SaveAnalysisAsync(options, analysis);

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);

            var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

            Assert.Equal(AnalysisJsonExportResultKind.Exported, result.Kind);

            using var document = JsonDocument.Parse(result.Json);
            var root = document.RootElement;
            var aiAnalysisResult = root.GetProperty("aiAnalysisResult");
            var retrievedContext = aiAnalysisResult.GetProperty("retrievedContext");

            Assert.Equal(baseline.Analysis.Title, root.GetProperty("metadata").GetProperty("title").GetString());
            Assert.Equal(AnalysisMode.ExternalRag.ToString(), aiAnalysisResult.GetProperty("analysisMode").GetString());
            Assert.Equal(
                baseline.ExternalHappyPathRequest.ExecutionMetadata.EngineName,
                aiAnalysisResult.GetProperty("analysisEngine").GetProperty("name").GetString());
            Assert.Equal(
                baseline.ExternalHappyPathResponse.Metadata.ProviderName,
                aiAnalysisResult.GetProperty("provider").GetProperty("name").GetString());
            Assert.Equal(
                baseline.ExternalHappyPathResponse.Metadata.AdapterName,
                aiAnalysisResult.GetProperty("adapter").GetProperty("name").GetString());
            Assert.Equal(
                metadata.ModelWorkflowProfileName,
                aiAnalysisResult.GetProperty("modelWorkflowProfile").GetProperty("name").GetString());
            Assert.True(
                aiAnalysisResult.GetProperty("manualContextUsage").GetProperty("forwardedToExternalAiOrRag").GetBoolean());
            Assert.Equal(
                baseline.ExternalHappyPathResponse.RetrievedContextState.ToString(),
                aiAnalysisResult.GetProperty("retrievedContextState").GetString());
            Assert.Equal(
                baseline.ExternalHappyPathResponse.RetrievedContextState.ToString(),
                retrievedContext.GetProperty("state").GetString());
            Assert.Empty(retrievedContext.GetProperty("limitations").EnumerateArray());

            var items = retrievedContext.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(baseline.ExternalHappyPathResponse.RetrievedContextItems.Count, items.Length);

            for (var index = 0; index < items.Length; index++)
            {
                var expected = baseline.ExternalHappyPathResponse.RetrievedContextItems[index];
                var actual = items[index];

                Assert.Equal(expected.SourceTitle, actual.GetProperty("sourceTitle").GetString());
                Assert.Equal(expected.SourceId, actual.GetProperty("sourceId").GetString());
                Assert.Equal(expected.ExternalReference, actual.GetProperty("externalReference").GetString());
                Assert.Equal(expected.FragmentId, actual.GetProperty("fragmentId").GetString());
                Assert.Equal(expected.Text, actual.GetProperty("text").GetString());
                Assert.Equal(expected.Excerpt, actual.GetProperty("excerpt").GetString());
                Assert.Equal(expected.UrlOrReference, actual.GetProperty("urlOrReference").GetString());
                Assert.Equal(expected.Rank, actual.GetProperty("rank").GetInt32());
                Assert.Equal(expected.Score, actual.GetProperty("score").GetDouble());
                Assert.Equal(expected.ProviderName, actual.GetProperty("provider").GetString());
                Assert.Equal(expected.AdapterName, actual.GetProperty("adapter").GetString());
                Assert.Equal(expected.Completeness.ToString(), actual.GetProperty("completeness").GetString());
            }

            AssertJsonContainsSavedMvp1HumanLayerAndImpactMap(root, baseline);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExportAsync_ProducesJsonThatCanBeSavedAndParsedAsFile()
    {
        var databasePath = CreateDatabasePath();
        var exportPath = Path.Combine(Path.GetTempPath(), $"requirement-impact-assistant-export-{Guid.NewGuid():N}.json");

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();

            await SaveAnalysisAsync(options, analysis);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = new AnalysisJsonExportService(dbContext);
                var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

                await File.WriteAllTextAsync(exportPath, result.Json, Encoding.UTF8);
            }

            await using var stream = File.OpenRead(exportPath);
            using var document = await JsonDocument.ParseAsync(stream);

            Assert.Equal("Payment API change", document.RootElement.GetProperty("metadata").GetProperty("title").GetString());
            Assert.Equal(
                "Human expert conclusion",
                document.RootElement.GetProperty("expertConclusion").GetProperty("comment").GetString());
        }
        finally
        {
            DeleteDatabase(databasePath);

            if (File.Exists(exportPath))
            {
                File.Delete(exportPath);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_WithOnlyExpertConclusion_IncludesStableTopLevelFieldsWithNullOptionalObjects()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();
            analysis.AiAnalysisResult = null;
            analysis.ExpertEvaluation = null;

            await SaveAnalysisAsync(options, analysis);

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);

            var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

            Assert.Equal(AnalysisJsonExportResultKind.Exported, result.Kind);

            using var document = JsonDocument.Parse(result.Json);
            Assert.Equal(
                [
                    "metadata",
                    "input",
                    "contextFragments",
                    "aiAnalysisResult",
                    "impactMap",
                    "expertEvaluation",
                    "expertConclusion",
                    "exportMetadata"
                ],
                document.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("aiAnalysisResult").ValueKind);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("impactMap").ValueKind);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("expertEvaluation").ValueKind);
            Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("expertConclusion").ValueKind);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public void Build_OrdersImpactMapItemArraysDeterministicallyById()
    {
        var analysis = CreateAnalysisGraph();
        var impactMap = analysis.AiAnalysisResult!.ImpactMap!;
        var firstRiskId = impactMap.Risks.Single().Id;
        var secondRisk = impactMap.AddRisk();
        secondRisk.Title = "Rollout sequencing risk";
        secondRisk.Description = "Deployment order may affect API consumers.";
        secondRisk.Severity = ImpactSeverity.Low;

        ReverseImpactMapItems(impactMap, "_risks");

        var json = new AnalysisJsonReportBuilder().Build(analysis, DateTimeOffset.UtcNow);

        using var document = JsonDocument.Parse(json);
        var riskIds = document.RootElement
            .GetProperty("impactMap")
            .GetProperty("risks")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .ToArray();

        var expectedRiskIds = new[] { firstRiskId, secondRisk.Id }
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedRiskIds, riskIds);
    }

    [Fact]
    public void Build_IncludesSavedExternalMetadataAndRetrievedContext()
    {
        var analysis = CreateAnalysisGraph();
        analysis.AiAnalysisResult!.ErrorMessage = "Error message remains legacy error data, not a metadata warning.";
        analysis.AiAnalysisResult.Metadata = new AiAnalysisResultMetadata
        {
            AnalysisMode = AnalysisMode.ExternalRag,
            EngineName = "external-analysis-engine",
            ProviderName = "neutral-provider",
            AdapterName = "neutral-adapter",
            ModelWorkflowProfileName = "impact-workflow-profile",
            RetrievedContextState = RetrievedContextState.Partial,
            ManualContextForwardedToExternalAiOrRag = true,
            Warnings =
            [
                "Retrieved context is partial."
            ],
            RetrievedContextItems =
            [
                new RetrievedContextItem
                {
                    SourceTitle = "External source title",
                    SourceId = "SRC-1",
                    ExternalReference = "REQ-EXT-1",
                    FragmentId = "fragment-1",
                    Excerpt = "Saved retrieved context excerpt.",
                    UrlOrReference = "https://example.test/context/1",
                    Rank = 2,
                    Score = 0.875,
                    ProviderName = "neutral-provider",
                    AdapterName = "neutral-adapter",
                    Completeness = RetrievedContextItemCompleteness.ExcerptOnly,
                    WarningOrLimitationNote = "Only excerpt was saved."
                }
            ]
        };

        var json = new AnalysisJsonReportBuilder().Build(analysis, DateTimeOffset.UtcNow);

        using var document = JsonDocument.Parse(json);
        var aiAnalysisResult = document.RootElement.GetProperty("aiAnalysisResult");

        Assert.Equal("ExternalRag", aiAnalysisResult.GetProperty("analysisMode").GetString());
        Assert.Equal(
            "external-analysis-engine",
            aiAnalysisResult.GetProperty("analysisEngine").GetProperty("name").GetString());
        Assert.Equal(
            "neutral-provider",
            aiAnalysisResult.GetProperty("provider").GetProperty("name").GetString());
        Assert.Equal(
            "neutral-adapter",
            aiAnalysisResult.GetProperty("adapter").GetProperty("name").GetString());
        Assert.Equal(
            "impact-workflow-profile",
            aiAnalysisResult.GetProperty("modelWorkflowProfile").GetProperty("name").GetString());
        Assert.True(
            aiAnalysisResult.GetProperty("manualContextUsage").GetProperty("forwardedToExternalAiOrRag").GetBoolean());
        Assert.Equal("Partial", aiAnalysisResult.GetProperty("retrievedContextState").GetString());
        Assert.Equal(
            "Retrieved context was saved only partially for this analysis result.",
            Assert.Single(aiAnalysisResult.GetProperty("retrievedContextLimitations").EnumerateArray()).GetString());
        Assert.Equal(
            "Retrieved context is partial.",
            Assert.Single(aiAnalysisResult.GetProperty("warnings").EnumerateArray()).GetString());
        Assert.Equal(
            "Error message remains legacy error data, not a metadata warning.",
            aiAnalysisResult.GetProperty("errorMessage").GetString());
        var retrievedContext = aiAnalysisResult.GetProperty("retrievedContext");
        Assert.Equal("Partial", retrievedContext.GetProperty("state").GetString());
        Assert.Equal(
            "Retrieved context was saved only partially for this analysis result.",
            Assert.Single(retrievedContext.GetProperty("limitations").EnumerateArray()).GetString());
        Assert.Equal(
            "Retrieved context is partial.",
            Assert.Single(retrievedContext.GetProperty("warnings").EnumerateArray()).GetString());

        var item = Assert.Single(retrievedContext.GetProperty("items").EnumerateArray());
        Assert.Equal("External source title", item.GetProperty("sourceTitle").GetString());
        Assert.Equal("SRC-1", item.GetProperty("sourceId").GetString());
        Assert.Equal("REQ-EXT-1", item.GetProperty("externalReference").GetString());
        Assert.Equal("fragment-1", item.GetProperty("fragmentId").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("text").ValueKind);
        Assert.Equal("Saved retrieved context excerpt.", item.GetProperty("excerpt").GetString());
        Assert.Equal("https://example.test/context/1", item.GetProperty("urlOrReference").GetString());
        Assert.Equal(2, item.GetProperty("rank").GetInt32());
        Assert.Equal(0.875, item.GetProperty("score").GetDouble());
        Assert.Equal("neutral-provider", item.GetProperty("provider").GetString());
        Assert.Equal("neutral-adapter", item.GetProperty("adapter").GetString());
        Assert.Equal("ExcerptOnly", item.GetProperty("completeness").GetString());
        Assert.Equal("Only excerpt was saved.", item.GetProperty("warningOrLimitationNote").GetString());
        Assert.False(aiAnalysisResult.TryGetProperty("items", out _));
    }

    [Fact]
    public void Build_ExportsAvailableRetrievedContextItemsInSavedOrder()
    {
        var analysis = CreateAnalysisGraph();
        analysis.AiAnalysisResult!.Metadata = new AiAnalysisResultMetadata
        {
            AnalysisMode = AnalysisMode.ExternalRag,
            EngineName = "external-analysis-engine",
            ProviderName = "neutral-provider",
            AdapterName = "neutral-adapter",
            ModelWorkflowProfileName = "impact-workflow-profile",
            RetrievedContextState = RetrievedContextState.Available,
            RetrievedContextItems =
            [
                new RetrievedContextItem
                {
                    SourceTitle = "First saved source",
                    SourceId = "SRC-1",
                    Text = "Full text from first saved source.",
                    Rank = 10,
                    Score = 0.42,
                    ProviderName = "neutral-provider",
                    AdapterName = "neutral-adapter",
                    Completeness = RetrievedContextItemCompleteness.FullText
                },
                new RetrievedContextItem
                {
                    SourceTitle = "Second saved source",
                    SourceId = "SRC-2",
                    Excerpt = "Second saved source excerpt.",
                    Rank = 1,
                    Score = 0.99,
                    ProviderName = "neutral-provider",
                    AdapterName = "neutral-adapter",
                    Completeness = RetrievedContextItemCompleteness.ExcerptOnly
                }
            ]
        };

        var retrievedContext = BuildRetrievedContext(analysis);
        var items = retrievedContext.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal("Available", retrievedContext.GetProperty("state").GetString());
        Assert.Empty(retrievedContext.GetProperty("limitations").EnumerateArray());
        Assert.Equal(
            ["First saved source", "Second saved source"],
            items.Select(item => item.GetProperty("sourceTitle").GetString()).ToArray());
        Assert.Equal(10, items[0].GetProperty("rank").GetInt32());
        Assert.Equal(1, items[1].GetProperty("rank").GetInt32());
    }

    [Fact]
    public void Build_ExportsMetadataOnlyRetrievedContextWithoutTextOrExcerpt()
    {
        var analysis = CreateAnalysisGraph();
        analysis.AiAnalysisResult!.Metadata = new AiAnalysisResultMetadata
        {
            AnalysisMode = AnalysisMode.ExternalRag,
            EngineName = "external-analysis-engine",
            ProviderName = "neutral-provider",
            AdapterName = "neutral-adapter",
            ModelWorkflowProfileName = "impact-workflow-profile",
            RetrievedContextState = RetrievedContextState.MetadataOnly,
            RetrievedContextItems =
            [
                new RetrievedContextItem
                {
                    SourceTitle = "Metadata-only source",
                    SourceId = "META-1",
                    ExternalReference = "external-meta-1",
                    UrlOrReference = "kb://meta/1",
                    ProviderName = "neutral-provider",
                    AdapterName = "neutral-adapter",
                    Completeness = RetrievedContextItemCompleteness.MetadataOnly,
                    WarningOrLimitationNote = "Text was not saved."
                }
            ]
        };

        var retrievedContext = BuildRetrievedContext(analysis);
        var item = Assert.Single(retrievedContext.GetProperty("items").EnumerateArray());

        Assert.Equal("MetadataOnly", retrievedContext.GetProperty("state").GetString());
        Assert.Equal(
            "Only retrieved context metadata was saved for this analysis result.",
            Assert.Single(retrievedContext.GetProperty("limitations").EnumerateArray()).GetString());
        Assert.Equal("Metadata-only source", item.GetProperty("sourceTitle").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("text").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("excerpt").ValueKind);
        Assert.Equal("MetadataOnly", item.GetProperty("completeness").GetString());
        Assert.Equal("Text was not saved.", item.GetProperty("warningOrLimitationNote").GetString());
    }

    [Fact]
    public void Build_ExportsUnavailableRetrievedContextWithoutArtificialItems()
    {
        var analysis = CreateAnalysisGraph();
        analysis.AiAnalysisResult!.Metadata = new AiAnalysisResultMetadata
        {
            AnalysisMode = AnalysisMode.ExternalRag,
            EngineName = "external-analysis-engine",
            ProviderName = "neutral-provider",
            AdapterName = "neutral-adapter",
            ModelWorkflowProfileName = "impact-workflow-profile",
            RetrievedContextState = RetrievedContextState.Unavailable,
            Warnings =
            [
                "External retrieval was unavailable."
            ]
        };

        var retrievedContext = BuildRetrievedContext(analysis);

        Assert.Equal("Unavailable", retrievedContext.GetProperty("state").GetString());
        Assert.Empty(retrievedContext.GetProperty("items").EnumerateArray());
        Assert.Equal(
            "Retrieved context is unavailable for this saved analysis result.",
            Assert.Single(retrievedContext.GetProperty("limitations").EnumerateArray()).GetString());
        Assert.Equal(
            "External retrieval was unavailable.",
            Assert.Single(retrievedContext.GetProperty("warnings").EnumerateArray()).GetString());
    }

    [Fact]
    public void Build_ExportsDirectLlmRetrievedContextWithoutArtificialItems()
    {
        var analysis = CreateAnalysisGraph();
        analysis.AiAnalysisResult!.Metadata = AiAnalysisResultMetadata.CreateDefaultDirectLlm(
            engineName: "direct-engine",
            providerName: "direct-provider",
            modelWorkflowProfileName: "direct-model");

        var retrievedContext = BuildRetrievedContext(analysis);

        Assert.Equal("Unavailable", retrievedContext.GetProperty("state").GetString());
        Assert.Empty(retrievedContext.GetProperty("items").EnumerateArray());
        Assert.Equal(
            "Retrieved context is unavailable for this saved analysis result.",
            Assert.Single(retrievedContext.GetProperty("limitations").EnumerateArray()).GetString());
    }

    [Fact]
    public void Build_ExportsLegacyMvp0RetrievedContextWithoutArtificialItems()
    {
        var analysis = CreateAnalysisGraph();
        analysis.AiAnalysisResult = new AiAnalysisResult
        {
            AnalysisId = analysis.Id,
            Status = AiAnalysisResultStatus.Completed,
            GeneratedAt = new DateTimeOffset(2026, 06, 13, 12, 20, 0, TimeSpan.Zero),
            EngineName = "legacy-engine",
            ProviderName = "legacy-provider",
            ModelName = "legacy-model",
            PromptVersion = "mvp-v0",
            InputSnapshot = "{ \"legacy\": true }",
            RawResponse = "{ \"legacy\": \"response\" }",
            ImpactMap = analysis.AiAnalysisResult!.ImpactMap,
            ErrorMessage = string.Empty
        };

        var json = new AnalysisJsonReportBuilder().Build(analysis, DateTimeOffset.UtcNow);

        using var document = JsonDocument.Parse(json);
        var aiAnalysisResult = document.RootElement.GetProperty("aiAnalysisResult");
        var retrievedContext = aiAnalysisResult.GetProperty("retrievedContext");

        Assert.Equal("DirectLlm", aiAnalysisResult.GetProperty("analysisMode").GetString());
        Assert.Equal(
            "legacy-engine",
            aiAnalysisResult.GetProperty("analysisEngine").GetProperty("name").GetString());
        Assert.Equal(
            "legacy-provider",
            aiAnalysisResult.GetProperty("provider").GetProperty("name").GetString());
        Assert.Equal(
            "legacy-model",
            aiAnalysisResult.GetProperty("modelWorkflowProfile").GetProperty("name").GetString());
        Assert.Equal("Unavailable", retrievedContext.GetProperty("state").GetString());
        Assert.Empty(retrievedContext.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ExportAsync_ExportsSavedExternalRetrievedContextItemsFromSqlite()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();
            analysis.AiAnalysisResult!.Metadata = new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = "external-analysis-engine",
                ProviderName = "neutral-provider",
                AdapterName = "neutral-adapter",
                ModelWorkflowProfileName = "impact-workflow-profile",
                RetrievedContextState = RetrievedContextState.MetadataOnly,
                ManualContextForwardedToExternalAiOrRag = true,
                Warnings = ["Only retrieved context metadata was saved."],
                RetrievedContextItems =
                [
                    new RetrievedContextItem
                    {
                        SourceTitle = "Saved external requirement",
                        SourceId = "REQ-EXT-5",
                        ExternalReference = "external-record-5",
                        FragmentId = "fragment-5",
                        UrlOrReference = "kb://external/5",
                        Rank = 3,
                        Score = 0.73,
                        ProviderName = "neutral-provider",
                        AdapterName = "neutral-adapter",
                        Completeness = RetrievedContextItemCompleteness.MetadataOnly,
                        WarningOrLimitationNote = "Full text was not saved."
                    }
                ]
            };

            await SaveAnalysisAsync(options, analysis);

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);

            var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

            Assert.Equal(AnalysisJsonExportResultKind.Exported, result.Kind);

            using var document = JsonDocument.Parse(result.Json);
            var aiAnalysisResult = document.RootElement.GetProperty("aiAnalysisResult");
            var retrievedContext = aiAnalysisResult.GetProperty("retrievedContext");

            Assert.Equal("ExternalRag", aiAnalysisResult.GetProperty("analysisMode").GetString());
            Assert.Equal(
                "external-analysis-engine",
                aiAnalysisResult.GetProperty("analysisEngine").GetProperty("name").GetString());
            Assert.Equal(
                "neutral-provider",
                aiAnalysisResult.GetProperty("provider").GetProperty("name").GetString());
            Assert.Equal(
                "neutral-adapter",
                aiAnalysisResult.GetProperty("adapter").GetProperty("name").GetString());
            Assert.Equal(
                "impact-workflow-profile",
                aiAnalysisResult.GetProperty("modelWorkflowProfile").GetProperty("name").GetString());
            Assert.True(
                aiAnalysisResult.GetProperty("manualContextUsage").GetProperty("forwardedToExternalAiOrRag").GetBoolean());
            Assert.Equal("MetadataOnly", aiAnalysisResult.GetProperty("retrievedContextState").GetString());
            Assert.Equal(
                "Only retrieved context metadata was saved for this analysis result.",
                Assert.Single(aiAnalysisResult.GetProperty("retrievedContextLimitations").EnumerateArray()).GetString());
            Assert.Equal(
                "Only retrieved context metadata was saved.",
                Assert.Single(aiAnalysisResult.GetProperty("warnings").EnumerateArray()).GetString());
            Assert.Equal("MetadataOnly", retrievedContext.GetProperty("state").GetString());
            Assert.Equal(
                "Only retrieved context metadata was saved for this analysis result.",
                Assert.Single(retrievedContext.GetProperty("limitations").EnumerateArray()).GetString());
            Assert.Equal(
                "Only retrieved context metadata was saved.",
                Assert.Single(retrievedContext.GetProperty("warnings").EnumerateArray()).GetString());

            var item = Assert.Single(retrievedContext.GetProperty("items").EnumerateArray());
            Assert.Equal("Saved external requirement", item.GetProperty("sourceTitle").GetString());
            Assert.Equal("REQ-EXT-5", item.GetProperty("sourceId").GetString());
            Assert.Equal("external-record-5", item.GetProperty("externalReference").GetString());
            Assert.Equal("fragment-5", item.GetProperty("fragmentId").GetString());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("text").ValueKind);
            Assert.Equal(JsonValueKind.Null, item.GetProperty("excerpt").ValueKind);
            Assert.Equal("kb://external/5", item.GetProperty("urlOrReference").GetString());
            Assert.Equal(3, item.GetProperty("rank").GetInt32());
            Assert.Equal(0.73, item.GetProperty("score").GetDouble());
            Assert.Equal("neutral-provider", item.GetProperty("provider").GetString());
            Assert.Equal("neutral-adapter", item.GetProperty("adapter").GetString());
            Assert.Equal("MetadataOnly", item.GetProperty("completeness").GetString());
            Assert.Equal("Full text was not saved.", item.GetProperty("warningOrLimitationNote").GetString());
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Theory]
    [InlineData(null, "Available", "happy-path", 2, "Local demo requirement catalogue")]
    [InlineData("metadata-only", "MetadataOnly", "metadata-only", 1, "Local demo integration inventory")]
    [InlineData("unavailable", "Unavailable", "unavailable", 0, null)]
    [InlineData("partial", "Partial", "partial", 1, "Local demo requirement excerpt")]
    public async Task ExportAsync_ExportsAlreadySavedMockExternalResultWithoutCallingAnalysisAgain(
        string? mockProfileName,
        string expectedRetrievedContextState,
        string expectedProfileName,
        int expectedRetrievedContextItemCount,
        string? expectedSourceTitle)
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var saved = await SaveAnalysisThroughMockExternalModeAsync(options, mockProfileName);
            var externalEngineCallsAfterSave = saved.ExternalEngine.CallCount;
            var adapterCallsAfterSave = saved.Adapter.CallCount;
            var selectorCallsAfterSave = saved.Selector.SelectedModes.Count;

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);

            var result = await service.ExportAsync(saved.AnalysisId, DateTimeOffset.UtcNow);

            Assert.Equal(AnalysisJsonExportResultKind.Exported, result.Kind);
            Assert.Equal(1, externalEngineCallsAfterSave);
            Assert.Equal(1, adapterCallsAfterSave);
            Assert.Equal([AnalysisMode.ExternalRag], saved.Selector.SelectedModes);
            Assert.Equal(externalEngineCallsAfterSave, saved.ExternalEngine.CallCount);
            Assert.Equal(adapterCallsAfterSave, saved.Adapter.CallCount);
            Assert.Equal(selectorCallsAfterSave, saved.Selector.SelectedModes.Count);
            Assert.Equal(0, saved.DirectEngine.CallCount);

            using var document = JsonDocument.Parse(result.Json);
            var aiAnalysisResult = document.RootElement.GetProperty("aiAnalysisResult");
            var retrievedContext = aiAnalysisResult.GetProperty("retrievedContext");

            Assert.Equal("ExternalRag", aiAnalysisResult.GetProperty("analysisMode").GetString());
            Assert.Equal(
                nameof(ExternalRagAnalysisEngine),
                aiAnalysisResult.GetProperty("analysisEngine").GetProperty("name").GetString());
            Assert.Equal(
                "LocalMockKnowledgeSource",
                aiAnalysisResult.GetProperty("provider").GetProperty("name").GetString());
            Assert.Equal(
                nameof(MockExternalRagAdapter),
                aiAnalysisResult.GetProperty("adapter").GetProperty("name").GetString());
            Assert.Equal(
                $"local-demo-model / mock-impact-analysis / {expectedProfileName}",
                aiAnalysisResult.GetProperty("modelWorkflowProfile").GetProperty("name").GetString());
            Assert.True(
                aiAnalysisResult.GetProperty("manualContextUsage").GetProperty("forwardedToExternalAiOrRag").GetBoolean());
            Assert.Equal(expectedRetrievedContextState, aiAnalysisResult.GetProperty("retrievedContextState").GetString());
            Assert.Equal(expectedRetrievedContextState, retrievedContext.GetProperty("state").GetString());

            var items = retrievedContext.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(expectedRetrievedContextItemCount, items.Length);

            if (expectedSourceTitle is not null)
            {
                Assert.Equal(expectedSourceTitle, items[0].GetProperty("sourceTitle").GetString());
            }

            Assert.DoesNotContain("Dify", result.Json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ApiKey", result.Json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Endpoint", result.Json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", result.Json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("embedding", result.Json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("rerank", result.Json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("vector", result.Json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExportAsync_ExportsSavedLegacyMvp0ResultWithDirectLlmFallbacks()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();

            await SaveAnalysisAsync(options, analysis);

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);

            var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

            Assert.Equal(AnalysisJsonExportResultKind.Exported, result.Kind);

            using var document = JsonDocument.Parse(result.Json);
            var aiAnalysisResult = document.RootElement.GetProperty("aiAnalysisResult");
            var retrievedContext = aiAnalysisResult.GetProperty("retrievedContext");

            Assert.Equal("DirectLlm", aiAnalysisResult.GetProperty("analysisMode").GetString());
            Assert.Equal(
                "demo-engine",
                aiAnalysisResult.GetProperty("analysisEngine").GetProperty("name").GetString());
            Assert.Equal(
                "demo-provider",
                aiAnalysisResult.GetProperty("provider").GetProperty("name").GetString());
            Assert.Equal(
                "demo-model",
                aiAnalysisResult.GetProperty("modelWorkflowProfile").GetProperty("name").GetString());
            Assert.False(
                aiAnalysisResult.GetProperty("manualContextUsage").GetProperty("forwardedToExternalAiOrRag").GetBoolean());
            Assert.Equal("Unavailable", aiAnalysisResult.GetProperty("retrievedContextState").GetString());
            Assert.Empty(aiAnalysisResult.GetProperty("warnings").EnumerateArray());
            Assert.Equal("Unavailable", retrievedContext.GetProperty("state").GetString());
            Assert.Empty(retrievedContext.GetProperty("items").EnumerateArray());
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExportAsync_IsUnavailableWithoutExpertConclusion()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();
            analysis.ExpertConclusion = null;

            await SaveAnalysisAsync(options, analysis);

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);

            var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

            Assert.Equal(AnalysisJsonExportResultKind.Unavailable, result.Kind);
            Assert.Empty(result.Json);
            Assert.Contains("expert conclusion", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExportAsync_IncludesRequiredChapterFourFields()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();
            var affectedRequirementId = analysis.AiAnalysisResult!.ImpactMap!.AffectedRequirements.Single().Id;
            var riskId = analysis.AiAnalysisResult.ImpactMap.Risks.Single().Id;

            await SaveAnalysisAsync(options, analysis);

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);

            var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

            using var document = JsonDocument.Parse(result.Json);
            var root = document.RootElement;
            Assert.Equal(
                affectedRequirementId,
                root.GetProperty("impactMap").GetProperty("affectedRequirements")[0].GetProperty("id").GetString());
            Assert.Equal(
                riskId,
                root.GetProperty("impactMap").GetProperty("risks")[0].GetProperty("id").GetString());
            Assert.Equal(
                "Confirmed",
                root.GetProperty("expertEvaluation").GetProperty("expertMarks")[0].GetProperty("mark").GetString());
            Assert.Equal(
                "Missing rollout note",
                root.GetProperty("expertEvaluation").GetProperty("missedItems")[0].GetProperty("title").GetString());
            Assert.Equal(
                "Clarify compatibility risk for API consumers.",
                root.GetProperty("expertEvaluation").GetProperty("corrections")[0].GetProperty("text").GetString());
            Assert.Equal(
                "Accept after documenting rollout constraints.",
                root.GetProperty("expertConclusion").GetProperty("rationale").GetString());
            Assert.Contains(
                "does not make a management decision",
                root.GetProperty("exportMetadata")
                    .GetProperty("boundaryNotice")
                    .GetProperty("statement")
                    .GetString(),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExportAsync_DoesNotRequireDeepSeekApiKeyUserSecretsOrNetworkAccess()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();

            await SaveAnalysisAsync(options, analysis);

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);

            var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

            Assert.Equal(AnalysisJsonExportResultKind.Exported, result.Kind);
            Assert.Contains("demo-provider", result.Json, StringComparison.Ordinal);
            Assert.Contains("Human expert conclusion", result.Json, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task DetailsPage_ExportsJsonFileFromUiHandler()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();

            await SaveAnalysisAsync(options, analysis);

            await using var dbContext = new ApplicationDbContext(options);
            var service = new AnalysisJsonExportService(dbContext);
            var pageModel = new DetailsModel(dbContext, jsonExportService: service);

            var result = await pageModel.OnGetExportJsonAsync(analysis.Id);

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal(AnalysisJsonExportService.ContentType, fileResult.ContentType);
            Assert.EndsWith(".json", fileResult.FileDownloadName, StringComparison.Ordinal);

            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(fileResult.FileContents));
            Assert.Equal("Payment API change", document.RootElement.GetProperty("metadata").GetProperty("title").GetString());
            Assert.Equal(
                "AcceptWithLimitations",
                document.RootElement.GetProperty("expertConclusion").GetProperty("conclusionType").GetString());
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static void AssertJsonContainsCommonReproducibilityFields(
        JsonElement root,
        DateTimeOffset exportedAt)
    {
        Assert.Equal(
            "ExpertConclusionFixed",
            root.GetProperty("metadata").GetProperty("status").GetString());
        Assert.Equal(
            "requirement-impact-assistant.analysis-export",
            root.GetProperty("exportMetadata").GetProperty("format").GetString());
        Assert.Equal(
            exportedAt,
            root.GetProperty("exportMetadata").GetProperty("exportedAt").GetDateTimeOffset());
        Assert.Contains(
            "does not make a management decision",
            root.GetProperty("exportMetadata")
                .GetProperty("boundaryNotice")
                .GetProperty("statement")
                .GetString(),
            StringComparison.Ordinal);

        Assert.True(root.TryGetProperty("aiAnalysisResult", out _));
        Assert.True(root.TryGetProperty("impactMap", out _));
        Assert.True(root.TryGetProperty("expertEvaluation", out _));
        Assert.True(root.TryGetProperty("expertConclusion", out _));
    }

    private static void AssertJsonContainsSavedDirectLlmReproducibilityFields(
        JsonElement root,
        Mvp1SmokeBaseline baseline)
    {
        var aiAnalysisResult = root.GetProperty("aiAnalysisResult");
        var retrievedContext = aiAnalysisResult.GetProperty("retrievedContext");

        Assert.Equal(baseline.Analysis.Title, root.GetProperty("metadata").GetProperty("title").GetString());
        Assert.Equal(AnalysisMode.DirectLlm.ToString(), aiAnalysisResult.GetProperty("analysisMode").GetString());
        Assert.Equal(
            Mvp1SmokeBaselineFixture.DirectLlmEngineName,
            aiAnalysisResult.GetProperty("analysisEngine").GetProperty("name").GetString());
        Assert.Equal(
            Mvp1SmokeBaselineFixture.DirectLlmProviderName,
            aiAnalysisResult.GetProperty("provider").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, aiAnalysisResult.GetProperty("adapter").GetProperty("name").ValueKind);
        Assert.Equal(
            Mvp1SmokeBaselineFixture.DirectLlmModelWorkflowProfileName,
            aiAnalysisResult.GetProperty("modelWorkflowProfile").GetProperty("name").GetString());
        Assert.False(
            aiAnalysisResult.GetProperty("manualContextUsage").GetProperty("forwardedToExternalAiOrRag").GetBoolean());
        Assert.Equal(RetrievedContextState.Unavailable.ToString(), aiAnalysisResult.GetProperty("retrievedContextState").GetString());
        Assert.Equal(RetrievedContextState.Unavailable.ToString(), retrievedContext.GetProperty("state").GetString());
        Assert.Empty(retrievedContext.GetProperty("items").EnumerateArray());
        Assert.Equal(
            "Retrieved context is unavailable for this saved analysis result.",
            Assert.Single(retrievedContext.GetProperty("limitations").EnumerateArray()).GetString());
    }

    private static void AssertJsonContainsSavedExternalRagReproducibilityFields(
        JsonElement root,
        Mvp1SmokeBaseline baseline)
    {
        var response = baseline.ExternalHappyPathResponse;
        var aiAnalysisResult = root.GetProperty("aiAnalysisResult");
        var retrievedContext = aiAnalysisResult.GetProperty("retrievedContext");

        Assert.Equal(baseline.Analysis.Title, root.GetProperty("metadata").GetProperty("title").GetString());
        Assert.Equal(AnalysisMode.ExternalRag.ToString(), aiAnalysisResult.GetProperty("analysisMode").GetString());
        Assert.Equal(
            baseline.ExternalHappyPathRequest.ExecutionMetadata.EngineName,
            aiAnalysisResult.GetProperty("analysisEngine").GetProperty("name").GetString());
        Assert.Equal(
            response.Metadata.ProviderName,
            aiAnalysisResult.GetProperty("provider").GetProperty("name").GetString());
        Assert.Equal(
            response.Metadata.AdapterName,
            aiAnalysisResult.GetProperty("adapter").GetProperty("name").GetString());
        Assert.Equal(
            "local-demo-model / mock-impact-analysis / happy-path",
            aiAnalysisResult.GetProperty("modelWorkflowProfile").GetProperty("name").GetString());
        Assert.True(
            aiAnalysisResult.GetProperty("manualContextUsage").GetProperty("forwardedToExternalAiOrRag").GetBoolean());
        Assert.Equal(response.RetrievedContextState.ToString(), aiAnalysisResult.GetProperty("retrievedContextState").GetString());
        Assert.Equal(response.RetrievedContextState.ToString(), retrievedContext.GetProperty("state").GetString());
        Assert.Empty(retrievedContext.GetProperty("limitations").EnumerateArray());

        var items = retrievedContext.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(response.RetrievedContextItems.Count, items.Length);
        Assert.Equal(response.RetrievedContextItems[0].SourceTitle, items[0].GetProperty("sourceTitle").GetString());
        Assert.Equal(response.RetrievedContextItems[0].Text, items[0].GetProperty("text").GetString());
    }

    private static void AssertJsonContainsLegacyReproducibilityFields(JsonElement root, Analysis analysis)
    {
        var aiAnalysisResult = root.GetProperty("aiAnalysisResult");
        var retrievedContext = aiAnalysisResult.GetProperty("retrievedContext");

        Assert.Equal("Payment API change", root.GetProperty("metadata").GetProperty("title").GetString());
        Assert.Equal("DirectLlm", aiAnalysisResult.GetProperty("analysisMode").GetString());
        Assert.Equal(
            analysis.AiAnalysisResult!.EngineName,
            aiAnalysisResult.GetProperty("analysisEngine").GetProperty("name").GetString());
        Assert.Equal(
            analysis.AiAnalysisResult.ProviderName,
            aiAnalysisResult.GetProperty("provider").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, aiAnalysisResult.GetProperty("adapter").GetProperty("name").ValueKind);
        Assert.Equal(
            analysis.AiAnalysisResult.ModelName,
            aiAnalysisResult.GetProperty("modelWorkflowProfile").GetProperty("name").GetString());
        Assert.False(
            aiAnalysisResult.GetProperty("manualContextUsage").GetProperty("forwardedToExternalAiOrRag").GetBoolean());
        Assert.Equal("Unavailable", aiAnalysisResult.GetProperty("retrievedContextState").GetString());
        Assert.Empty(aiAnalysisResult.GetProperty("warnings").EnumerateArray());
        Assert.Equal("Unavailable", retrievedContext.GetProperty("state").GetString());
        Assert.Empty(retrievedContext.GetProperty("items").EnumerateArray());
        Assert.Equal(
            "Retrieved context is unavailable for this saved analysis result.",
            Assert.Single(retrievedContext.GetProperty("limitations").EnumerateArray()).GetString());

        var expertEvaluation = root.GetProperty("expertEvaluation");
        Assert.Equal("PartiallySufficient", expertEvaluation.GetProperty("contextSufficiency").GetString());
        Assert.Equal("Useful", expertEvaluation.GetProperty("resultUsefulness").GetString());

        var expertConclusion = root.GetProperty("expertConclusion");
        Assert.Equal("AcceptWithLimitations", expertConclusion.GetProperty("conclusionType").GetString());
    }

    private static JsonElement BuildRetrievedContext(Analysis analysis)
    {
        var json = new AnalysisJsonReportBuilder().Build(analysis, DateTimeOffset.UtcNow);

        using var document = JsonDocument.Parse(json);

        return document.RootElement
            .GetProperty("aiAnalysisResult")
            .GetProperty("retrievedContext")
            .Clone();
    }

    private static async Task SaveAnalysisAsync(DbContextOptions<ApplicationDbContext> options, Analysis analysis)
    {
        var retrievedContextItems = analysis.AiAnalysisResult?.Metadata.RetrievedContextItems.ToList() ?? [];
        analysis.AiAnalysisResult?.Metadata.RetrievedContextItems.Clear();

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.MigrateAsync();
        dbContext.Analyses.Add(analysis);
        await dbContext.SaveChangesAsync();

        if (analysis.AiAnalysisResult is null || retrievedContextItems.Count == 0)
        {
            return;
        }

        for (var index = 0; index < retrievedContextItems.Count; index++)
        {
            var contextItem = retrievedContextItems[index];
            analysis.AiAnalysisResult.Metadata.RetrievedContextItems.Add(contextItem);
            dbContext.Entry(contextItem).Property("Ordinal").CurrentValue = index;
        }

        await dbContext.SaveChangesAsync();
    }

    private static void AssertJsonContainsSavedMvp1HumanLayerAndImpactMap(
        JsonElement root,
        Mvp1SmokeBaseline baseline)
    {
        var contextFragments = root.GetProperty("contextFragments").EnumerateArray().ToArray();
        Assert.Equal(baseline.ManualContextFragments.Count, contextFragments.Length);

        foreach (var expected in baseline.ManualContextFragments)
        {
            var actual = Assert.Single(
                contextFragments,
                fragment => fragment.GetProperty("id").GetGuid() == expected.Id);
            Assert.Equal(expected.Source, actual.GetProperty("source").GetString());
            Assert.Equal(expected.Text, actual.GetProperty("text").GetString());
        }

        var impactMap = root.GetProperty("impactMap");
        Assert.Equal(
            baseline.ExpectedImpactMap.ChangeSummary.Title,
            impactMap.GetProperty("changeSummary").GetProperty("title").GetString());
        Assert.Equal(
            baseline.ExpectedImpactMap.AffectedRequirements.Single().Title,
            impactMap.GetProperty("affectedRequirements")[0].GetProperty("title").GetString());
        Assert.Equal(
            baseline.ExpectedImpactMap.AffectedProjectDecisions.Single().Title,
            impactMap.GetProperty("affectedProjectDecisions")[0].GetProperty("title").GetString());
        Assert.Equal(
            baseline.ExpectedImpactMap.AffectedApiInterfacesDocumentsTests.Single().Title,
            impactMap.GetProperty("affectedApiInterfacesDocumentsTests")[0].GetProperty("title").GetString());
        Assert.Equal(
            baseline.ExpectedImpactMap.Risks.Single().Title,
            impactMap.GetProperty("risks")[0].GetProperty("title").GetString());
        Assert.Equal(
            baseline.ExpectedImpactMap.OptionsForExpertReview.Single().Title,
            impactMap.GetProperty("optionsForExpertReview")[0].GetProperty("title").GetString());
        Assert.Equal(
            baseline.ExpectedImpactMap.PreliminaryAssessment.Title,
            impactMap.GetProperty("preliminaryAssessment").GetProperty("title").GetString());

        var expertEvaluation = root.GetProperty("expertEvaluation");
        Assert.Equal(
            baseline.ExpectedExpertEvaluation.ContextSufficiency.ToString(),
            expertEvaluation.GetProperty("contextSufficiency").GetString());
        Assert.Equal(
            baseline.ExpectedExpertEvaluation.ResultUsefulness.ToString(),
            expertEvaluation.GetProperty("resultUsefulness").GetString());
        Assert.Equal(
            baseline.ExpectedExpertEvaluation.GeneralComment,
            expertEvaluation.GetProperty("generalComment").GetString());

        var expertMarks = expertEvaluation.GetProperty("expertMarks").EnumerateArray().ToArray();
        Assert.Equal(baseline.ExpectedExpertEvaluation.EvaluatedItems.Count, expertMarks.Length);
        foreach (var expected in baseline.ExpectedExpertEvaluation.EvaluatedItems)
        {
            var actual = Assert.Single(
                expertMarks,
                item => item.GetProperty("targetId").GetString() == expected.TargetId);
            Assert.Equal(expected.TargetType.ToString(), actual.GetProperty("targetType").GetString());
            Assert.Equal(expected.Mark.ToString(), actual.GetProperty("mark").GetString());
            Assert.Equal(expected.Comment, actual.GetProperty("comment").GetString());
            Assert.Equal(expected.CorrectionText, actual.GetProperty("correctionText").GetString());
        }

        var missedItems = expertEvaluation.GetProperty("missedItems").EnumerateArray().ToArray();
        var expectedMissedItem = Assert.Single(baseline.ExpectedExpertEvaluation.MissedItems);
        var actualMissedItem = Assert.Single(missedItems);
        Assert.Equal(expectedMissedItem.Title, actualMissedItem.GetProperty("title").GetString());
        Assert.Equal(expectedMissedItem.Description, actualMissedItem.GetProperty("description").GetString());
        Assert.Equal(expectedMissedItem.Comment, actualMissedItem.GetProperty("comment").GetString());

        var corrections = expertEvaluation.GetProperty("corrections").EnumerateArray().ToArray();
        var expectedCorrection = Assert.Single(baseline.ExpectedExpertEvaluation.Corrections);
        var actualCorrection = Assert.Single(corrections);
        Assert.Equal(expectedCorrection.TargetId, actualCorrection.GetProperty("targetId").GetString());
        Assert.Equal(expectedCorrection.Text, actualCorrection.GetProperty("text").GetString());
        Assert.Equal(expectedCorrection.Comment, actualCorrection.GetProperty("comment").GetString());

        var expertConclusion = root.GetProperty("expertConclusion");
        Assert.Equal(
            baseline.ExpectedExpertConclusion.ConclusionType.ToString(),
            expertConclusion.GetProperty("conclusionType").GetString());
        Assert.Equal(baseline.ExpectedExpertConclusion.Comment, expertConclusion.GetProperty("comment").GetString());
        Assert.Equal(baseline.ExpectedExpertConclusion.Rationale, expertConclusion.GetProperty("rationale").GetString());
        Assert.Equal(
            baseline.ExpectedExpertConclusion.FixedAt,
            expertConclusion.GetProperty("fixedAt").GetDateTimeOffset());

        Assert.Contains(
            "does not make a management decision",
            root.GetProperty("exportMetadata")
                .GetProperty("boundaryNotice")
                .GetProperty("statement")
                .GetString(),
            StringComparison.Ordinal);
    }

    private static async Task<SavedMockExternalAnalysis> SaveAnalysisThroughMockExternalModeAsync(
        DbContextOptions<ApplicationDbContext> options,
        string? mockProfileName)
    {
        var analysis = CreateReadyForExternalAnalysisGraph();
        var adapter = new ProfiledCountingMockExternalRagAdapter(mockProfileName);
        var directEngine = new CountingAiAnalysisEngine(new ThrowingAiAnalysisEngine());
        var externalEngine = new CountingAiAnalysisEngine(new ExternalRagAnalysisEngine(adapter));
        var selector = new CountingAiAnalysisEngineSelector(directEngine, externalEngine);

        await using (var dbContext = new ApplicationDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
            dbContext.Analyses.Add(analysis);
            await dbContext.SaveChangesAsync();
        }

        await using (var dbContext = new ApplicationDbContext(options))
        {
            var service = new AnalysisExecutionService(
                dbContext,
                new AnalysisInputAssembler(),
                selector,
                Options.Create(new AiAnalysisOptions
                {
                    Provider = LlmProviderNames.Demo
                }));

            var outcome = await service.RunAsync(analysis.Id, AnalysisMode.ExternalRag);

            Assert.Equal(AnalysisExecutionOutcomeKind.Completed, outcome.Kind);
            Assert.True(outcome.Succeeded);
        }

        await using (var dbContext = new ApplicationDbContext(options))
        {
            var savedAnalysis = await dbContext.Analyses.SingleAsync(candidate => candidate.Id == analysis.Id);
            var fixedAt = new DateTimeOffset(2026, 06, 13, 12, 30, 0, TimeSpan.Zero);

            savedAnalysis.Status = AnalysisStatus.ExpertConclusionFixed;
            savedAnalysis.UpdatedAt = fixedAt;
            dbContext.ExpertConclusions.Add(new ExpertConclusion
            {
                AnalysisId = analysis.Id,
                ConclusionType = ExpertConclusionType.AcceptWithLimitations,
                Comment = "Human expert conclusion for saved mock external result.",
                Rationale = "Accepted after reviewing preliminary mock external analytical material.",
                FixedAt = fixedAt
            });

            await dbContext.SaveChangesAsync();
        }

        return new SavedMockExternalAnalysis(
            analysis.Id,
            directEngine,
            externalEngine,
            adapter,
            selector);
    }

    private static string CreateDatabasePath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"requirement-impact-assistant-json-export-{Guid.NewGuid():N}.db");

    private static DbContextOptions<ApplicationDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

    private static Analysis CreateAnalysisGraph()
    {
        var analysisId = Guid.NewGuid();
        var fixedAt = new DateTimeOffset(2026, 06, 13, 12, 30, 0, TimeSpan.Zero);
        var fragment = new ContextFragment
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Type = ContextFragmentType.ApiDescription,
            Source = "OpenAPI fragment",
            Text = "Payment API response contract changes.",
            FileName = "payment-api.md",
            FilePath = "context/payment-api.md",
            CreatedAt = fixedAt.AddMinutes(-30)
        };

        var impactMap = new ImpactMap
        {
            ChangeSummary =
            {
                Title = "Scope update",
                Description = "The request changes the payment API response.",
                Severity = ImpactSeverity.Medium,
                Notes = "Stable singleton id must survive export."
            },
            PreliminaryAssessment =
            {
                Title = "Requires expert review",
                Description = "The change affects requirements and integration tests.",
                Severity = ImpactSeverity.High,
                Notes = "Preliminary only."
            }
        };

        var affectedRequirement = impactMap.AddAffectedRequirement();
        affectedRequirement.Title = "Payment response requirement";
        affectedRequirement.Description = "Update response schema expectations.";
        affectedRequirement.Severity = ImpactSeverity.High;
        affectedRequirement.RelatedContextFragmentIds.Add(fragment.Id);
        affectedRequirement.Notes = "Check consumer contracts.";

        var risk = impactMap.AddRisk();
        risk.Title = "Client compatibility risk";
        risk.Description = "Existing clients may depend on the old response.";
        risk.Severity = ImpactSeverity.Medium;
        risk.RelatedContextFragmentIds.Add(fragment.Id);
        risk.Notes = "Coordinate rollout.";

        var evaluation = new ExpertEvaluation
        {
            AnalysisId = analysisId,
            ContextSufficiency = ContextSufficiencyRating.PartiallySufficient,
            ResultUsefulness = ResultUsefulnessRating.Useful,
            GeneralComment = "AI result is useful but needs an added rollout note."
        };
        evaluation.EvaluatedItems.Add(new ExpertEvaluatedItem
        {
            TargetType = ExpertEvaluationTargetType.ImpactItem,
            TargetId = affectedRequirement.Id,
            Mark = ExpertMark.Confirmed,
            Comment = "Requirement impact is correct.",
            CorrectionText = string.Empty
        });
        evaluation.MissedItems.Add(new ExpertMissedItem
        {
            ItemType = ImpactMapItemType.AffectedProjectDecision,
            Title = "Missing rollout note",
            Description = "Deployment sequencing should be documented.",
            Severity = ImpactSeverity.Low,
            Comment = "Add in implementation planning."
        });
        evaluation.Corrections.Add(new ExpertCorrection
        {
            TargetType = ExpertEvaluationTargetType.ImpactItem,
            TargetId = risk.Id,
            ItemType = ImpactMapItemType.Risk,
            Text = "Clarify compatibility risk for API consumers.",
            Comment = "Risk wording should be more specific."
        });

        var analysis = new Analysis
        {
            Id = analysisId,
            Title = "Payment API change",
            Status = AnalysisStatus.ExpertConclusionFixed,
            OriginalDescription = "Change payment API response.",
            ProjectRequest = "Add new status field to payment API response.",
            SituationDescription = "Several consumers parse the payment response.",
            ChangeSource = "RFC-42",
            CreatedAt = fixedAt.AddHours(-2),
            UpdatedAt = fixedAt,
            AiAnalysisResult = new AiAnalysisResult
            {
                AnalysisId = analysisId,
                Status = AiAnalysisResultStatus.Completed,
                GeneratedAt = fixedAt.AddMinutes(-10),
                EngineName = "demo-engine",
                ProviderName = "demo-provider",
                ModelName = "demo-model",
                PromptVersion = "mvp-v1",
                InputSnapshot = "{ \"request\": \"payment-api\" }",
                RawResponse = "{ \"status\": \"completed\" }",
                ImpactMap = impactMap,
                ErrorMessage = string.Empty
            },
            ExpertEvaluation = evaluation,
            ExpertConclusion = new ExpertConclusion
            {
                AnalysisId = analysisId,
                ConclusionType = ExpertConclusionType.AcceptWithLimitations,
                Comment = "Human expert conclusion",
                Rationale = "Accept after documenting rollout constraints.",
                FixedAt = fixedAt
            }
        };
        analysis.ContextFragments.Add(fragment);

        return analysis;
    }

    private static Analysis CreateReadyForExternalAnalysisGraph()
    {
        var analysisId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero);
        var analysis = new Analysis
        {
            Id = analysisId,
            Title = "Saved mock external result",
            Status = AnalysisStatus.ReadyForAnalysis,
            OriginalDescription = "Change integration boundary for a saved mock external result.",
            ProjectRequest = "Assess the impact of a deterministic mock external change.",
            SituationDescription = "The project uses local demo context for reproducible analysis.",
            ChangeSource = "Local demo RFC",
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        analysis.ContextFragments.Add(new ContextFragment
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Type = ContextFragmentType.ArchitecturalConstraint,
            Source = "Local architecture note",
            Text = "Keep integration boundaries explicit and require human expert review.",
            CreatedAt = createdAt.AddMinutes(10)
        });

        return analysis;
    }

    private static void ReverseImpactMapItems(ImpactMap impactMap, string fieldName)
    {
        var field = typeof(ImpactMap).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        var items = Assert.IsType<List<ImpactMapItem>>(field?.GetValue(impactMap));

        items.Reverse();
    }

    private static void DeleteDatabase(string databasePath)
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private sealed record SavedMockExternalAnalysis(
        Guid AnalysisId,
        CountingAiAnalysisEngine DirectEngine,
        CountingAiAnalysisEngine ExternalEngine,
        ProfiledCountingMockExternalRagAdapter Adapter,
        CountingAiAnalysisEngineSelector Selector);

    private sealed class ProfiledCountingMockExternalRagAdapter(string? profileName) : IExternalRagAdapter
    {
        private readonly MockExternalRagAdapter inner = new();

        public int CallCount { get; private set; }

        public Task<ExternalRagAdapterResponse> AnalyzeAsync(
            ExternalRagAdapterRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            var profiledRequest = request with
            {
                ExecutionMetadata = request.ExecutionMetadata with
                {
                    RequestedProfileName = profileName
                }
            };

            return inner.AnalyzeAsync(profiledRequest, cancellationToken);
        }
    }

    private sealed class CountingAiAnalysisEngine(IAiAnalysisEngine inner) : IAiAnalysisEngine
    {
        public int CallCount { get; private set; }

        public Task<AiAnalysisResponse> AnalyzeAsync(
            AiAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return inner.AnalyzeAsync(request, cancellationToken);
        }
    }

    private sealed class ThrowingAiAnalysisEngine : IAiAnalysisEngine
    {
        public Task<AiAnalysisResponse> AnalyzeAsync(
            AiAnalysisRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Direct LLM engine must not be used by external-mode export tests.");
    }

    private sealed class CountingAiAnalysisEngineSelector(
        IAiAnalysisEngine directLlmAnalysisEngine,
        IAiAnalysisEngine externalRagAnalysisEngine) : IAiAnalysisEngineSelector
    {
        public List<AnalysisMode> SelectedModes { get; } = [];

        public IAiAnalysisEngine Select(AnalysisMode analysisMode)
        {
            SelectedModes.Add(analysisMode);

            return analysisMode switch
            {
                AnalysisMode.DirectLlm => directLlmAnalysisEngine,
                AnalysisMode.ExternalRag => externalRagAnalysisEngine,
                _ => throw new ArgumentOutOfRangeException(nameof(analysisMode), analysisMode, "Unsupported test analysis mode.")
            };
        }
    }
}

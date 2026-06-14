using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Application.Export;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;
using RequirementImpactAssistant.Web.Pages.Analyses;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class AnalysisMarkdownExportServiceTests
{
    [Fact]
    public async Task ExportAsync_BuildsSnapshotStyleMarkdownReportFromSavedAnalysis()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();
            var exportedAt = new DateTimeOffset(2026, 06, 13, 14, 15, 16, TimeSpan.Zero);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = new AnalysisMarkdownExportService(dbContext);

                var result = await service.ExportAsync(analysis.Id, exportedAt);

                Assert.Equal(AnalysisMarkdownExportResultKind.Exported, result.Kind);
                Assert.EndsWith(".md", result.FileName, StringComparison.Ordinal);
                Assert.Contains("payment-api-change-", result.FileName, StringComparison.Ordinal);
                AssertMarkdownContainsSnapshotSections(result.Markdown, analysis.ContextFragments.Single().Id);
            }
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

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = new AnalysisMarkdownExportService(dbContext);

                var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

                Assert.Equal(AnalysisMarkdownExportResultKind.Unavailable, result.Kind);
                Assert.Empty(result.Markdown);
                Assert.Contains("expert conclusion", result.Message, StringComparison.OrdinalIgnoreCase);
            }
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

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = new AnalysisMarkdownExportService(dbContext);

                var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

                Assert.Equal(AnalysisMarkdownExportResultKind.Exported, result.Kind);
                Assert.Contains("Human expert conclusion", result.Markdown);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExportAsync_KeepsMarkdownReportStructuredWhenContextFragmentContainsBacktickFence()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();
            analysis.ContextFragments.Single().Text = """
                Before saved markdown fence.
                ```text
                ## Spoofed section inside fragment
                ```
                After saved markdown fence.
                """;

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = new AnalysisMarkdownExportService(dbContext);

                var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

                Assert.Equal(AnalysisMarkdownExportResultKind.Exported, result.Kind);
                var contentLabelIndex = result.Markdown.IndexOf("**Content:**", StringComparison.Ordinal);
                var openingFenceIndex = result.Markdown.IndexOf("````text", contentLabelIndex, StringComparison.Ordinal);
                var innerFenceIndex = result.Markdown.IndexOf("```text", openingFenceIndex + "````text".Length, StringComparison.Ordinal);
                var closingFenceIndex = result.Markdown.IndexOf($"{Environment.NewLine}````{Environment.NewLine}", innerFenceIndex, StringComparison.Ordinal);
                var nextSectionIndex = result.Markdown.IndexOf("## Structured impact map", StringComparison.Ordinal);

                Assert.True(contentLabelIndex >= 0);
                Assert.True(openingFenceIndex > contentLabelIndex);
                Assert.True(innerFenceIndex > openingFenceIndex);
                Assert.True(closingFenceIndex > innerFenceIndex);
                Assert.True(nextSectionIndex > closingFenceIndex);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public void Build_ExportsAvailableRetrievedContextItemsWithSavedMetadata()
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
            ManualContextForwardedToExternalAiOrRag = true,
            RetrievedContextItems =
            [
                new RetrievedContextItem
                {
                    SourceTitle = "API change request",
                    SourceId = "REQ-17",
                    ExternalReference = "external-doc-17",
                    FragmentId = "chunk-03",
                    Text = """
                        Full retrieved fragment text.
                        ```text
                        Saved fence inside retrieved text.
                        ```
                        """,
                    UrlOrReference = "kb://requirements/REQ-17",
                    Rank = 1,
                    Score = 0.94,
                    ProviderName = "neutral-provider",
                    AdapterName = "neutral-adapter",
                    Completeness = RetrievedContextItemCompleteness.FullText
                },
                new RetrievedContextItem
                {
                    SourceTitle = "Integration inventory",
                    ExternalReference = "inventory-record-42",
                    UrlOrReference = "kb://inventory/42",
                    Rank = 2,
                    Score = 0.81,
                    ProviderName = "neutral-provider",
                    AdapterName = "neutral-adapter",
                    Completeness = RetrievedContextItemCompleteness.MetadataOnly,
                    WarningOrLimitationNote = "Only source metadata was returned."
                }
            ]
        };

        var markdown = new AnalysisMarkdownReportBuilder().Build(analysis, DateTimeOffset.UtcNow);

        Assert.Contains("## Retrieved context", markdown);
        Assert.Contains("- **State:** Available", markdown);
        Assert.Contains("Retrieved context was saved for this analysis result.", markdown);
        Assert.Contains("### Retrieved context item 1: API change request", markdown);
        Assert.Contains("- **Source title:** API change request", markdown);
        Assert.Contains("- **Source id:** REQ-17", markdown);
        Assert.Contains("- **External reference:** external-doc-17", markdown);
        Assert.Contains("- **Fragment id:** chunk-03", markdown);
        Assert.Contains("- **URL or reference:** kb://requirements/REQ-17", markdown);
        Assert.Contains("- **Rank:** 1", markdown);
        Assert.Contains("- **Score:** 0.94", markdown);
        Assert.Contains("- **Provider:** neutral-provider", markdown);
        Assert.Contains("- **Adapter:** neutral-adapter", markdown);
        Assert.Contains("- **Completeness:** FullText", markdown);
        Assert.Contains("````text", markdown);
        Assert.Contains("Saved fence inside retrieved text.", markdown);
        Assert.Contains("### Retrieved context item 2: Integration inventory", markdown);
        Assert.Contains("- **Completeness:** MetadataOnly", markdown);
        Assert.Contains("- **Warning or limitation note:** Only source metadata was returned.", markdown);
        Assert.Contains("- **Text:** Not provided", markdown);
        Assert.Contains("- **Excerpt:** Not provided", markdown);
    }

    [Fact]
    public void Build_ExportsPartialRetrievedContextItemWithExcerptAndLimitation()
    {
        var analysis = CreateAnalysisGraph();
        analysis.AiAnalysisResult!.Metadata = new AiAnalysisResultMetadata
        {
            AnalysisMode = AnalysisMode.ExternalRag,
            EngineName = "external-analysis-engine",
            AdapterName = "neutral-adapter",
            RetrievedContextState = RetrievedContextState.Partial,
            Warnings = ["Retrieved context is partial."],
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
            ]
        };

        var markdown = new AnalysisMarkdownReportBuilder().Build(analysis, DateTimeOffset.UtcNow);

        Assert.Contains("- **State:** Partial", markdown);
        Assert.Contains(
            "Retrieved context was saved only partially; review item completeness and limitation notes.",
            markdown);
        Assert.Contains("### Retrieved context item 1: Requirement summary", markdown);
        Assert.Contains("- **Completeness:** ExcerptOnly", markdown);
        Assert.Contains(
            "- **Warning or limitation note:** Full text was not returned by the external engine.",
            markdown);
        Assert.Contains("**Excerpt:**", markdown);
        Assert.Contains("Only an excerpt was returned.", markdown);
        Assert.Contains("- Retrieved context is partial.", markdown);
    }

    [Fact]
    public void Build_ExportsMetadataOnlyRetrievedContextItemWithoutTextOrExcerpt()
    {
        var analysis = CreateAnalysisGraph();
        analysis.AiAnalysisResult!.Metadata = new AiAnalysisResultMetadata
        {
            AnalysisMode = AnalysisMode.ExternalRag,
            EngineName = "external-analysis-engine",
            ProviderName = "neutral-provider",
            AdapterName = "neutral-adapter",
            RetrievedContextState = RetrievedContextState.MetadataOnly,
            RetrievedContextItems =
            [
                new RetrievedContextItem
                {
                    SourceTitle = "Integration inventory",
                    ExternalReference = "inventory-record-42",
                    UrlOrReference = "kb://inventory/42",
                    Rank = 2,
                    Score = 0.81,
                    ProviderName = "neutral-provider",
                    AdapterName = "neutral-adapter",
                    Completeness = RetrievedContextItemCompleteness.MetadataOnly,
                    WarningOrLimitationNote = "Only source metadata was returned."
                }
            ]
        };

        var markdown = new AnalysisMarkdownReportBuilder().Build(analysis, DateTimeOffset.UtcNow);

        Assert.Contains("## Retrieved context", markdown);
        Assert.Contains("- **State:** MetadataOnly", markdown);
        Assert.Contains(
            "Only retrieved context metadata was saved; full text or excerpts may be unavailable.",
            markdown);
        Assert.Contains("### Retrieved context item 1: Integration inventory", markdown);
        Assert.Contains("- **External reference:** inventory-record-42", markdown);
        Assert.Contains("- **URL or reference:** kb://inventory/42", markdown);
        Assert.Contains("- **Score:** 0.81", markdown);
        Assert.Contains("- **Completeness:** MetadataOnly", markdown);
        Assert.Contains("- **Warning or limitation note:** Only source metadata was returned.", markdown);
        Assert.Contains("- **Text:** Not provided", markdown);
        Assert.Contains("- **Excerpt:** Not provided", markdown);
    }

    [Fact]
    public void Build_ExportsUnavailableRetrievedContextWithoutArtificialItems()
    {
        var analysis = CreateAnalysisGraph();
        analysis.AiAnalysisResult!.Metadata = new AiAnalysisResultMetadata
        {
            AnalysisMode = AnalysisMode.ExternalRag,
            EngineName = "external-analysis-engine",
            RetrievedContextState = RetrievedContextState.Unavailable
        };

        var markdown = new AnalysisMarkdownReportBuilder().Build(analysis, DateTimeOffset.UtcNow);

        Assert.Contains("## Retrieved context", markdown);
        Assert.Contains("- **State:** Unavailable", markdown);
        Assert.Contains("Retrieved context is unavailable for this saved analysis result.", markdown);
        Assert.Contains("No retrieved context items were saved.", markdown);
        Assert.DoesNotContain("### Retrieved context item 1:", markdown);
    }

    [Fact]
    public async Task ExportAsync_ExportsLegacyMvp0ResultMetadataWithDirectLlmFallbacks()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph(includeStage1Metadata: false);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = new AnalysisMarkdownExportService(dbContext);

                var result = await service.ExportAsync(analysis.Id, DateTimeOffset.UtcNow);

                Assert.Equal(AnalysisMarkdownExportResultKind.Exported, result.Kind);
                Assert.Contains("## Analysis result metadata", result.Markdown);
                Assert.Contains("- **Analysis mode:** DirectLlm", result.Markdown);
                Assert.Contains("- **Engine:** demo-engine", result.Markdown);
                Assert.Contains("- **Provider:** demo-provider", result.Markdown);
                Assert.Contains("- **Adapter:** Not provided", result.Markdown);
                Assert.Contains("- **Model workflow profile:** demo-model", result.Markdown);
                Assert.Contains("- **Manual context forwarded to external AI or RAG:** False", result.Markdown);
                Assert.Contains("- **Retrieved context state:** Unavailable", result.Markdown);
                Assert.Contains("No warnings were saved.", result.Markdown);
                Assert.Contains("## Retrieved context", result.Markdown);
                Assert.Contains("- **State:** Unavailable", result.Markdown);
                Assert.Contains("No retrieved context items were saved.", result.Markdown);
                Assert.DoesNotContain("### Retrieved context item 1:", result.Markdown);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task DetailsPage_ExportsMarkdownFileFromUiHandler()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = new AnalysisMarkdownExportService(dbContext);
                var pageModel = new DetailsModel(dbContext, markdownExportService: service);

                var result = await pageModel.OnGetExportMarkdownAsync(analysis.Id);

                var fileResult = Assert.IsType<FileContentResult>(result);
                Assert.Equal(AnalysisMarkdownExportService.ContentType, fileResult.ContentType);
                Assert.EndsWith(".md", fileResult.FileDownloadName, StringComparison.Ordinal);
                var markdown = Encoding.UTF8.GetString(fileResult.FileContents);
                Assert.Contains("# Payment API change", markdown);
                Assert.Contains("## Expert conclusion", markdown);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static void AssertMarkdownContainsSnapshotSections(string markdown, Guid contextFragmentId)
    {
        Assert.Contains("# Payment API change", markdown);
        Assert.Contains("## Export metadata", markdown);
        Assert.Contains("- **Exported at:** 2026-06-13 14:15:16 UTC", markdown);
        Assert.Contains("- **Analysis status:** ExpertConclusionFixed", markdown);
        Assert.Contains("## Analysis result metadata", markdown);
        Assert.Contains("- **Analysis mode:** DirectLlm", markdown);
        Assert.Contains("- **Engine:** direct-llm-engine", markdown);
        Assert.Contains("- **Provider:** direct-provider", markdown);
        Assert.Contains("- **Adapter:** Not provided", markdown);
        Assert.Contains("- **Model workflow profile:** demo-deterministic", markdown);
        Assert.Contains("- **Manual context forwarded to external AI or RAG:** False", markdown);
        Assert.Contains("- **Retrieved context state:** Unavailable", markdown);
        Assert.Contains("### Warnings", markdown);
        Assert.Contains("- Saved warning from direct LLM metadata.", markdown);
        Assert.Contains("## Retrieved context", markdown);
        Assert.Contains("- **State:** Unavailable", markdown);
        Assert.Contains("Retrieved context is unavailable for this saved analysis result.", markdown);
        Assert.Contains("No retrieved context items were saved.", markdown);
        Assert.Contains("## Input", markdown);
        Assert.Contains("**Original requirement:**", markdown);
        Assert.Contains("Change payment API response.", markdown);
        Assert.Contains("**Proposed change:**", markdown);
        Assert.Contains("Add new status field to payment API response.", markdown);
        Assert.Contains("## Context fragments", markdown);
        Assert.Contains($"- **Identifier:** {contextFragmentId}", markdown);
        Assert.Contains("Payment API response contract changes.", markdown);
        Assert.Contains("## Structured impact map", markdown);
        Assert.Contains("### Risks", markdown);
        Assert.Contains("Client compatibility risk", markdown);
        Assert.Contains("### Contradictions", markdown);
        Assert.Contains("### Clarification questions", markdown);
        Assert.Contains("### Missing information", markdown);
        Assert.Contains("## Expert evaluation", markdown);
        Assert.Contains("- **Context sufficiency:** PartiallySufficient", markdown);
        Assert.Contains("- **Result usefulness:** Useful", markdown);
        Assert.Contains("### Expert marks", markdown);
        Assert.Contains("### Missed items", markdown);
        Assert.Contains("Missing rollout note", markdown);
        Assert.Contains("### Expert corrections", markdown);
        Assert.Contains("Clarify compatibility risk for API consumers.", markdown);
        Assert.Contains("## Expert conclusion", markdown);
        Assert.Contains("- **Conclusion:** AcceptWithLimitations", markdown);
        Assert.Contains("Human expert conclusion", markdown);
        Assert.Contains("Accept after documenting rollout constraints.", markdown);
        Assert.Contains("- **Fixed at:** 2026-06-13 12:30:00 UTC", markdown);
        Assert.Contains("## Decision boundary", markdown);
        Assert.Contains("does not make a management decision", markdown);
    }

    private static string CreateDatabasePath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"requirement-impact-assistant-export-{Guid.NewGuid():N}.db");

    private static DbContextOptions<ApplicationDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

    private static Analysis CreateAnalysisGraph(bool includeStage1Metadata = true)
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

        if (includeStage1Metadata)
        {
            analysis.AiAnalysisResult.Metadata = AiAnalysisResultMetadata.CreateDefaultDirectLlm(
                "direct-llm-engine",
                "direct-provider",
                "demo-deterministic",
                ["Saved warning from direct LLM metadata."]);
        }

        return analysis;
    }

    private static void DeleteDatabase(string databasePath)
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Tests.Data;

public sealed class ApplicationDbContextPersistenceTests
{
    private const string InitialMvpSchemaMigration = "20260613120005_InitialMvpSchema";

    [Fact]
    public async Task MvpDataGraph_CanBeSavedAndLoadedFromSqlite()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"requirement-impact-assistant-{Guid.NewGuid():N}.db");

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();
            var analysisId = analysis.Id;
            var fragmentId = analysis.ContextFragments.Single().Id;
            var affectedRequirementId = analysis.AiAnalysisResult!
                .ImpactMap!
                .AffectedRequirements
                .Single()
                .Id;
            var riskId = analysis.AiAnalysisResult
                .ImpactMap
                .Risks
                .Single()
                .Id;

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);

                await dbContext.SaveChangesAsync();
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM RetrievedContextItems;";

                var retrievedContextItemCount = (long)(await command.ExecuteScalarAsync() ?? 0L);
                Assert.Equal(0L, retrievedContextItemCount);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var loadedAnalysis = await dbContext.Analyses
                    .AsSplitQuery()
                    .Include(item => item.ContextFragments)
                    .Include(item => item.AiAnalysisResult)
                    .Include(item => item.ExpertEvaluation)
                        .ThenInclude(item => item!.EvaluatedItems)
                    .Include(item => item.ExpertEvaluation)
                        .ThenInclude(item => item!.MissedItems)
                    .Include(item => item.ExpertEvaluation)
                        .ThenInclude(item => item!.Corrections)
                    .Include(item => item.ExpertConclusion)
                    .SingleAsync(item => item.Id == analysisId);

                Assert.Equal("Payment API change", loadedAnalysis.Title);
                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, loadedAnalysis.Status);
                Assert.Equal("RFC-42", loadedAnalysis.ChangeSource);

                var loadedFragment = Assert.Single(loadedAnalysis.ContextFragments);
                Assert.Equal(fragmentId, loadedFragment.Id);
                Assert.Equal(ContextFragmentType.ApiDescription, loadedFragment.Type);
                Assert.Equal("OpenAPI fragment", loadedFragment.Source);

                Assert.NotNull(loadedAnalysis.AiAnalysisResult);
                var loadedResult = loadedAnalysis.AiAnalysisResult;
                Assert.Equal(AiAnalysisResultStatus.Completed, loadedResult.Status);
                Assert.Equal("demo-engine", loadedResult.EngineName);

                Assert.NotNull(loadedResult.ImpactMap);
                var loadedImpactMap = loadedResult.ImpactMap;
                Assert.Equal("Scope update", loadedImpactMap.ChangeSummary.Title);
                Assert.Equal("Requires expert review", loadedImpactMap.PreliminaryAssessment.Title);

                var loadedAffectedRequirement = Assert.Single(loadedImpactMap.AffectedRequirements);
                Assert.Equal(affectedRequirementId, loadedAffectedRequirement.Id);
                Assert.Equal("affected-requirement-001", loadedAffectedRequirement.Id);
                Assert.Equal(fragmentId, Assert.Single(loadedAffectedRequirement.RelatedContextFragmentIds));

                var loadedRisk = Assert.Single(loadedImpactMap.Risks);
                Assert.Equal(riskId, loadedRisk.Id);
                Assert.Equal("risk-001", loadedRisk.Id);

                Assert.NotNull(loadedAnalysis.ExpertEvaluation);
                var loadedEvaluation = loadedAnalysis.ExpertEvaluation;
                Assert.Equal(ContextSufficiencyRating.PartiallySufficient, loadedEvaluation.ContextSufficiency);
                Assert.Equal(ResultUsefulnessRating.Useful, loadedEvaluation.ResultUsefulness);
                Assert.Equal(affectedRequirementId, Assert.Single(loadedEvaluation.EvaluatedItems).TargetId);
                Assert.Equal(riskId, Assert.Single(loadedEvaluation.Corrections).TargetId);
                Assert.Equal("Missing rollout note", Assert.Single(loadedEvaluation.MissedItems).Title);

                Assert.NotNull(loadedAnalysis.ExpertConclusion);
                var loadedConclusion = loadedAnalysis.ExpertConclusion;
                Assert.Equal(ExpertConclusionType.AcceptWithLimitations, loadedConclusion.ConclusionType);
                Assert.Equal("Human expert conclusion", loadedConclusion.Comment);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task Stage1AiAnalysisResultMetadata_CanBeSavedAndLoadedFromSqlite()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"requirement-impact-assistant-stage1-{Guid.NewGuid():N}.db");

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();
            var analysisId = analysis.Id;
            var result = analysis.AiAnalysisResult!;
            result.Metadata = new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = "external-rag-engine",
                ProviderName = "neutral-provider",
                AdapterName = "neutral-adapter",
                ModelWorkflowProfileName = "impact-workflow-profile",
                RetrievedContextState = RetrievedContextState.Partial,
                Warnings =
                [
                    "Retrieved context was partially available.",
                    "Adapter returned normalized metadata."
                ],
                ManualContextForwardedToExternalAiOrRag = true,
                RetrievedContextItems =
                [
                    new RetrievedContextItem
                    {
                        SourceTitle = "Gateway requirements note",
                        SourceId = "REQ-GW-001",
                        ExternalReference = "external-ref-42",
                        FragmentId = "chunk-7",
                        Text = null,
                        Excerpt = "Gateway consumers depend on the existing response contract.",
                        UrlOrReference = "kb://gateway/requirements",
                        Rank = 1,
                        Score = 0.87,
                        ProviderName = "neutral-provider",
                        AdapterName = "neutral-adapter",
                        Completeness = RetrievedContextItemCompleteness.ExcerptOnly,
                        WarningOrLimitationNote = "Full text was not returned by the external circuit."
                    }
                ]
            };

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);

                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var loadedAnalysis = await dbContext.Analyses
                    .AsSplitQuery()
                    .Include(item => item.AiAnalysisResult)
                    .SingleAsync(item => item.Id == analysisId);

                Assert.NotNull(loadedAnalysis.AiAnalysisResult);
                var loadedMetadata = loadedAnalysis.AiAnalysisResult.Metadata;

                Assert.Equal(AnalysisMode.ExternalRag, loadedMetadata.AnalysisMode);
                Assert.Equal("external-rag-engine", loadedMetadata.EngineName);
                Assert.Equal("neutral-provider", loadedMetadata.ProviderName);
                Assert.Equal("neutral-adapter", loadedMetadata.AdapterName);
                Assert.Equal("impact-workflow-profile", loadedMetadata.ModelWorkflowProfileName);
                Assert.Equal(RetrievedContextState.Partial, loadedMetadata.RetrievedContextState);
                Assert.True(loadedMetadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Equal(
                    ["Retrieved context was partially available.", "Adapter returned normalized metadata."],
                    loadedMetadata.Warnings);

                var loadedItem = Assert.Single(loadedMetadata.RetrievedContextItems);
                Assert.Equal("Gateway requirements note", loadedItem.SourceTitle);
                Assert.Equal("REQ-GW-001", loadedItem.SourceId);
                Assert.Equal("external-ref-42", loadedItem.ExternalReference);
                Assert.Equal("chunk-7", loadedItem.FragmentId);
                Assert.Null(loadedItem.Text);
                Assert.Equal(
                    "Gateway consumers depend on the existing response contract.",
                    loadedItem.Excerpt);
                Assert.Equal("kb://gateway/requirements", loadedItem.UrlOrReference);
                Assert.Equal(1, loadedItem.Rank);
                Assert.Equal(0.87, loadedItem.Score);
                Assert.Equal("neutral-provider", loadedItem.ProviderName);
                Assert.Equal("neutral-adapter", loadedItem.AdapterName);
                Assert.Equal(RetrievedContextItemCompleteness.ExcerptOnly, loadedItem.Completeness);
                Assert.Equal(
                    "Full text was not returned by the external circuit.",
                    loadedItem.WarningOrLimitationNote);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task ExternalRagResultWithAvailableRetrievedContext_CanBeSavedAndLoadedFromSqlite()
    {
        var metadata = CreateExternalRagMetadata(
            RetrievedContextState.Available,
            manualContextForwarded: true);
        metadata.ProviderName = null;
        metadata.AdapterName = null;
        metadata.ModelWorkflowProfileName = null;
        metadata.RetrievedContextItems.Add(new RetrievedContextItem
        {
            SourceTitle = "Anonymized requirements package",
            SourceId = "REQ-001",
            ExternalReference = "external-ref-001",
            FragmentId = "fragment-001",
            Text = "The request can affect validation rules and integration contracts.",
            Excerpt = "Validation rules and integration contracts.",
            UrlOrReference = "kb://requirements/req-001",
            Rank = 1,
            Score = 0.91,
            ProviderName = null,
            AdapterName = null,
            Completeness = RetrievedContextItemCompleteness.FullText,
            WarningOrLimitationNote = null
        });

        var loadedMetadata = await SaveAndLoadExternalRagMetadataAsync(
            metadata,
            "external-rag-available");

        Assert.Equal(AnalysisMode.ExternalRag, loadedMetadata.AnalysisMode);
        Assert.Equal("external-rag-shape", loadedMetadata.EngineName);
        Assert.Null(loadedMetadata.ProviderName);
        Assert.Null(loadedMetadata.AdapterName);
        Assert.Null(loadedMetadata.ModelWorkflowProfileName);
        Assert.Equal(RetrievedContextState.Available, loadedMetadata.RetrievedContextState);
        Assert.True(loadedMetadata.ManualContextForwardedToExternalAiOrRag);
        Assert.Empty(loadedMetadata.Warnings);

        var loadedItem = Assert.Single(loadedMetadata.RetrievedContextItems);
        Assert.Equal("Anonymized requirements package", loadedItem.SourceTitle);
        Assert.Equal("REQ-001", loadedItem.SourceId);
        Assert.Equal("external-ref-001", loadedItem.ExternalReference);
        Assert.Equal("fragment-001", loadedItem.FragmentId);
        Assert.Equal(
            "The request can affect validation rules and integration contracts.",
            loadedItem.Text);
        Assert.Equal("Validation rules and integration contracts.", loadedItem.Excerpt);
        Assert.Equal("kb://requirements/req-001", loadedItem.UrlOrReference);
        Assert.Equal(1, loadedItem.Rank);
        Assert.Equal(0.91, loadedItem.Score);
        Assert.Null(loadedItem.ProviderName);
        Assert.Null(loadedItem.AdapterName);
        Assert.Equal(RetrievedContextItemCompleteness.FullText, loadedItem.Completeness);
        Assert.Null(loadedItem.WarningOrLimitationNote);
    }

    [Fact]
    public async Task ExternalRagResultWithMetadataOnlyRetrievedContext_CanBeSavedAndLoadedWithoutFragmentText()
    {
        var metadata = CreateExternalRagMetadata(
            RetrievedContextState.MetadataOnly,
            manualContextForwarded: false);
        metadata.ProviderName = "neutral-provider";
        metadata.AdapterName = "neutral-adapter";
        metadata.ModelWorkflowProfileName = "neutral-workflow-profile";
        metadata.RetrievedContextItems.Add(new RetrievedContextItem
        {
            SourceTitle = "Anonymized architecture note",
            SourceId = "ARCH-001",
            ExternalReference = "external-ref-002",
            FragmentId = "fragment-002",
            Text = null,
            Excerpt = null,
            UrlOrReference = "kb://architecture/arch-001",
            Rank = 2,
            Score = 0.74,
            ProviderName = "neutral-provider",
            AdapterName = "neutral-adapter",
            Completeness = RetrievedContextItemCompleteness.MetadataOnly,
            WarningOrLimitationNote = "External circuit returned source metadata without fragment text."
        });

        var loadedMetadata = await SaveAndLoadExternalRagMetadataAsync(
            metadata,
            "external-rag-metadata-only");

        Assert.Equal(AnalysisMode.ExternalRag, loadedMetadata.AnalysisMode);
        Assert.Equal("external-rag-shape", loadedMetadata.EngineName);
        Assert.Equal("neutral-provider", loadedMetadata.ProviderName);
        Assert.Equal("neutral-adapter", loadedMetadata.AdapterName);
        Assert.Equal("neutral-workflow-profile", loadedMetadata.ModelWorkflowProfileName);
        Assert.Equal(RetrievedContextState.MetadataOnly, loadedMetadata.RetrievedContextState);
        Assert.False(loadedMetadata.ManualContextForwardedToExternalAiOrRag);

        var loadedItem = Assert.Single(loadedMetadata.RetrievedContextItems);
        Assert.Equal("Anonymized architecture note", loadedItem.SourceTitle);
        Assert.Equal("ARCH-001", loadedItem.SourceId);
        Assert.Equal("external-ref-002", loadedItem.ExternalReference);
        Assert.Equal("fragment-002", loadedItem.FragmentId);
        Assert.Null(loadedItem.Text);
        Assert.Null(loadedItem.Excerpt);
        Assert.Equal("kb://architecture/arch-001", loadedItem.UrlOrReference);
        Assert.Equal(2, loadedItem.Rank);
        Assert.Equal(0.74, loadedItem.Score);
        Assert.Equal("neutral-provider", loadedItem.ProviderName);
        Assert.Equal("neutral-adapter", loadedItem.AdapterName);
        Assert.Equal(RetrievedContextItemCompleteness.MetadataOnly, loadedItem.Completeness);
        Assert.Equal(
            "External circuit returned source metadata without fragment text.",
            loadedItem.WarningOrLimitationNote);
    }

    [Fact]
    public async Task ExternalRagResultWithUnavailableRetrievedContext_CanBeSavedAndLoadedWithLimitation()
    {
        var metadata = CreateExternalRagMetadata(
            RetrievedContextState.Unavailable,
            manualContextForwarded: false);
        metadata.Warnings.Add("Retrieved context was unavailable in the external result.");

        var loadedMetadata = await SaveAndLoadExternalRagMetadataAsync(
            metadata,
            "external-rag-unavailable");

        Assert.Equal(AnalysisMode.ExternalRag, loadedMetadata.AnalysisMode);
        Assert.Equal(RetrievedContextState.Unavailable, loadedMetadata.RetrievedContextState);
        Assert.False(loadedMetadata.ManualContextForwardedToExternalAiOrRag);
        Assert.Equal(
            ["Retrieved context was unavailable in the external result."],
            loadedMetadata.Warnings);
        Assert.Empty(loadedMetadata.RetrievedContextItems);
    }

    [Fact]
    public async Task ExternalRagResultWithPartialRetrievedContext_CanBeSavedAndLoadedWithWarning()
    {
        var metadata = CreateExternalRagMetadata(
            RetrievedContextState.Partial,
            manualContextForwarded: true);
        metadata.Warnings.Add("Only part of the external retrieved context was returned.");
        metadata.RetrievedContextItems.Add(new RetrievedContextItem
        {
            SourceTitle = "Anonymized integration note",
            SourceId = "INT-001",
            ExternalReference = "external-ref-003",
            FragmentId = "fragment-003",
            Excerpt = "Downstream consumers may require coordinated validation.",
            Rank = 1,
            Score = 0.82,
            Completeness = RetrievedContextItemCompleteness.ExcerptOnly,
            WarningOrLimitationNote = "Full source text was not returned."
        });

        var loadedMetadata = await SaveAndLoadExternalRagMetadataAsync(
            metadata,
            "external-rag-partial");

        Assert.Equal(AnalysisMode.ExternalRag, loadedMetadata.AnalysisMode);
        Assert.Equal(RetrievedContextState.Partial, loadedMetadata.RetrievedContextState);
        Assert.True(loadedMetadata.ManualContextForwardedToExternalAiOrRag);
        Assert.Equal(
            ["Only part of the external retrieved context was returned."],
            loadedMetadata.Warnings);

        var loadedItem = Assert.Single(loadedMetadata.RetrievedContextItems);
        Assert.Equal("Anonymized integration note", loadedItem.SourceTitle);
        Assert.Null(loadedItem.Text);
        Assert.Equal(
            "Downstream consumers may require coordinated validation.",
            loadedItem.Excerpt);
        Assert.Equal(RetrievedContextItemCompleteness.ExcerptOnly, loadedItem.Completeness);
        Assert.Equal("Full source text was not returned.", loadedItem.WarningOrLimitationNote);
    }

    [Fact]
    public async Task DirectLlmResult_CanBeSavedAndLoadedWithoutManualContextForwardingOrRetrievedContext()
    {
        var metadata = AiAnalysisResultMetadata.CreateDefaultDirectLlm(
            "direct-llm-engine",
            "demo-provider",
            "demo-model");

        var loadedMetadata = await SaveAndLoadResultMetadataAsync(
            metadata,
            "direct-llm-compatible");

        Assert.Equal(AnalysisMode.DirectLlm, loadedMetadata.AnalysisMode);
        Assert.Equal("direct-llm-engine", loadedMetadata.EngineName);
        Assert.Equal("demo-provider", loadedMetadata.ProviderName);
        Assert.Null(loadedMetadata.AdapterName);
        Assert.Equal("demo-model", loadedMetadata.ModelWorkflowProfileName);
        Assert.Equal(RetrievedContextState.Unavailable, loadedMetadata.RetrievedContextState);
        Assert.False(loadedMetadata.ManualContextForwardedToExternalAiOrRag);
        Assert.Empty(loadedMetadata.Warnings);
        Assert.Empty(loadedMetadata.RetrievedContextItems);
    }

    [Fact]
    public async Task LegacyMvp0AiAnalysisResult_CanBeReadAfterStage1MigrationsWithoutSyntheticRetrievedContext()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"requirement-impact-assistant-legacy-{Guid.NewGuid():N}.db");
        var analysisId = Guid.NewGuid();
        var resultId = Guid.NewGuid();
        var fixedAt = new DateTimeOffset(2026, 06, 13, 13, 00, 00, TimeSpan.Zero);

        try
        {
            var options = CreateOptions(databasePath);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.GetService<IMigrator>().MigrateAsync(InitialMvpSchemaMigration);
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await InsertLegacyMvp0AnalysisAsync(connection, analysisId, resultId, fixedAt);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var loadedAnalysis = await dbContext.Analyses
                    .Include(item => item.AiAnalysisResult)
                    .SingleAsync(item => item.Id == analysisId);

                Assert.Equal("Legacy MVP-0 analysis", loadedAnalysis.Title);
                Assert.NotNull(loadedAnalysis.AiAnalysisResult);

                var loadedResult = loadedAnalysis.AiAnalysisResult;
                Assert.Equal(resultId, loadedResult.Id);
                Assert.Equal(AiAnalysisResultStatus.Completed, loadedResult.Status);
                Assert.Equal("legacy-engine", loadedResult.EngineName);
                Assert.Equal("legacy-provider", loadedResult.ProviderName);
                Assert.Equal("legacy-model", loadedResult.ModelName);

                var loadedMetadata = loadedResult.Metadata;
                Assert.Equal(AnalysisMode.DirectLlm, loadedMetadata.AnalysisMode);
                Assert.Equal("legacy-engine", loadedMetadata.EngineName);
                Assert.Equal("legacy-provider", loadedMetadata.ProviderName);
                Assert.Null(loadedMetadata.AdapterName);
                Assert.Equal("legacy-model", loadedMetadata.ModelWorkflowProfileName);
                Assert.Equal(RetrievedContextState.Unavailable, loadedMetadata.RetrievedContextState);
                Assert.False(loadedMetadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Empty(loadedMetadata.Warnings);
                Assert.Empty(loadedMetadata.RetrievedContextItems);
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM RetrievedContextItems;";

                var retrievedContextItemCount = (long)(await command.ExecuteScalarAsync() ?? 0L);
                Assert.Equal(0L, retrievedContextItemCount);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static DbContextOptions<ApplicationDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

    private static AiAnalysisResultMetadata CreateExternalRagMetadata(
        RetrievedContextState retrievedContextState,
        bool manualContextForwarded) =>
        new()
        {
            AnalysisMode = AnalysisMode.ExternalRag,
            EngineName = "external-rag-shape",
            ProviderName = "neutral-provider",
            AdapterName = "neutral-adapter",
            ModelWorkflowProfileName = "neutral-model-workflow-profile",
            RetrievedContextState = retrievedContextState,
            ManualContextForwardedToExternalAiOrRag = manualContextForwarded
        };

    private static Task<AiAnalysisResultMetadata> SaveAndLoadExternalRagMetadataAsync(
        AiAnalysisResultMetadata metadata,
        string databaseNamePrefix) =>
        SaveAndLoadResultMetadataAsync(metadata, databaseNamePrefix);

    private static async Task<AiAnalysisResultMetadata> SaveAndLoadResultMetadataAsync(
        AiAnalysisResultMetadata metadata,
        string databaseNamePrefix)
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"requirement-impact-assistant-{databaseNamePrefix}-{Guid.NewGuid():N}.db");

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysisGraph();
            var analysisId = analysis.Id;
            analysis.AiAnalysisResult!.Metadata = metadata;

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);

                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var loadedAnalysis = await dbContext.Analyses
                    .AsSplitQuery()
                    .Include(item => item.AiAnalysisResult)
                    .SingleAsync(item => item.Id == analysisId);

                return loadedAnalysis.AiAnalysisResult!.Metadata;
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static async Task InsertLegacyMvp0AnalysisAsync(
        SqliteConnection connection,
        Guid analysisId,
        Guid resultId,
        DateTimeOffset fixedAt)
    {
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO Analyses
                    (Id, Title, Status, OriginalDescription, ProjectRequest, SituationDescription, ChangeSource, CreatedAt, UpdatedAt, FixedAt)
                VALUES
                    ($id, $title, $status, $originalDescription, $projectRequest, $situationDescription, $changeSource, $createdAt, $updatedAt, NULL);
                """;
            command.Parameters.AddWithValue("$id", analysisId);
            command.Parameters.AddWithValue("$title", "Legacy MVP-0 analysis");
            command.Parameters.AddWithValue("$status", AnalysisStatus.NeedsExpertEvaluation.ToString());
            command.Parameters.AddWithValue("$originalDescription", "Legacy original description.");
            command.Parameters.AddWithValue("$projectRequest", "Legacy project request.");
            command.Parameters.AddWithValue("$situationDescription", "Legacy situation description.");
            command.Parameters.AddWithValue("$changeSource", "Legacy change source.");
            command.Parameters.AddWithValue("$createdAt", fixedAt);
            command.Parameters.AddWithValue("$updatedAt", fixedAt);

            await command.ExecuteNonQueryAsync();
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO AiAnalysisResults
                    (Id, AnalysisId, Status, GeneratedAt, EngineName, ProviderName, ModelName, PromptVersion, InputSnapshot, RawResponse, ErrorMessage)
                VALUES
                    ($id, $analysisId, $status, $generatedAt, $engineName, $providerName, $modelName, $promptVersion, $inputSnapshot, $rawResponse, $errorMessage);
                """;
            command.Parameters.AddWithValue("$id", resultId);
            command.Parameters.AddWithValue("$analysisId", analysisId);
            command.Parameters.AddWithValue("$status", AiAnalysisResultStatus.Completed.ToString());
            command.Parameters.AddWithValue("$generatedAt", fixedAt);
            command.Parameters.AddWithValue("$engineName", "legacy-engine");
            command.Parameters.AddWithValue("$providerName", "legacy-provider");
            command.Parameters.AddWithValue("$modelName", "legacy-model");
            command.Parameters.AddWithValue("$promptVersion", "legacy-prompt");
            command.Parameters.AddWithValue("$inputSnapshot", "{ \"legacy\": true }");
            command.Parameters.AddWithValue("$rawResponse", "{ \"status\": \"completed\" }");
            command.Parameters.AddWithValue("$errorMessage", string.Empty);

            await command.ExecuteNonQueryAsync();
        }
    }

    private static Analysis CreateAnalysisGraph()
    {
        var fixedAt = new DateTimeOffset(2026, 06, 13, 12, 30, 0, TimeSpan.Zero);
        var fragment = new ContextFragment
        {
            Type = ContextFragmentType.ApiDescription,
            Source = "OpenAPI fragment",
            Text = "Payment API response contract changes.",
            FileName = "payment-api.md",
            FilePath = "context/payment-api.md",
            CreatedAt = fixedAt
        };

        var impactMap = new ImpactMap
        {
            ChangeSummary =
            {
                Title = "Scope update",
                Description = "The request changes the payment API response.",
                Severity = ImpactSeverity.Medium,
                Notes = "Stable singleton id must survive persistence."
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

        var expertEvaluation = new ExpertEvaluation
        {
            ContextSufficiency = ContextSufficiencyRating.PartiallySufficient,
            ResultUsefulness = ResultUsefulnessRating.Useful,
            GeneralComment = "AI result is useful but needs an added rollout note."
        };

        expertEvaluation.EvaluatedItems.Add(new ExpertEvaluatedItem
        {
            TargetType = ExpertEvaluationTargetType.ImpactItem,
            TargetId = affectedRequirement.Id,
            Mark = ExpertMark.Confirmed,
            Comment = "Requirement impact is correct.",
            CorrectionText = string.Empty
        });

        expertEvaluation.MissedItems.Add(new ExpertMissedItem
        {
            ItemType = ImpactMapItemType.AffectedProjectDecision,
            Title = "Missing rollout note",
            Description = "Deployment sequencing should be documented.",
            Severity = ImpactSeverity.Low,
            Comment = "Add in implementation planning."
        });

        expertEvaluation.Corrections.Add(new ExpertCorrection
        {
            TargetType = ExpertEvaluationTargetType.ImpactItem,
            TargetId = risk.Id,
            ItemType = ImpactMapItemType.Risk,
            Text = "Clarify compatibility risk for API consumers.",
            Comment = "Risk wording should be more specific."
        });

        var analysis = new Analysis
        {
            Id = Guid.NewGuid(),
            Title = "Payment API change",
            Status = AnalysisStatus.NeedsExpertEvaluation,
            OriginalDescription = "Change payment API response.",
            ProjectRequest = "Add new status field to payment API response.",
            SituationDescription = "Several consumers parse the payment response.",
            ChangeSource = "RFC-42",
            CreatedAt = fixedAt,
            UpdatedAt = fixedAt,
            FixedAt = null,
            AiAnalysisResult = new AiAnalysisResult
            {
                Status = AiAnalysisResultStatus.Completed,
                GeneratedAt = fixedAt,
                EngineName = "demo-engine",
                ProviderName = "demo-provider",
                ModelName = "demo-model",
                PromptVersion = "mvp-v1",
                InputSnapshot = "{ \"request\": \"payment-api\" }",
                RawResponse = "{ \"status\": \"completed\" }",
                ImpactMap = impactMap,
                ErrorMessage = string.Empty
            },
            ExpertEvaluation = expertEvaluation,
            ExpertConclusion = new ExpertConclusion
            {
                ConclusionType = ExpertConclusionType.AcceptWithLimitations,
                Comment = "Human expert conclusion",
                Rationale = "Accept after documenting rollout constraints.",
                FixedAt = fixedAt
            }
        };

        analysis.ContextFragments.Add(fragment);

        return analysis;
    }
}

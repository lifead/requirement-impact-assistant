using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Application.Analysis.External.Dify;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;
using RequirementImpactAssistant.Web.Extensions;
using RequirementImpactAssistant.Tests.Support;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class AnalysisExecutionServiceTests
{
    private static readonly JsonSerializerOptions StableJsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task RunAsync_ExecutesEngineThroughAssemblerAndPersistsCompletedResult()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");
            var fragment = CreateContextFragment(analysis.Id);
            analysis.ContextFragments.Add(fragment);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            var engine = new StubAiAnalysisEngine(new AiAnalysisResponse(
                AiAnalysisResponseStatus.Succeeded,
                CreateImpactMap(),
                "raw success response",
                [],
                AnalysisBoundaryNotice.Default));
            var assembler = new CapturingAssembler();

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, assembler, engine);

                var outcome = await service.RunAsync(analysis.Id);

                Assert.True(outcome.Succeeded);
                Assert.Equal(AiAnalysisResultStatus.Completed, outcome.ResultStatus);
            }

            Assert.Equal(1, assembler.CallCount);
            Assert.NotNull(assembler.LastAnalysis);
            Assert.Equal(analysis.Id, assembler.LastAnalysis.Id);
            var assembledFragment = Assert.Single(assembler.LastAnalysis.ContextFragments);
            Assert.Equal(fragment.Id, assembledFragment.Id);
            Assert.Equal(1, engine.CallCount);
            Assert.NotNull(engine.LastRequest);
            Assert.Contains("Gateway migration", engine.LastRequest.InputSnapshotJson);
            Assert.Contains("Architecture note", engine.LastRequest.InputSnapshotJson);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var saved = await dbContext.Analyses
                    .Include(candidate => candidate.AiAnalysisResult)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, saved.Status);
                Assert.NotNull(saved.AiAnalysisResult);
                Assert.Equal(AiAnalysisResultStatus.Completed, saved.AiAnalysisResult.Status);
                Assert.Equal("raw success response", saved.AiAnalysisResult.RawResponse);
                Assert.Equal("Demo", saved.AiAnalysisResult.ProviderName);
                Assert.Equal("demo-deterministic", saved.AiAnalysisResult.ModelName);
                Assert.Equal("direct-llm-analysis-v1", saved.AiAnalysisResult.PromptVersion);
                Assert.NotNull(saved.AiAnalysisResult.GeneratedAt);
                Assert.NotNull(saved.AiAnalysisResult.ImpactMap);
                Assert.Equal("change-summary", saved.AiAnalysisResult.ImpactMap.ChangeSummary.Id);
                Assert.Empty(saved.AiAnalysisResult.ErrorMessage);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_PersistsDirectLlmMetadataWithoutRetrievedContextItems()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");
            var warning = "provider returned partial diagnostic";

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            var provider = new CapturingLlmProvider(new LlmProviderResponse(
                LlmProviderResponseStatus.Partial,
                CreateImpactMap(),
                "raw partial direct llm response",
                [warning]));
            var engine = new DirectLlmAnalysisEngine(
                provider,
                Options.Create(new AiAnalysisOptions
                {
                    Provider = LlmProviderNames.Demo
                }));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, new CapturingAssembler(), engine);

                var outcome = await service.RunAsync(analysis.Id);

                Assert.True(outcome.Succeeded);
                Assert.Equal(AiAnalysisResultStatus.CompletedWithWarnings, outcome.ResultStatus);
            }

            Assert.Equal(1, provider.CallCount);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var saved = await dbContext.Analyses
                    .AsSplitQuery()
                    .Include(candidate => candidate.AiAnalysisResult)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.NotNull(saved.AiAnalysisResult);
                var metadata = saved.AiAnalysisResult.Metadata;

                Assert.Equal(AnalysisMode.DirectLlm, metadata.AnalysisMode);
                Assert.Equal(nameof(DirectLlmAnalysisEngine), metadata.EngineName);
                Assert.Equal(LlmProviderNames.Demo, metadata.ProviderName);
                Assert.Null(metadata.AdapterName);
                Assert.Equal("demo-deterministic", metadata.ModelWorkflowProfileName);
                Assert.Equal(RetrievedContextState.Unavailable, metadata.RetrievedContextState);
                Assert.False(metadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Equal([warning], metadata.Warnings);
                Assert.Empty(metadata.RetrievedContextItems);
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
            DeleteDatabase(databasePath);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_Mvp1SmokeBaselineDirectLlmPathPersistsImpactMapAndNeutralMetadata(
        bool explicitDirectLlmMode)
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var baseline = Mvp1SmokeBaselineFixture.Create();
            var options = CreateOptions(databasePath);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(baseline.Analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var openedAnalysis = await dbContext.Analyses
                    .AsNoTracking()
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == baseline.Analysis.Id);

                Assert.Equal(AnalysisStatus.ReadyForAnalysis, openedAnalysis.Status);
                Assert.Equal(
                    baseline.ManualContextFragments.Select(fragment => fragment.Id),
                    openedAnalysis.ContextFragments
                        .OrderBy(fragment => fragment.CreatedAt)
                        .ThenBy(fragment => fragment.Id)
                        .Select(fragment => fragment.Id));
            }

            var provider = new CapturingLlmProvider(new LlmProviderResponse(
                LlmProviderResponseStatus.Succeeded,
                baseline.ExpectedImpactMap,
                "local direct LLM smoke response",
                []));
            var directEngine = new DirectLlmAnalysisEngine(
                provider,
                Options.Create(new AiAnalysisOptions
                {
                    Provider = LlmProviderNames.Demo
                }));
            var externalEngine = new StubAiAnalysisEngine(new AiAnalysisResponse(
                AiAnalysisResponseStatus.Failed,
                null,
                "external path must not be called",
                ["external path must not be called"],
                AnalysisBoundaryNotice.Default));
            var selector = new ModeAwareAiAnalysisEngineSelector(directEngine, externalEngine);
            var assembler = new CapturingAssembler();

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, assembler, selector);

                var outcome = explicitDirectLlmMode
                    ? await service.RunAsync(baseline.Analysis.Id, AnalysisMode.DirectLlm)
                    : await service.RunAsync(baseline.Analysis.Id);

                Assert.True(outcome.Succeeded);
                Assert.Equal(AiAnalysisResultStatus.Completed, outcome.ResultStatus);
            }

            Assert.Equal([AnalysisMode.DirectLlm], selector.SelectedModes);
            Assert.Equal(1, assembler.CallCount);
            Assert.NotNull(assembler.LastAnalysis);
            Assert.Equal(baseline.Analysis.Id, assembler.LastAnalysis.Id);
            Assert.Equal(
                baseline.ManualContextFragments.Select(fragment => fragment.Id),
                assembler.LastAnalysis.ContextFragments
                    .OrderBy(fragment => fragment.CreatedAt)
                    .ThenBy(fragment => fragment.Id)
                    .Select(fragment => fragment.Id));
            Assert.Equal(1, provider.CallCount);
            Assert.NotNull(provider.LastRequest);
            Assert.Equal(LlmProviderNames.Demo, provider.LastRequest.Provider);
            Assert.Equal(baseline.AnalysisRequest.InputSnapshotJson, provider.LastRequest.AnalysisRequest.InputSnapshotJson);
            Assert.All(
                baseline.ManualContextFragments,
                fragment => Assert.Contains(fragment.Text, provider.LastRequest.Prompt, StringComparison.Ordinal));
            Assert.Equal(0, externalEngine.CallCount);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var saved = await dbContext.Analyses
                    .AsSplitQuery()
                    .Include(candidate => candidate.ContextFragments)
                    .Include(candidate => candidate.AiAnalysisResult)
                    .ThenInclude(result => result!.Metadata.RetrievedContextItems)
                    .SingleAsync(candidate => candidate.Id == baseline.Analysis.Id);

                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, saved.Status);
                Assert.Equal(baseline.ManualContextFragments.Count, saved.ContextFragments.Count);
                Assert.Equal(
                    baseline.ManualContextFragments
                        .OrderBy(fragment => fragment.CreatedAt)
                        .ThenBy(fragment => fragment.Id)
                        .Select(fragment => new
                        {
                            fragment.Id,
                            fragment.AnalysisId,
                            fragment.Type,
                            fragment.Source,
                            fragment.Text,
                            fragment.FileName,
                            fragment.FilePath,
                            fragment.CreatedAt
                        }),
                    saved.ContextFragments
                        .OrderBy(fragment => fragment.CreatedAt)
                        .ThenBy(fragment => fragment.Id)
                        .Select(fragment => new
                        {
                            fragment.Id,
                            fragment.AnalysisId,
                            fragment.Type,
                            fragment.Source,
                            fragment.Text,
                            fragment.FileName,
                            fragment.FilePath,
                            fragment.CreatedAt
                        }));
                Assert.NotNull(saved.AiAnalysisResult);
                var savedResult = saved.AiAnalysisResult;
                Assert.Equal(AiAnalysisResultStatus.Completed, savedResult.Status);
                Assert.Equal("local direct LLM smoke response", savedResult.RawResponse);
                Assert.Empty(savedResult.ErrorMessage);
                Assert.Equal(nameof(DirectLlmAnalysisEngine), savedResult.EngineName);
                Assert.Equal(LlmProviderNames.Demo, savedResult.ProviderName);
                Assert.Equal("demo-deterministic", savedResult.ModelName);
                Assert.Equal("direct-llm-analysis-v1", savedResult.PromptVersion);
                Assert.Equal(baseline.AnalysisRequest.InputSnapshotJson, savedResult.InputSnapshot);
                Assert.NotNull(savedResult.ImpactMap);
                Assert.Equal(
                    JsonSerializer.Serialize(baseline.ExpectedImpactMap, StableJsonOptions),
                    JsonSerializer.Serialize(savedResult.ImpactMap, StableJsonOptions));

                var metadata = savedResult.Metadata;
                Assert.Equal(AnalysisMode.DirectLlm, metadata.AnalysisMode);
                Assert.Equal(nameof(DirectLlmAnalysisEngine), metadata.EngineName);
                Assert.Equal(LlmProviderNames.Demo, metadata.ProviderName);
                Assert.Null(metadata.AdapterName);
                Assert.Equal("demo-deterministic", metadata.ModelWorkflowProfileName);
                Assert.Equal(RetrievedContextState.Unavailable, metadata.RetrievedContextState);
                Assert.False(metadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Empty(metadata.Warnings);
                Assert.Empty(metadata.RetrievedContextItems);
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
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_Mvp1SmokeBaselineExternalRagPathPersistsImpactMapMetadataAndRetrievedContext()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var baseline = Mvp1SmokeBaselineFixture.Create();
            var options = CreateOptions(databasePath);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(baseline.Analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var openedAnalysis = await dbContext.Analyses
                    .AsNoTracking()
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == baseline.Analysis.Id);

                Assert.Equal(AnalysisStatus.ReadyForAnalysis, openedAnalysis.Status);
                Assert.Equal(
                    baseline.ManualContextFragments.Select(fragment => fragment.Id),
                    openedAnalysis.ContextFragments
                        .OrderBy(fragment => fragment.CreatedAt)
                        .ThenBy(fragment => fragment.Id)
                        .Select(fragment => fragment.Id));
            }

            var provider = new CapturingLlmProvider(new LlmProviderResponse(
                LlmProviderResponseStatus.Failed,
                null,
                "direct path must not be called",
                ["direct path must not be called"]));
            var directEngine = new DirectLlmAnalysisEngine(
                provider,
                Options.Create(new AiAnalysisOptions
                {
                    Provider = LlmProviderNames.Demo
                }));
            var adapter = new CapturingFixtureExternalRagAdapter(baseline.ExternalHappyPathResponse);
            var externalEngine = new ExternalRagAnalysisEngine(adapter);
            var selector = new ModeAwareAiAnalysisEngineSelector(directEngine, externalEngine);
            var assembler = new CapturingAssembler();

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, assembler, selector);

                var outcome = await service.RunAsync(baseline.Analysis.Id, AnalysisMode.ExternalRag);

                Assert.True(outcome.Succeeded);
                Assert.Equal(AiAnalysisResultStatus.Completed, outcome.ResultStatus);
            }

            Assert.Equal([AnalysisMode.ExternalRag], selector.SelectedModes);
            Assert.Equal(0, provider.CallCount);
            Assert.Equal(1, assembler.CallCount);
            Assert.NotNull(assembler.LastAnalysis);
            Assert.Equal(baseline.Analysis.Id, assembler.LastAnalysis.Id);
            Assert.IsType<CapturingFixtureExternalRagAdapter>(adapter);
            Assert.Equal(1, adapter.CallCount);
            Assert.NotNull(adapter.LastRequest);
            var adapterRequest = adapter.LastRequest;
            Assert.Equal(baseline.Analysis.Id, adapterRequest.CorrelationId);
            Assert.Equal(nameof(ExternalRagAnalysisEngine), adapterRequest.ExecutionMetadata.EngineName);
            Assert.True(adapterRequest.CanForwardManualContextToExternalAiOrRag);
            Assert.NotNull(adapterRequest.ManualContext);
            Assert.Equal(
                baseline.ManualContextFragments.Select(fragment => fragment.Id),
                adapterRequest.ManualContext.ContextFragments.Select(fragment => fragment.Id));
            Assert.Equal(
                baseline.ManualContextFragments.Select(fragment => fragment.Text),
                adapterRequest.ManualContext.ContextFragments.Select(fragment => fragment.Text));
            Assert.All(
                baseline.ManualContextFragments,
                fragment => Assert.Contains(fragment.Text, adapterRequest.ManualContext.CombinedText, StringComparison.Ordinal));
            Assert.All(
                baseline.ExternalHappyPathResponse.RetrievedContextItems,
                retrievedItem =>
                {
                    Assert.DoesNotContain(retrievedItem.ExternalReference!, adapterRequest.ManualContext.CombinedText, StringComparison.Ordinal);
                    Assert.DoesNotContain(retrievedItem.FragmentId!, adapterRequest.ManualContext.CombinedText, StringComparison.Ordinal);
                    Assert.DoesNotContain(retrievedItem.Text!, adapterRequest.ManualContext.CombinedText, StringComparison.Ordinal);
                });

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var saved = await dbContext.Analyses
                    .AsSplitQuery()
                    .Include(candidate => candidate.ContextFragments)
                    .Include(candidate => candidate.AiAnalysisResult)
                    .ThenInclude(result => result!.Metadata.RetrievedContextItems)
                    .SingleAsync(candidate => candidate.Id == baseline.Analysis.Id);

                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, saved.Status);
                Assert.NotNull(saved.AiAnalysisResult);
                var savedResult = saved.AiAnalysisResult;
                var expectedModelWorkflowProfileName = CreateExternalModelWorkflowProfileName(
                    baseline.ExternalHappyPathResponse.Metadata);
                Assert.Equal(AiAnalysisResultStatus.Completed, savedResult.Status);
                Assert.Empty(savedResult.ErrorMessage);
                Assert.Equal(baseline.ExternalHappyPathResponse.SanitizedDiagnosticSnapshot, savedResult.RawResponse);
                Assert.Equal(nameof(ExternalRagAnalysisEngine), savedResult.EngineName);
                Assert.Equal(baseline.ExternalHappyPathResponse.Metadata.ProviderName, savedResult.ProviderName);
                Assert.Equal(expectedModelWorkflowProfileName, savedResult.ModelName);
                Assert.DoesNotContain("Dify", savedResult.RawResponse, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("DeepSeek", savedResult.RawResponse, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Authorization", savedResult.RawResponse, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Bearer", savedResult.RawResponse, StringComparison.OrdinalIgnoreCase);
                using (var diagnosticDocument = JsonDocument.Parse(savedResult.RawResponse))
                {
                    Assert.Equal(
                        baseline.ExternalHappyPathResponse.Metadata.SanitizedProperties["network"],
                        diagnosticDocument.RootElement.GetProperty("network").GetString());
                }
                Assert.Equal(baseline.AnalysisRequest.InputSnapshotJson, savedResult.InputSnapshot);
                Assert.NotNull(savedResult.ImpactMap);
                Assert.Equal(
                    JsonSerializer.Serialize(baseline.ExpectedImpactMap, StableJsonOptions),
                    JsonSerializer.Serialize(savedResult.ImpactMap, StableJsonOptions));

                var metadata = savedResult.Metadata;
                Assert.Equal(AnalysisMode.ExternalRag, metadata.AnalysisMode);
                Assert.Equal(nameof(ExternalRagAnalysisEngine), metadata.EngineName);
                Assert.Equal(baseline.ExternalHappyPathResponse.Metadata.ProviderName, metadata.ProviderName);
                Assert.Equal(baseline.ExternalHappyPathResponse.Metadata.AdapterName, metadata.AdapterName);
                Assert.Equal(expectedModelWorkflowProfileName, metadata.ModelWorkflowProfileName);
                Assert.Equal(RetrievedContextState.Available, metadata.RetrievedContextState);
                Assert.True(metadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Empty(metadata.Warnings);
                Assert.Equal(
                    baseline.ExternalHappyPathResponse.RetrievedContextItems.Count,
                    metadata.RetrievedContextItems.Count);

                Assert.Equal(
                    baseline.ExternalHappyPathResponse.RetrievedContextItems
                        .OrderBy(item => item.Rank)
                        .Select(item => new
                        {
                            item.SourceTitle,
                            item.SourceId,
                            item.ExternalReference,
                            item.FragmentId,
                            item.Text,
                            item.Excerpt,
                            item.UrlOrReference,
                            item.Rank,
                            item.Score,
                            item.ProviderName,
                            item.AdapterName,
                            item.Completeness
                        }),
                    metadata.RetrievedContextItems
                        .OrderBy(item => item.Rank)
                        .Select(item => new
                        {
                            item.SourceTitle,
                            item.SourceId,
                            item.ExternalReference,
                            item.FragmentId,
                            item.Text,
                            item.Excerpt,
                            item.UrlOrReference,
                            item.Rank,
                            item.Score,
                            item.ProviderName,
                            item.AdapterName,
                            item.Completeness
                        }));
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM RetrievedContextItems;";

                var retrievedContextItemCount = (long)(await command.ExecuteScalarAsync() ?? 0L);
                Assert.Equal(baseline.ExternalHappyPathResponse.RetrievedContextItems.Count, retrievedContextItemCount);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_PersistsEngineProvidedMetadataAndRetrievedContextItems()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");
            var metadata = new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = "external-rag-engine",
                ProviderName = "neutral-provider",
                AdapterName = "neutral-adapter",
                ModelWorkflowProfileName = "impact-profile",
                RetrievedContextState = RetrievedContextState.MetadataOnly,
                ManualContextForwardedToExternalAiOrRag = true,
                Warnings = ["external context metadata only"],
                RetrievedContextItems =
                [
                    new RetrievedContextItem
                    {
                        SourceTitle = "Integration inventory",
                        ExternalReference = "inventory-record-42",
                        UrlOrReference = "kb://inventory/42",
                        Rank = 1,
                        Score = 0.86,
                        ProviderName = "neutral-provider",
                        AdapterName = "neutral-adapter",
                        Completeness = RetrievedContextItemCompleteness.MetadataOnly,
                        WarningOrLimitationNote = "Only metadata was returned."
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
                var service = CreateService(
                    dbContext,
                    new CapturingAssembler(),
                    new StubAiAnalysisEngine(new AiAnalysisResponse(
                        AiAnalysisResponseStatus.Partial,
                        CreateImpactMap(),
                        "sanitized external diagnostic snapshot",
                        ["external diagnostic"],
                        AnalysisBoundaryNotice.Default,
                        ResultMetadata: metadata)));

                var outcome = await service.RunAsync(analysis.Id);

                Assert.True(outcome.Succeeded);
                Assert.Equal(AiAnalysisResultStatus.CompletedWithWarnings, outcome.ResultStatus);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var savedResult = await dbContext.AiAnalysisResults
                    .AsSplitQuery()
                    .Include(candidate => candidate.Metadata.RetrievedContextItems)
                    .SingleAsync(candidate => candidate.AnalysisId == analysis.Id);

                Assert.Equal(AiAnalysisResultStatus.CompletedWithWarnings, savedResult.Status);
                Assert.Equal("external-rag-engine", savedResult.EngineName);
                Assert.Equal("neutral-provider", savedResult.ProviderName);
                Assert.Equal("impact-profile", savedResult.ModelName);
                Assert.Equal("sanitized external diagnostic snapshot", savedResult.RawResponse);
                Assert.Equal("external diagnostic", savedResult.ErrorMessage);

                var savedMetadata = savedResult.Metadata;
                Assert.Equal(AnalysisMode.ExternalRag, savedMetadata.AnalysisMode);
                Assert.Equal("external-rag-engine", savedMetadata.EngineName);
                Assert.Equal("neutral-provider", savedMetadata.ProviderName);
                Assert.Equal("neutral-adapter", savedMetadata.AdapterName);
                Assert.Equal("impact-profile", savedMetadata.ModelWorkflowProfileName);
                Assert.Equal(RetrievedContextState.MetadataOnly, savedMetadata.RetrievedContextState);
                Assert.True(savedMetadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Equal(["external context metadata only"], savedMetadata.Warnings);

                var savedItem = Assert.Single(savedMetadata.RetrievedContextItems);
                Assert.Equal("Integration inventory", savedItem.SourceTitle);
                Assert.Equal("inventory-record-42", savedItem.ExternalReference);
                Assert.Equal("kb://inventory/42", savedItem.UrlOrReference);
                Assert.Equal(1, savedItem.Rank);
                Assert.Equal(0.86, savedItem.Score);
                Assert.Equal("neutral-provider", savedItem.ProviderName);
                Assert.Equal("neutral-adapter", savedItem.AdapterName);
                Assert.Equal(RetrievedContextItemCompleteness.MetadataOnly, savedItem.Completeness);
                Assert.Equal("Only metadata was returned.", savedItem.WarningOrLimitationNote);
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM RetrievedContextItems;";

                var retrievedContextItemCount = (long)(await command.ExecuteScalarAsync() ?? 0L);
                Assert.Equal(1L, retrievedContextItemCount);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_DefaultExecutionSelectsDirectLlmMode()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            var directEngine = new StubAiAnalysisEngine(new AiAnalysisResponse(
                AiAnalysisResponseStatus.Succeeded,
                CreateImpactMap(),
                "direct raw response",
                [],
                AnalysisBoundaryNotice.Default));
            var externalEngine = new StubAiAnalysisEngine(new AiAnalysisResponse(
                AiAnalysisResponseStatus.Failed,
                null,
                "external raw response",
                ["external should not be selected"],
                AnalysisBoundaryNotice.Default));
            var selector = new ModeAwareAiAnalysisEngineSelector(directEngine, externalEngine);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, new CapturingAssembler(), selector);

                var outcome = await service.RunAsync(analysis.Id);

                Assert.True(outcome.Succeeded);
                Assert.Equal(AiAnalysisResultStatus.Completed, outcome.ResultStatus);
            }

            Assert.Equal([AnalysisMode.DirectLlm], selector.SelectedModes);
            Assert.Equal(1, directEngine.CallCount);
            Assert.Equal(0, externalEngine.CallCount);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_ExternalRagModeSelectsExternalEngineAndPersistsUnavailableResultWithoutAdapter()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            var directEngine = new StubAiAnalysisEngine(new AiAnalysisResponse(
                AiAnalysisResponseStatus.Succeeded,
                CreateImpactMap(),
                "direct raw response",
                [],
                AnalysisBoundaryNotice.Default));
            var externalEngine = new ExternalRagAnalysisEngine();
            var selector = new ModeAwareAiAnalysisEngineSelector(directEngine, externalEngine);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, new CapturingAssembler(), selector);

                var outcome = await service.RunAsync(analysis.Id, AnalysisMode.ExternalRag);

                Assert.True(outcome.Succeeded);
                Assert.Equal(AiAnalysisResultStatus.Failed, outcome.ResultStatus);
            }

            Assert.Equal([AnalysisMode.ExternalRag], selector.SelectedModes);
            Assert.Equal(0, directEngine.CallCount);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var savedResult = await dbContext.AiAnalysisResults
                    .AsSplitQuery()
                    .Include(candidate => candidate.Metadata.RetrievedContextItems)
                    .SingleAsync(candidate => candidate.AnalysisId == analysis.Id);

                Assert.Equal(AiAnalysisResultStatus.Failed, savedResult.Status);
                Assert.Equal(nameof(ExternalRagAnalysisEngine), savedResult.EngineName);
                Assert.Empty(savedResult.RawResponse);
                Assert.Contains("adapter is not configured", savedResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
                Assert.Null(savedResult.ImpactMap);

                var metadata = savedResult.Metadata;
                Assert.Equal(AnalysisMode.ExternalRag, metadata.AnalysisMode);
                Assert.Equal(nameof(ExternalRagAnalysisEngine), metadata.EngineName);
                Assert.Equal(RetrievedContextState.Unavailable, metadata.RetrievedContextState);
                Assert.False(metadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Contains(metadata.Warnings, warning => warning.Contains("adapter is not configured", StringComparison.OrdinalIgnoreCase));
                Assert.Empty(metadata.RetrievedContextItems);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_ExternalRagModeThroughApplicationServicesUsesConfiguredMockAdapter()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var analysis = CreateAnalysis("Gateway migration");
            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite($"Data Source={databasePath}"));
            services.AddApplicationAnalysis(CreateAnalysisConfiguration());

            using var serviceProvider = services.BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            using (var scope = serviceProvider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IAnalysisExecutionService>();

                var outcome = await service.RunAsync(analysis.Id, AnalysisMode.ExternalRag);

                Assert.True(outcome.Succeeded);
                Assert.Equal(AiAnalysisResultStatus.Completed, outcome.ResultStatus);
            }

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var savedResult = await dbContext.AiAnalysisResults
                    .Include(candidate => candidate.Metadata)
                    .SingleAsync(candidate => candidate.AnalysisId == analysis.Id);

                Assert.Equal(AiAnalysisResultStatus.Completed, savedResult.Status);
                Assert.Equal(nameof(ExternalRagAnalysisEngine), savedResult.EngineName);
                Assert.Equal("LocalMockKnowledgeSource", savedResult.ProviderName);
                Assert.Equal(nameof(MockExternalRagAdapter), savedResult.Metadata.AdapterName);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_ExternalRagModeThroughApplicationServicesUsesConfiguredDifyAdapterWithFakeHttp()
    {
        const string testDifyApiKey = "test-service-dify-api-key";
        var databasePath = CreateDatabasePath();
        var handler = new CapturingHttpMessageHandler(_ => CreateJsonResponse(HttpStatusCode.OK, """
            {
              "workflow_run_id": "run-service-1",
              "task_id": "task-service-1",
              "data": {
                "workflow_id": "workflow-from-response",
                "status": "succeeded",
                "outputs": {
                  "metadata": {
                    "model": "dify-service-model",
                    "response_shape": "structured-impact-map"
                  },
                  "impact_map": {
                    "change_summary": {
                      "title": "Gateway migration",
                      "description": "Authentication gateway change affects integration boundaries.",
                      "severity": "High"
                    },
                    "affected_requirements": [
                      {
                        "title": "Review gateway requirement",
                        "description": "Confirm whether the migration changes the requirement boundary.",
                        "severity": "Medium"
                      }
                    ],
                    "risks": [
                      {
                        "title": "Downstream integration regression",
                        "description": "Dependent clients may need additional regression checks.",
                        "severity": "High"
                      }
                    ],
                    "preliminary_assessment": {
                      "title": "Requires expert review",
                      "description": "The response is preliminary analytical material only.",
                      "severity": "Medium"
                    }
                  },
                  "retrieved_context": [
                    {
                      "source_title": "Integration requirements catalogue",
                      "source_id": "requirements",
                      "external_reference": "REQ-42",
                      "fragment_id": "fragment-42",
                      "text": "Gateway changes that affect integration boundaries require expert review.",
                      "excerpt": "Gateway changes require expert review.",
                      "url_or_reference": "kb://requirements/REQ-42",
                      "rank": 1,
                      "score": 0.91
                    }
                  ],
                  "warnings": []
                }
              }
            }
            """));

        try
        {
            var analysis = CreateAnalysis("Gateway migration");
            analysis.ContextFragments.Add(CreateContextFragment(analysis.Id));
            var services = new ServiceCollection();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite($"Data Source={databasePath}"));
            services.AddApplicationAnalysis(CreateDifyAnalysisConfiguration(testDifyApiKey));
            services.AddHttpClient<DifyExternalRagAdapter>()
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            using var serviceProvider = services.BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            using (var scope = serviceProvider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IAnalysisExecutionService>();

                var outcome = await service.RunAsync(analysis.Id, AnalysisMode.ExternalRag);

                Assert.True(outcome.Succeeded);
                Assert.Equal(AiAnalysisResultStatus.Completed, outcome.ResultStatus);
            }

            Assert.Equal(1, handler.CallCount);
            Assert.NotNull(handler.LastRequest);
            Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
            Assert.Equal("https://dify.invalid/workflows/run", handler.LastRequest.RequestUri?.ToString());
            Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
            Assert.Equal(testDifyApiKey, handler.LastRequest.Headers.Authorization?.Parameter);
            Assert.NotNull(handler.LastRequestBody);
            Assert.Contains("Gateway migration", handler.LastRequestBody, StringComparison.Ordinal);
            Assert.Contains("Keep gateway contract backward compatible.", handler.LastRequestBody, StringComparison.Ordinal);
            Assert.DoesNotContain(testDifyApiKey, handler.LastRequestBody, StringComparison.Ordinal);

            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var saved = await dbContext.Analyses
                    .AsSplitQuery()
                    .Include(candidate => candidate.AiAnalysisResult)
                    .ThenInclude(result => result!.Metadata.RetrievedContextItems)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, saved.Status);
                Assert.NotNull(saved.AiAnalysisResult);
                Assert.Equal(AiAnalysisResultStatus.Completed, saved.AiAnalysisResult.Status);
                Assert.Equal(nameof(ExternalRagAnalysisEngine), saved.AiAnalysisResult.EngineName);
                Assert.Equal("Dify", saved.AiAnalysisResult.ProviderName);
                Assert.Equal(
                    "dify-service-model / workflow-from-response / service-test-profile",
                    saved.AiAnalysisResult.ModelName);
                Assert.Empty(saved.AiAnalysisResult.ErrorMessage);
                Assert.NotNull(saved.AiAnalysisResult.ImpactMap);
                Assert.Equal("Gateway migration", saved.AiAnalysisResult.ImpactMap.ChangeSummary.Title);
                Assert.Single(saved.AiAnalysisResult.ImpactMap.AffectedRequirements);
                Assert.Single(saved.AiAnalysisResult.ImpactMap.Risks);

                var metadata = saved.AiAnalysisResult.Metadata;
                Assert.Equal(AnalysisMode.ExternalRag, metadata.AnalysisMode);
                Assert.Equal(nameof(ExternalRagAnalysisEngine), metadata.EngineName);
                Assert.Equal("Dify", metadata.ProviderName);
                Assert.Equal(nameof(DifyExternalRagAdapter), metadata.AdapterName);
                Assert.Equal(
                    "dify-service-model / workflow-from-response / service-test-profile",
                    metadata.ModelWorkflowProfileName);
                Assert.Equal(RetrievedContextState.Available, metadata.RetrievedContextState);
                Assert.True(metadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Empty(metadata.Warnings);

                var savedItem = Assert.Single(metadata.RetrievedContextItems);
                Assert.Equal("Integration requirements catalogue", savedItem.SourceTitle);
                Assert.Equal("requirements", savedItem.SourceId);
                Assert.Equal("REQ-42", savedItem.ExternalReference);
                Assert.Equal("fragment-42", savedItem.FragmentId);
                Assert.Contains("integration boundaries", savedItem.Text);
                Assert.Equal("Gateway changes require expert review.", savedItem.Excerpt);
                Assert.Equal("kb://requirements/REQ-42", savedItem.UrlOrReference);
                Assert.Equal(1, savedItem.Rank);
                Assert.Equal(0.91, savedItem.Score);
                Assert.Equal("Dify", savedItem.ProviderName);
                Assert.Equal(nameof(DifyExternalRagAdapter), savedItem.AdapterName);
                Assert.Equal(RetrievedContextItemCompleteness.FullText, savedItem.Completeness);

                Assert.DoesNotContain(testDifyApiKey, saved.AiAnalysisResult.RawResponse, StringComparison.Ordinal);
                Assert.DoesNotContain("https://dify.invalid", saved.AiAnalysisResult.RawResponse, StringComparison.Ordinal);
                Assert.DoesNotContain("Authorization", saved.AiAnalysisResult.RawResponse, StringComparison.Ordinal);
                Assert.DoesNotContain("Bearer", saved.AiAnalysisResult.RawResponse, StringComparison.Ordinal);

                using var diagnosticDocument = JsonDocument.Parse(saved.AiAnalysisResult.RawResponse);
                var diagnosticRoot = diagnosticDocument.RootElement;
                Assert.Equal("completed", diagnosticRoot.GetProperty("status").GetString());
                Assert.Equal("Dify", diagnosticRoot.GetProperty("provider").GetString());
                Assert.Equal(nameof(DifyExternalRagAdapter), diagnosticRoot.GetProperty("adapter").GetString());
                Assert.Equal("workflow-from-response", diagnosticRoot.GetProperty("workflow").GetString());
                Assert.Equal("service-test-profile", diagnosticRoot.GetProperty("profile").GetString());
                Assert.Equal("Available", diagnosticRoot.GetProperty("retrievedContextState").GetString());
                Assert.Equal(1, diagnosticRoot.GetProperty("retrievedContextItemCount").GetInt32());
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Theory]
    [InlineData(null, AiAnalysisResultStatus.Completed, AnalysisStatus.NeedsExpertEvaluation, RetrievedContextState.Available, 2, false)]
    [InlineData("metadata-only", AiAnalysisResultStatus.CompletedWithWarnings, AnalysisStatus.NeedsExpertEvaluation, RetrievedContextState.MetadataOnly, 1, false)]
    [InlineData("unavailable", AiAnalysisResultStatus.CompletedWithWarnings, AnalysisStatus.NeedsExpertEvaluation, RetrievedContextState.Unavailable, 0, false)]
    [InlineData("partial", AiAnalysisResultStatus.CompletedWithWarnings, AnalysisStatus.NeedsExpertEvaluation, RetrievedContextState.Partial, 1, false)]
    [InlineData("failed", AiAnalysisResultStatus.Failed, AnalysisStatus.LlmAnalysisFailed, RetrievedContextState.Unavailable, 0, true)]
    public async Task RunAsync_ExternalRagModeThroughMockAdapterPersistsScenarioMetadataAndRetrievedContext(
        string? mockProfileName,
        AiAnalysisResultStatus expectedResultStatus,
        AnalysisStatus expectedAnalysisStatus,
        RetrievedContextState expectedRetrievedContextState,
        int expectedRetrievedContextItemCount,
        bool expectedFailedResult)
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");
            analysis.ContextFragments.Add(CreateContextFragment(analysis.Id));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            var directEngine = new StubAiAnalysisEngine(new AiAnalysisResponse(
                AiAnalysisResponseStatus.Succeeded,
                CreateImpactMap(),
                "direct raw response",
                [],
                AnalysisBoundaryNotice.Default));
            var adapter = new ProfiledMockExternalRagAdapter(mockProfileName);
            var externalEngine = new ExternalRagAnalysisEngine(adapter);
            var selector = new ModeAwareAiAnalysisEngineSelector(directEngine, externalEngine);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, new CapturingAssembler(), selector);

                var outcome = await service.RunAsync(analysis.Id, AnalysisMode.ExternalRag);

                Assert.True(outcome.Succeeded);
                Assert.Equal(expectedResultStatus, outcome.ResultStatus);
            }

            Assert.Equal([AnalysisMode.ExternalRag], selector.SelectedModes);
            Assert.Equal(0, directEngine.CallCount);
            Assert.Equal(1, adapter.CallCount);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var saved = await dbContext.Analyses
                    .AsSplitQuery()
                    .Include(candidate => candidate.AiAnalysisResult)
                    .ThenInclude(result => result!.Metadata.RetrievedContextItems)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(expectedAnalysisStatus, saved.Status);
                Assert.NotNull(saved.AiAnalysisResult);
                Assert.Equal(expectedResultStatus, saved.AiAnalysisResult.Status);
                Assert.Equal(nameof(ExternalRagAnalysisEngine), saved.AiAnalysisResult.EngineName);
                Assert.Equal("LocalMockKnowledgeSource", saved.AiAnalysisResult.ProviderName);
                Assert.Equal(CreateMockModelWorkflowProfileName(mockProfileName), saved.AiAnalysisResult.ModelName);
                Assert.Equal(expectedFailedResult, saved.AiAnalysisResult.ImpactMap is null);

                var metadata = saved.AiAnalysisResult.Metadata;
                Assert.Equal(AnalysisMode.ExternalRag, metadata.AnalysisMode);
                Assert.Equal(nameof(ExternalRagAnalysisEngine), metadata.EngineName);
                Assert.Equal("LocalMockKnowledgeSource", metadata.ProviderName);
                Assert.Equal(nameof(MockExternalRagAdapter), metadata.AdapterName);
                Assert.Equal(CreateMockModelWorkflowProfileName(mockProfileName), metadata.ModelWorkflowProfileName);
                Assert.Equal(expectedRetrievedContextState, metadata.RetrievedContextState);
                Assert.True(metadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Equal(expectedRetrievedContextItemCount, metadata.RetrievedContextItems.Count);

                using var document = JsonDocument.Parse(saved.AiAnalysisResult.RawResponse);
                Assert.Equal(
                    CreateMockProfileName(mockProfileName),
                    document.RootElement.GetProperty("profile").GetString());
                Assert.Equal(
                    expectedRetrievedContextState.ToString(),
                    document.RootElement.GetProperty("retrievedContextState").GetString());
                Assert.Equal(
                    expectedRetrievedContextItemCount,
                    document.RootElement.GetProperty("retrievedContextItemCount").GetInt32());

                AssertMockScenarioPersistence(
                    metadata,
                    mockProfileName,
                    expectedRetrievedContextItemCount,
                    expectedFailedResult,
                    saved.AiAnalysisResult.ErrorMessage);
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM RetrievedContextItems;";

                var retrievedContextItemCount = (long)(await command.ExecuteScalarAsync() ?? 0L);
                Assert.Equal(expectedRetrievedContextItemCount, retrievedContextItemCount);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_DefaultDirectLlmModeDoesNotUseMockExternalScenarioOrPersistRetrievedContext()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");
            analysis.ContextFragments.Add(CreateContextFragment(analysis.Id));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            var directEngine = new StubAiAnalysisEngine(new AiAnalysisResponse(
                AiAnalysisResponseStatus.Succeeded,
                CreateImpactMap(),
                "direct raw response",
                [],
                AnalysisBoundaryNotice.Default));
            var adapter = new ProfiledMockExternalRagAdapter("failed");
            var externalEngine = new ExternalRagAnalysisEngine(adapter);
            var selector = new ModeAwareAiAnalysisEngineSelector(directEngine, externalEngine);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, new CapturingAssembler(), selector);

                var outcome = await service.RunAsync(analysis.Id);

                Assert.True(outcome.Succeeded);
                Assert.Equal(AiAnalysisResultStatus.Completed, outcome.ResultStatus);
            }

            Assert.Equal([AnalysisMode.DirectLlm], selector.SelectedModes);
            Assert.Equal(1, directEngine.CallCount);
            Assert.Equal(0, adapter.CallCount);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var savedResult = await dbContext.AiAnalysisResults
                    .AsSplitQuery()
                    .Include(candidate => candidate.Metadata.RetrievedContextItems)
                    .SingleAsync(candidate => candidate.AnalysisId == analysis.Id);

                Assert.Equal(AiAnalysisResultStatus.Completed, savedResult.Status);
                Assert.Equal(nameof(StubAiAnalysisEngine), savedResult.EngineName);
                Assert.Equal("Demo", savedResult.ProviderName);
                Assert.Equal("demo-deterministic", savedResult.ModelName);
                Assert.Equal("direct raw response", savedResult.RawResponse);
                Assert.NotNull(savedResult.ImpactMap);

                var metadata = savedResult.Metadata;
                Assert.Equal(AnalysisMode.DirectLlm, metadata.AnalysisMode);
                Assert.Equal(nameof(StubAiAnalysisEngine), metadata.EngineName);
                Assert.Null(metadata.AdapterName);
                Assert.Equal(RetrievedContextState.Unavailable, metadata.RetrievedContextState);
                Assert.False(metadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Empty(metadata.Warnings);
                Assert.Empty(metadata.RetrievedContextItems);
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
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_OverwritesExistingResultMetadataRetrievedContextItems()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");
            var existingResultId = Guid.NewGuid();
            analysis.AiAnalysisResult = new AiAnalysisResult
            {
                Id = existingResultId,
                AnalysisId = analysis.Id,
                Status = AiAnalysisResultStatus.CompletedWithWarnings,
                EngineName = "previous-external-engine",
                ProviderName = "previous-provider",
                ModelName = "previous-profile",
                PromptVersion = "previous-prompt",
                InputSnapshot = "previous input",
                RawResponse = "previous raw response",
                ErrorMessage = "previous diagnostics",
                Metadata = new AiAnalysisResultMetadata
                {
                    AnalysisMode = AnalysisMode.ExternalRag,
                    EngineName = "previous-external-engine",
                    ProviderName = "previous-provider",
                    AdapterName = "previous-adapter",
                    ModelWorkflowProfileName = "previous-profile",
                    RetrievedContextState = RetrievedContextState.Partial,
                    Warnings = ["previous warning"],
                    RetrievedContextItems =
                    [
                        new RetrievedContextItem
                        {
                            SourceTitle = "Previous source",
                            ExternalReference = "previous-reference",
                            Completeness = RetrievedContextItemCompleteness.ExcerptOnly
                        }
                    ]
                }
            };
            var replacementMetadata = new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = "replacement-external-engine",
                ProviderName = "replacement-provider",
                AdapterName = "replacement-adapter",
                ModelWorkflowProfileName = "replacement-profile",
                RetrievedContextState = RetrievedContextState.MetadataOnly,
                Warnings = ["replacement warning"],
                RetrievedContextItems =
                [
                    new RetrievedContextItem
                    {
                        SourceTitle = "Replacement source",
                        ExternalReference = "replacement-reference",
                        Completeness = RetrievedContextItemCompleteness.MetadataOnly
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
                var service = CreateService(
                    dbContext,
                    new CapturingAssembler(),
                    new StubAiAnalysisEngine(new AiAnalysisResponse(
                        AiAnalysisResponseStatus.Succeeded,
                        CreateImpactMap(),
                        "replacement raw response",
                        [],
                        AnalysisBoundaryNotice.Default,
                        ResultMetadata: replacementMetadata)));

                var outcome = await service.RunAsync(analysis.Id);

                Assert.True(outcome.Succeeded);
                Assert.Equal(AiAnalysisResultStatus.Completed, outcome.ResultStatus);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var savedResult = await dbContext.AiAnalysisResults
                    .AsSplitQuery()
                    .Include(candidate => candidate.Metadata.RetrievedContextItems)
                    .SingleAsync(candidate => candidate.AnalysisId == analysis.Id);

                Assert.Equal(existingResultId, savedResult.Id);
                Assert.Equal("replacement-external-engine", savedResult.EngineName);
                Assert.Equal("replacement-provider", savedResult.ProviderName);
                Assert.Equal("replacement-profile", savedResult.ModelName);
                Assert.Equal("replacement raw response", savedResult.RawResponse);

                var savedMetadata = savedResult.Metadata;
                Assert.Equal("replacement-external-engine", savedMetadata.EngineName);
                Assert.Equal("replacement-provider", savedMetadata.ProviderName);
                Assert.Equal("replacement-adapter", savedMetadata.AdapterName);
                Assert.Equal("replacement-profile", savedMetadata.ModelWorkflowProfileName);
                Assert.Equal(["replacement warning"], savedMetadata.Warnings);

                var savedItem = Assert.Single(savedMetadata.RetrievedContextItems);
                Assert.Equal("Replacement source", savedItem.SourceTitle);
                Assert.Equal("replacement-reference", savedItem.ExternalReference);
                Assert.Equal(RetrievedContextItemCompleteness.MetadataOnly, savedItem.Completeness);
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT
                        COUNT(*),
                        SUM(CASE WHEN SourceTitle = 'Replacement source' THEN 1 ELSE 0 END),
                        SUM(CASE WHEN SourceTitle = 'Previous source' THEN 1 ELSE 0 END)
                    FROM RetrievedContextItems;
                    """;

                await using var reader = await command.ExecuteReaderAsync();
                Assert.True(await reader.ReadAsync());
                Assert.Equal(1L, reader.GetInt64(0));
                Assert.Equal(1L, reader.GetInt64(1));
                Assert.Equal(0L, reader.GetInt64(2));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_DoesNotCallEngineWhenMinimumInputIsMissing()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Incomplete request");
            analysis.ProjectRequest = string.Empty;

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            var engine = new StubAiAnalysisEngine(new AiAnalysisResponse(
                AiAnalysisResponseStatus.Succeeded,
                CreateImpactMap(),
                "raw response",
                [],
                AnalysisBoundaryNotice.Default));
            var assembler = new CapturingAssembler();

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, assembler, engine);

                var outcome = await service.RunAsync(analysis.Id);

                Assert.Equal(AnalysisExecutionOutcomeKind.InvalidInput, outcome.Kind);
                Assert.Contains("Минимальные поля анализа", outcome.Message);
            }

            Assert.Equal(0, assembler.CallCount);
            Assert.Equal(0, engine.CallCount);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.AiAnalysisResult)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Null(unchanged.AiAnalysisResult);
                Assert.Equal(AnalysisStatus.ReadyForAnalysis, unchanged.Status);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Theory]
    [InlineData(AiAnalysisResponseStatus.Succeeded, AiAnalysisResultStatus.Completed, AnalysisStatus.NeedsExpertEvaluation, "")]
    [InlineData(AiAnalysisResponseStatus.Partial, AiAnalysisResultStatus.CompletedWithWarnings, AnalysisStatus.NeedsExpertEvaluation, "missing optional context")]
    [InlineData(AiAnalysisResponseStatus.Failed, AiAnalysisResultStatus.Failed, AnalysisStatus.LlmAnalysisFailed, "provider unavailable")]
    public async Task RunAsync_MapsResponseStatusDiagnosticsRawAndImpactMap(
        AiAnalysisResponseStatus responseStatus,
        AiAnalysisResultStatus expectedResultStatus,
        AnalysisStatus expectedAnalysisStatus,
        string diagnostic)
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");
            var response = new AiAnalysisResponse(
                responseStatus,
                responseStatus == AiAnalysisResponseStatus.Failed ? null : CreateImpactMap(),
                $"raw {responseStatus}",
                string.IsNullOrWhiteSpace(diagnostic) ? [] : [diagnostic],
                AnalysisBoundaryNotice.Default);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(
                    dbContext,
                    new CapturingAssembler(),
                    new StubAiAnalysisEngine(response));

                await service.RunAsync(analysis.Id);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var saved = await dbContext.Analyses
                    .Include(candidate => candidate.AiAnalysisResult)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(expectedAnalysisStatus, saved.Status);
                Assert.NotNull(saved.AiAnalysisResult);
                Assert.Equal(expectedResultStatus, saved.AiAnalysisResult.Status);
                Assert.Equal($"raw {responseStatus}", saved.AiAnalysisResult.RawResponse);
                Assert.Equal(diagnostic, saved.AiAnalysisResult.ErrorMessage);
                Assert.Equal(responseStatus == AiAnalysisResponseStatus.Failed, saved.AiAnalysisResult.ImpactMap is null);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_MapsInvalidResponseFailureToInvalidResponseStatus()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(
                    dbContext,
                    new CapturingAssembler(),
                    new StubAiAnalysisEngine(new AiAnalysisResponse(
                        AiAnalysisResponseStatus.Failed,
                        null,
                        "raw invalid response",
                        ["LLM response is invalid: impact map is missing."],
                        AnalysisBoundaryNotice.Default)));

                await service.RunAsync(analysis.Id);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var saved = await dbContext.Analyses
                    .Include(candidate => candidate.AiAnalysisResult)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(AnalysisStatus.LlmAnalysisFailed, saved.Status);
                Assert.NotNull(saved.AiAnalysisResult);
                Assert.Equal(AiAnalysisResultStatus.InvalidResponse, saved.AiAnalysisResult.Status);
                Assert.Equal("raw invalid response", saved.AiAnalysisResult.RawResponse);
                Assert.Contains("impact map is missing", saved.AiAnalysisResult.ErrorMessage);
                Assert.Null(saved.AiAnalysisResult.ImpactMap);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_OverwritesExistingAiAnalysisResultBeforeExpertEvaluation()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");
            var existingResultId = Guid.NewGuid();
            analysis.AiAnalysisResult = new AiAnalysisResult
            {
                Id = existingResultId,
                AnalysisId = analysis.Id,
                Status = AiAnalysisResultStatus.Failed,
                RawResponse = "previous raw response",
                ErrorMessage = "previous diagnostics",
                InputSnapshot = "previous input"
            };

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(
                    dbContext,
                    new CapturingAssembler(),
                    new StubAiAnalysisEngine(new AiAnalysisResponse(
                        AiAnalysisResponseStatus.Succeeded,
                        CreateImpactMap(),
                        "new raw response",
                        [],
                        AnalysisBoundaryNotice.Default)));

                await service.RunAsync(analysis.Id);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var saved = await dbContext.Analyses
                    .Include(candidate => candidate.AiAnalysisResult)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.NotNull(saved.AiAnalysisResult);
                Assert.Equal(existingResultId, saved.AiAnalysisResult.Id);
                Assert.Equal(AiAnalysisResultStatus.Completed, saved.AiAnalysisResult.Status);
                Assert.Equal("new raw response", saved.AiAnalysisResult.RawResponse);
                Assert.DoesNotContain("previous", saved.AiAnalysisResult.InputSnapshot);
                Assert.Empty(saved.AiAnalysisResult.ErrorMessage);
                Assert.NotNull(saved.AiAnalysisResult.ImpactMap);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_AfterExpertEvaluationDoesNotCallEngineAndKeepsExistingAiAnalysisResult()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");
            var generatedAt = new DateTimeOffset(2026, 06, 13, 10, 00, 00, TimeSpan.Zero);
            analysis.AiAnalysisResult = CreateExistingAiAnalysisResult(analysis.Id, generatedAt);
            analysis.ExpertEvaluation = new ExpertEvaluation
            {
                AnalysisId = analysis.Id,
                ContextSufficiency = ContextSufficiencyRating.Sufficient,
                ResultUsefulness = ResultUsefulnessRating.Useful,
                GeneralComment = "Expert evaluation saved."
            };

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            var assembler = new CapturingAssembler();
            var engine = new StubAiAnalysisEngine(new AiAnalysisResponse(
                AiAnalysisResponseStatus.Succeeded,
                CreateImpactMap(),
                "new raw response",
                [],
                AnalysisBoundaryNotice.Default));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, assembler, engine);

                var outcome = await service.RunAsync(analysis.Id);

                Assert.Equal(AnalysisExecutionOutcomeKind.SnapshotLocked, outcome.Kind);
                Assert.Equal(AiAnalysisResultStatus.Completed, outcome.ResultStatus);
                Assert.Contains("экспертная оценка", outcome.Message, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Equal(0, assembler.CallCount);
            Assert.Equal(0, engine.CallCount);

            await AssertExistingAiAnalysisResultUnchangedAsync(options, analysis.Id, generatedAt);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_AfterExpertConclusionDoesNotCallEngineAndKeepsExistingAiAnalysisResult()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");
            var generatedAt = new DateTimeOffset(2026, 06, 13, 10, 00, 00, TimeSpan.Zero);
            analysis.AiAnalysisResult = CreateExistingAiAnalysisResult(analysis.Id, generatedAt);
            analysis.ExpertConclusion = new ExpertConclusion
            {
                AnalysisId = analysis.Id,
                ConclusionType = ExpertConclusionType.AcceptWithLimitations,
                Comment = "Human expert conclusion.",
                Rationale = "Accepted with rollout constraints.",
                FixedAt = generatedAt.AddMinutes(30)
            };

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            var assembler = new CapturingAssembler();
            var engine = new StubAiAnalysisEngine(new AiAnalysisResponse(
                AiAnalysisResponseStatus.Succeeded,
                CreateImpactMap(),
                "new raw response",
                [],
                AnalysisBoundaryNotice.Default));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, assembler, engine);

                var outcome = await service.RunAsync(analysis.Id);

                Assert.Equal(AnalysisExecutionOutcomeKind.SnapshotLocked, outcome.Kind);
                Assert.Equal(AiAnalysisResultStatus.Completed, outcome.ResultStatus);
                Assert.Contains("экспертное заключение", outcome.Message, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Equal(0, assembler.CallCount);
            Assert.Equal(0, engine.CallCount);

            await AssertExistingAiAnalysisResultUnchangedAsync(options, analysis.Id, generatedAt);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task RunAsync_AfterExportedMarkerDoesNotCallEngineAndKeepsExistingAiAnalysisResult()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis("Gateway migration");
            var generatedAt = new DateTimeOffset(2026, 06, 13, 10, 00, 00, TimeSpan.Zero);
            analysis.Status = AnalysisStatus.Exported;
            analysis.AiAnalysisResult = CreateExistingAiAnalysisResult(analysis.Id, generatedAt);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            var assembler = new CapturingAssembler();
            var engine = new StubAiAnalysisEngine(new AiAnalysisResponse(
                AiAnalysisResponseStatus.Succeeded,
                CreateImpactMap(),
                "new raw response",
                [],
                AnalysisBoundaryNotice.Default));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = CreateService(dbContext, assembler, engine);

                var outcome = await service.RunAsync(analysis.Id);

                Assert.Equal(AnalysisExecutionOutcomeKind.SnapshotLocked, outcome.Kind);
                Assert.Equal(AiAnalysisResultStatus.Completed, outcome.ResultStatus);
                Assert.Contains("экспортирован", outcome.Message, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Equal(0, assembler.CallCount);
            Assert.Equal(0, engine.CallCount);

            await AssertExistingAiAnalysisResultUnchangedAsync(options, analysis.Id, generatedAt);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static AnalysisExecutionService CreateService(
        ApplicationDbContext dbContext,
        IAnalysisInputAssembler assembler,
        IAiAnalysisEngine engine) =>
        CreateService(
            dbContext,
            assembler,
            new ModeAwareAiAnalysisEngineSelector(engine, engine));

    private static AnalysisExecutionService CreateService(
        ApplicationDbContext dbContext,
        IAnalysisInputAssembler assembler,
        IAiAnalysisEngineSelector analysisEngineSelector) =>
        new(
            dbContext,
            assembler,
            analysisEngineSelector,
            Options.Create(new AiAnalysisOptions
            {
                Provider = LlmProviderNames.Demo
            }));

    private static string CreateMockProfileName(string? mockProfileName) =>
        string.IsNullOrWhiteSpace(mockProfileName)
            ? "happy-path"
            : mockProfileName;

    private static string CreateMockModelWorkflowProfileName(string? mockProfileName) =>
        $"local-demo-model / mock-impact-analysis / {CreateMockProfileName(mockProfileName)}";

    private static string CreateExternalModelWorkflowProfileName(ExternalRagAdapterResponseMetadata metadata) =>
        string.Join(
            " / ",
            new[]
            {
                metadata.ModelName,
                metadata.WorkflowName,
                metadata.ProfileName
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static void AssertMockScenarioPersistence(
        AiAnalysisResultMetadata metadata,
        string? mockProfileName,
        int expectedRetrievedContextItemCount,
        bool expectedFailedResult,
        string errorMessage)
    {
        Assert.Equal(expectedRetrievedContextItemCount, metadata.RetrievedContextItems.Count);

        switch (CreateMockProfileName(mockProfileName))
        {
            case "happy-path":
                Assert.Empty(metadata.Warnings);
                Assert.Collection(
                    metadata.RetrievedContextItems,
                    item =>
                    {
                        Assert.Equal("Local demo requirement catalogue", item.SourceTitle);
                        Assert.Equal("local-demo-REQ-001", item.ExternalReference);
                        Assert.NotNull(item.Text);
                        Assert.Equal("Controlled change to an integration boundary requires expert review.", item.Excerpt);
                        Assert.Equal(RetrievedContextItemCompleteness.FullText, item.Completeness);
                        Assert.Equal(nameof(MockExternalRagAdapter), item.AdapterName);
                    },
                    item =>
                    {
                        Assert.Equal("Local demo decision log", item.SourceTitle);
                        Assert.Equal("local-demo-ADR-002", item.ExternalReference);
                        Assert.NotNull(item.Text);
                        Assert.Equal(RetrievedContextItemCompleteness.FullText, item.Completeness);
                        Assert.Equal(nameof(MockExternalRagAdapter), item.AdapterName);
                    });
                Assert.Empty(errorMessage);
                break;

            case "metadata-only":
                Assert.Equal(["Retrieved context contains source metadata only; source text was not returned."], metadata.Warnings);
                var metadataOnlyItem = Assert.Single(metadata.RetrievedContextItems);
                Assert.Equal("Local demo integration inventory", metadataOnlyItem.SourceTitle);
                Assert.Equal("local-demo-INV-003", metadataOnlyItem.ExternalReference);
                Assert.Null(metadataOnlyItem.Text);
                Assert.Null(metadataOnlyItem.Excerpt);
                Assert.Equal(RetrievedContextItemCompleteness.MetadataOnly, metadataOnlyItem.Completeness);
                Assert.Equal("Only source metadata is available in this mock scenario.", metadataOnlyItem.WarningOrLimitationNote);
                Assert.Equal(nameof(MockExternalRagAdapter), metadataOnlyItem.AdapterName);
                Assert.False(expectedFailedResult);
                break;

            case "unavailable":
                Assert.Equal(
                    ["Structured impact analysis is available, but retrieved context is unavailable in this mock scenario."],
                    metadata.Warnings);
                Assert.Empty(metadata.RetrievedContextItems);
                Assert.False(expectedFailedResult);
                break;

            case "partial":
                Assert.Equal(["Retrieved context is partial; full source text was not returned."], metadata.Warnings);
                var partialItem = Assert.Single(metadata.RetrievedContextItems);
                Assert.Equal("Local demo requirement excerpt", partialItem.SourceTitle);
                Assert.Equal("local-demo-REQ-004", partialItem.ExternalReference);
                Assert.Null(partialItem.Text);
                Assert.Equal("The requested change may affect an integration boundary.", partialItem.Excerpt);
                Assert.Equal(RetrievedContextItemCompleteness.ExcerptOnly, partialItem.Completeness);
                Assert.Equal("Full source text is not available in this mock scenario.", partialItem.WarningOrLimitationNote);
                Assert.Equal(nameof(MockExternalRagAdapter), partialItem.AdapterName);
                Assert.False(expectedFailedResult);
                break;

            case "failed":
                Assert.Equal(["Local mock external analysis returned a controlled failed response."], metadata.Warnings);
                Assert.Empty(metadata.RetrievedContextItems);
                Assert.Contains("mock_external_failure", errorMessage);
                Assert.Contains("Local mock external analysis did not produce an impact map.", errorMessage);
                Assert.True(expectedFailedResult);
                break;
        }
    }

    private static ImpactMap CreateImpactMap()
    {
        var impactMap = new ImpactMap
        {
            ChangeSummary =
            {
                Title = "Potential migration impact",
                Description = "Gateway requirements may change.",
                Severity = ImpactSeverity.Medium
            },
            PreliminaryAssessment =
            {
                Title = "Requires human expert review",
                Description = "Preliminary material only.",
                Severity = ImpactSeverity.Medium
            }
        };

        var risk = impactMap.AddRisk();
        risk.Title = "Downstream validation risk";
        risk.Description = "Affected integrations need expert validation.";
        risk.Severity = ImpactSeverity.Medium;

        return impactMap;
    }

    private static AiAnalysisResult CreateExistingAiAnalysisResult(
        Guid analysisId,
        DateTimeOffset generatedAt) =>
        new()
        {
            AnalysisId = analysisId,
            Status = AiAnalysisResultStatus.Completed,
            GeneratedAt = generatedAt,
            EngineName = "previous-engine",
            ProviderName = "previous-provider",
            ModelName = "previous-model",
            PromptVersion = "previous-prompt",
            InputSnapshot = "previous input snapshot",
            RawResponse = "previous raw response",
            ErrorMessage = "previous diagnostics",
            ImpactMap = CreateImpactMap()
        };

    private static async Task AssertExistingAiAnalysisResultUnchangedAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid analysisId,
        DateTimeOffset generatedAt)
    {
        await using var dbContext = new ApplicationDbContext(options);
        var saved = await dbContext.Analyses
            .Include(candidate => candidate.AiAnalysisResult)
            .SingleAsync(candidate => candidate.Id == analysisId);

        Assert.NotNull(saved.AiAnalysisResult);
        Assert.Equal(AiAnalysisResultStatus.Completed, saved.AiAnalysisResult.Status);
        Assert.Equal(generatedAt, saved.AiAnalysisResult.GeneratedAt);
        Assert.Equal("previous-engine", saved.AiAnalysisResult.EngineName);
        Assert.Equal("previous-provider", saved.AiAnalysisResult.ProviderName);
        Assert.Equal("previous-model", saved.AiAnalysisResult.ModelName);
        Assert.Equal("previous-prompt", saved.AiAnalysisResult.PromptVersion);
        Assert.Equal("previous input snapshot", saved.AiAnalysisResult.InputSnapshot);
        Assert.Equal("previous raw response", saved.AiAnalysisResult.RawResponse);
        Assert.Equal("previous diagnostics", saved.AiAnalysisResult.ErrorMessage);
        Assert.NotNull(saved.AiAnalysisResult.ImpactMap);
        Assert.Equal("Potential migration impact", saved.AiAnalysisResult.ImpactMap.ChangeSummary.Title);
    }

    private static Analysis CreateAnalysis(string title) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Status = AnalysisStatus.ReadyForAnalysis,
            OriginalDescription = $"Original requirement for {title}",
            ProjectRequest = $"Project request for {title}",
            SituationDescription = $"Situation for {title}",
            ChangeSource = $"Change source for {title}",
            CreatedAt = new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 06, 13, 08, 30, 00, TimeSpan.Zero)
        };

    private static ContextFragment CreateContextFragment(Guid analysisId) =>
        new()
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Type = ContextFragmentType.ArchitecturalConstraint,
            Source = "Architecture note",
            Text = "Keep gateway contract backward compatible.",
            CreatedAt = new DateTimeOffset(2026, 06, 13, 09, 00, 00, TimeSpan.Zero)
        };

    private static string CreateDatabasePath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"requirement-impact-assistant-task16-{Guid.NewGuid():N}.db");

    private static DbContextOptions<ApplicationDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

    private static IConfiguration CreateAnalysisConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiAnalysis:Provider"] = LlmProviderNames.Demo
            })
            .Build();

    private static IConfiguration CreateDifyAnalysisConfiguration(string apiKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiAnalysis:Provider"] = LlmProviderNames.Demo,
                [$"{DifyExternalRagOptions.SectionName}:Enabled"] = "true",
                [$"{DifyExternalRagOptions.SectionName}:Endpoint"] = "https://dify.invalid/workflows/run",
                [$"{DifyExternalRagOptions.SectionName}:WorkflowOrAppId"] = "workflow-from-options",
                [$"{DifyExternalRagOptions.SectionName}:ApiKey"] = apiKey,
                [$"{DifyExternalRagOptions.SectionName}:TimeoutSeconds"] = "30",
                [$"{DifyExternalRagOptions.SectionName}:ProfileName"] = "service-test-profile"
            })
            .Build();

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string content) =>
        new(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private static void DeleteDatabase(string databasePath)
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private sealed class ProfiledMockExternalRagAdapter(string? profileName) : IExternalRagAdapter
    {
        private readonly MockExternalRagAdapter inner = new();

        public int CallCount { get; private set; }

        public ExternalRagAdapterRequest? LastRequest { get; private set; }

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
            LastRequest = profiledRequest;

            return inner.AnalyzeAsync(profiledRequest, cancellationToken);
        }
    }

    private sealed class CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responseFactory(request);
        }
    }

    private sealed class CapturingFixtureExternalRagAdapter(ExternalRagAdapterResponse response) : IExternalRagAdapter
    {
        public int CallCount { get; private set; }

        public ExternalRagAdapterRequest? LastRequest { get; private set; }

        public Task<ExternalRagAdapterResponse> AnalyzeAsync(
            ExternalRagAdapterRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;

            return Task.FromResult(response);
        }
    }

    private sealed class CapturingAssembler : IAnalysisInputAssembler
    {
        private readonly AnalysisInputAssembler inner = new();

        public int CallCount { get; private set; }

        public Analysis? LastAnalysis { get; private set; }

        public AiAnalysisRequest Assemble(Analysis analysis)
        {
            CallCount++;
            LastAnalysis = analysis;

            return inner.Assemble(analysis);
        }
    }

    private sealed class StubAiAnalysisEngine(AiAnalysisResponse response) : IAiAnalysisEngine
    {
        public int CallCount { get; private set; }

        public AiAnalysisRequest? LastRequest { get; private set; }

        public Task<AiAnalysisResponse> AnalyzeAsync(
            AiAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;

            return Task.FromResult(response);
        }
    }

    private sealed class ModeAwareAiAnalysisEngineSelector(
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

    private sealed class CapturingLlmProvider(LlmProviderResponse response) : ILlmProvider
    {
        public int CallCount { get; private set; }

        public LlmProviderRequest? LastRequest { get; private set; }

        public Task<LlmProviderResponse> CompleteAsync(
            LlmProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;

            return Task.FromResult(response);
        }
    }
}

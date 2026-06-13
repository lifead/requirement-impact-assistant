using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class AnalysisExecutionServiceTests
{
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
                Assert.Contains("Minimum analysis fields", outcome.Message);
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
                Assert.Contains("expert evaluation", outcome.Message, StringComparison.OrdinalIgnoreCase);
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
                Assert.Contains("expert conclusion", outcome.Message, StringComparison.OrdinalIgnoreCase);
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
                Assert.Contains("exported", outcome.Message, StringComparison.OrdinalIgnoreCase);
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
        new(
            dbContext,
            assembler,
            engine,
            Options.Create(new AiAnalysisOptions
            {
                Provider = LlmProviderNames.Demo
            }));

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

    private static void DeleteDatabase(string databasePath)
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
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
}

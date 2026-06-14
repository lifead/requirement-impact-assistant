using System.Text;
using System.Text.Json;
using System.Reflection;
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

public sealed class AnalysisJsonExportServiceTests
{
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
            Assert.False(aiAnalysisResult.TryGetProperty("retrievedContext", out _));
            Assert.False(aiAnalysisResult.TryGetProperty("items", out _));
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
    public void Build_IncludesSavedExternalMetadataWithoutRetrievedContextItems()
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
                    SourceTitle = "Out-of-scope Task 4 source title",
                    Excerpt = "Out-of-scope Task 4 excerpt.",
                    ProviderName = "neutral-provider",
                    AdapterName = "neutral-adapter",
                    Completeness = RetrievedContextItemCompleteness.ExcerptOnly
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
        Assert.False(aiAnalysisResult.TryGetProperty("retrievedContext", out _));
        Assert.False(aiAnalysisResult.TryGetProperty("items", out _));
        Assert.DoesNotContain("Out-of-scope Task 4 source title", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Out-of-scope Task 4 excerpt.", json, StringComparison.Ordinal);
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

    private static async Task SaveAnalysisAsync(DbContextOptions<ApplicationDbContext> options, Analysis analysis)
    {
        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.MigrateAsync();
        dbContext.Analyses.Add(analysis);
        await dbContext.SaveChangesAsync();
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
}

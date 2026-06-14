using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;

namespace RequirementImpactAssistant.Tests.Data;

public sealed class ApplicationDbContextPersistenceTests
{
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

    private static DbContextOptions<ApplicationDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

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

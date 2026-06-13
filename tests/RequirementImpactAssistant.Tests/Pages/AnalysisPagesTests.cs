using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Pages.Analyses;

namespace RequirementImpactAssistant.Tests.Pages;

public sealed class AnalysisPagesTests
{
    [Fact]
    public async Task ListPage_LoadsAnalysesFromSqliteOrderedByUpdatedAtDescending()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var older = CreateAnalysis(
                "Older request",
                AnalysisStatus.Draft,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            var newer = CreateAnalysis(
                "Newer request",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 11, 00, 00, TimeSpan.Zero));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.AddRange(older, newer);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new IndexModel(dbContext);

                await pageModel.OnGetAsync();

                Assert.Collection(
                    pageModel.Analyses,
                    item =>
                    {
                        Assert.Equal(newer.Id, item.Id);
                        Assert.Equal("Newer request", item.Title);
                        Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, item.Status);
                    },
                    item =>
                    {
                        Assert.Equal(older.Id, item.Id);
                        Assert.Equal("Older request", item.Title);
                        Assert.Equal(AnalysisStatus.Draft, item.Status);
                    });
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task DetailsPage_OpensExistingAnalysisByIdFromSqlite()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.ReadyForAnalysis,
                new DateTimeOffset(2026, 06, 13, 09, 30, 00, TimeSpan.Zero));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext);

                var result = await pageModel.OnGetAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.NotNull(pageModel.Analysis);
                Assert.Equal(analysis.Id, pageModel.Analysis.Id);
                Assert.Equal("Gateway migration", pageModel.Analysis.Title);
                Assert.Equal(AnalysisStatus.ReadyForAnalysis, pageModel.Analysis.Status);
                Assert.Equal("Original requirement for Gateway migration", pageModel.Analysis.OriginalDescription);
                Assert.Equal("Project request for Gateway migration", pageModel.Analysis.ProjectRequest);
                Assert.Equal("Situation for Gateway migration", pageModel.Analysis.SituationDescription);
                Assert.Equal("Change source for Gateway migration", pageModel.Analysis.ChangeSource);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    private static string CreateDatabasePath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"requirement-impact-assistant-task5-{Guid.NewGuid():N}.db");

    private static DbContextOptions<ApplicationDbContext> CreateOptions(string databasePath) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

    private static Analysis CreateAnalysis(
        string title,
        AnalysisStatus status,
        DateTimeOffset updatedAt)
    {
        var createdAt = updatedAt.AddHours(-1);

        return new Analysis
        {
            Id = Guid.NewGuid(),
            Title = title,
            Status = status,
            OriginalDescription = $"Original requirement for {title}",
            ProjectRequest = $"Project request for {title}",
            SituationDescription = $"Situation for {title}",
            ChangeSource = $"Change source for {title}",
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
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

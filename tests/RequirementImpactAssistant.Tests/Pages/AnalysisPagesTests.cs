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

    [Fact]
    public async Task CreatePage_CreatesAnalysisAndRedirectsToReadOnlyDetails()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
            }

            Guid analysisId;

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new CreateModel(dbContext)
                {
                    Input = CreateInput("Payment API change")
                };

                var result = await pageModel.OnPostAsync();
                var redirect = Assert.IsType<RedirectToPageResult>(result);

                Assert.Equal("/Analyses/Details", redirect.PageName);
                analysisId = Assert.IsType<Guid>(redirect.RouteValues?["id"]);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var analysis = await dbContext.Analyses.SingleAsync(candidate => candidate.Id == analysisId);

                Assert.Equal("Payment API change", analysis.Title);
                Assert.Equal(AnalysisStatus.ReadyForAnalysis, analysis.Status);
                Assert.Equal("Original requirement for Payment API change", analysis.OriginalDescription);
                Assert.Equal("Project request for Payment API change", analysis.ProjectRequest);
                Assert.Equal("Situation for Payment API change", analysis.SituationDescription);
                Assert.Equal("Change source for Payment API change", analysis.ChangeSource);
                Assert.NotEqual(default, analysis.CreatedAt);
                Assert.Equal(analysis.CreatedAt, analysis.UpdatedAt);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task CreatePage_ReturnsValidationErrorsForMissingInput()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                var pageModel = new CreateModel(dbContext);

                var result = await pageModel.OnPostAsync();

                Assert.IsType<PageResult>(result);
                Assert.False(pageModel.ModelState.IsValid);
                Assert.Equal(5, pageModel.ModelState.ErrorCount);
                Assert.Empty(await dbContext.Analyses.ToListAsync());
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task EditPage_UpdatesAnalysisAndRedirectsToReadOnlyDetails()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Legacy request", AnalysisStatus.Draft, originalUpdatedAt);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new EditModel(dbContext)
                {
                    Input = CreateInput("Edited request")
                };

                var result = await pageModel.OnPostAsync(analysis.Id);
                var redirect = Assert.IsType<RedirectToPageResult>(result);

                Assert.Equal("/Analyses/Details", redirect.PageName);
                Assert.Equal(analysis.Id, redirect.RouteValues?["id"]);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updated = await dbContext.Analyses.SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal("Edited request", updated.Title);
                Assert.Equal("Original requirement for Edited request", updated.OriginalDescription);
                Assert.Equal("Project request for Edited request", updated.ProjectRequest);
                Assert.Equal("Situation for Edited request", updated.SituationDescription);
                Assert.Equal("Change source for Edited request", updated.ChangeSource);
                Assert.Equal(analysis.CreatedAt, updated.CreatedAt);
                Assert.True(updated.UpdatedAt > originalUpdatedAt);
                Assert.Equal(AnalysisStatus.ReadyForAnalysis, updated.Status);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task EditPage_ReturnsValidationErrorsWithoutChangingAnalysis()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Existing request", AnalysisStatus.Draft, originalUpdatedAt);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new EditModel(dbContext);

                var result = await pageModel.OnPostAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.False(pageModel.ModelState.IsValid);
                Assert.Equal(5, pageModel.ModelState.ErrorCount);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses.SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal("Existing request", unchanged.Title);
                Assert.Equal(originalUpdatedAt, unchanged.UpdatedAt);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task Pages_DisplayPassiveStatusWithoutChangingItOrStartingActions()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Incomplete request",
                AnalysisStatus.InputIncomplete,
                new DateTimeOffset(2026, 06, 13, 10, 00, 00, TimeSpan.Zero));
            analysis.ProjectRequest = string.Empty;

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new IndexModel(dbContext);

                await pageModel.OnGetAsync();

                var item = Assert.Single(pageModel.Analyses);
                Assert.Equal(AnalysisStatus.InputIncomplete, item.Status);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext);

                var result = await pageModel.OnGetAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.NotNull(pageModel.Analysis);
                Assert.Equal(AnalysisStatus.InputIncomplete, pageModel.Analysis.Status);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(AnalysisStatus.InputIncomplete, unchanged.Status);
                Assert.Empty(unchanged.ContextFragments);
                Assert.Null(unchanged.AiAnalysisResult);
                Assert.Null(unchanged.ExpertEvaluation);
                Assert.Null(unchanged.ExpertConclusion);
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

    private static AnalysisFormInput CreateInput(string title) =>
        new()
        {
            Title = title,
            OriginalDescription = $"Original requirement for {title}",
            ProjectRequest = $"Project request for {title}",
            SituationDescription = $"Situation for {title}",
            ChangeSource = $"Change source for {title}"
        };

    private static void DeleteDatabase(string databasePath)
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }
}

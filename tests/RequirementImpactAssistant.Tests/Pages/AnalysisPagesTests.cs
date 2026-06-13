using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using RequirementImpactAssistant.Web.Application.Analysis;
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
    public async Task DetailsPage_ListsContextFragmentsForCurrentAnalysis()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.ReadyForAnalysis,
                new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero));
            var otherAnalysis = CreateAnalysis(
                "Other request",
                AnalysisStatus.ReadyForAnalysis,
                new DateTimeOffset(2024, 01, 01, 09, 00, 00, TimeSpan.Zero));
            analysis.ContextFragments.Add(CreateContextFragment(
                analysis.Id,
                ContextFragmentType.Task,
                "Task tracker",
                "Earlier task context.",
                new DateTimeOffset(2024, 01, 01, 10, 00, 00, TimeSpan.Zero)));
            analysis.ContextFragments.Add(CreateContextFragment(
                analysis.Id,
                ContextFragmentType.ApiDescription,
                "API notes",
                "Latest API context.",
                new DateTimeOffset(2024, 01, 01, 11, 00, 00, TimeSpan.Zero)));
            otherAnalysis.ContextFragments.Add(CreateContextFragment(
                otherAnalysis.Id,
                ContextFragmentType.Comment,
                "Other source",
                "Foreign context.",
                new DateTimeOffset(2024, 01, 01, 12, 00, 00, TimeSpan.Zero)));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.AddRange(analysis, otherAnalysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext);

                var result = await pageModel.OnGetAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.NotNull(pageModel.Analysis);
                Assert.Collection(
                    pageModel.Analysis.ContextFragments,
                    item =>
                    {
                        Assert.Equal(ContextFragmentType.ApiDescription, item.Type);
                        Assert.Equal("API notes", item.Source);
                        Assert.Equal("Latest API context.", item.Text);
                    },
                    item =>
                    {
                        Assert.Equal(ContextFragmentType.Task, item.Type);
                        Assert.Equal("Task tracker", item.Source);
                        Assert.Equal("Earlier task context.", item.Text);
                    });
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReviewPage_OpensExistingAnalysisWithContextFromSqlite()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.ReadyForAnalysis,
                new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero));
            var fragment = CreateContextFragment(
                analysis.Id,
                ContextFragmentType.ApiDescription,
                "API notes",
                "Gateway endpoint contract.",
                new DateTimeOffset(2024, 01, 01, 10, 00, 00, TimeSpan.Zero));
            fragment.FileName = "gateway-api.md";
            analysis.ContextFragments.Add(fragment);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ReviewModel(dbContext);

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
                Assert.True(pageModel.Analysis.HasMinimumInput);

                var reviewedFragment = Assert.Single(pageModel.Analysis.ContextFragments);
                Assert.Equal(ContextFragmentType.ApiDescription, reviewedFragment.Type);
                Assert.Equal("API notes", reviewedFragment.Source);
                Assert.Equal("gateway-api.md", reviewedFragment.FileName);
                Assert.Equal("Gateway endpoint contract.", reviewedFragment.Text);
                Assert.Equal(fragment.CreatedAt, reviewedFragment.CreatedAt);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReviewPage_LoadsReadyAnalysisWithoutContext()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.ReadyForAnalysis,
                new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ReviewModel(dbContext);

                var result = await pageModel.OnGetAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.NotNull(pageModel.Analysis);
                Assert.True(pageModel.Analysis.HasMinimumInput);
                Assert.Empty(pageModel.Analysis.ContextFragments);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReviewPage_LoadsOnlyCurrentAnalysisContext()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.ReadyForAnalysis,
                new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero));
            var otherAnalysis = CreateAnalysis(
                "Other request",
                AnalysisStatus.ReadyForAnalysis,
                new DateTimeOffset(2024, 01, 01, 09, 00, 00, TimeSpan.Zero));
            analysis.ContextFragments.Add(CreateContextFragment(
                analysis.Id,
                ContextFragmentType.Task,
                "Task tracker",
                "Gateway task context.",
                new DateTimeOffset(2024, 01, 01, 10, 00, 00, TimeSpan.Zero)));
            otherAnalysis.ContextFragments.Add(CreateContextFragment(
                otherAnalysis.Id,
                ContextFragmentType.Comment,
                "Other source",
                "Foreign context.",
                new DateTimeOffset(2024, 01, 01, 11, 00, 00, TimeSpan.Zero)));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.AddRange(analysis, otherAnalysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ReviewModel(dbContext);

                var result = await pageModel.OnGetAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.NotNull(pageModel.Analysis);
                var fragment = Assert.Single(pageModel.Analysis.ContextFragments);
                Assert.Equal(ContextFragmentType.Task, fragment.Type);
                Assert.Equal("Task tracker", fragment.Source);
                Assert.Equal("Gateway task context.", fragment.Text);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReviewPage_DoesNotChangeAnalysisOrContext()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.NeedsExpertEvaluation, originalUpdatedAt);
            var fragment = CreateContextFragment(
                analysis.Id,
                ContextFragmentType.PreviousDecision,
                "Decision log",
                "Approved previous decision.",
                new DateTimeOffset(2024, 01, 01, 10, 00, 00, TimeSpan.Zero));
            analysis.ContextFragments.Add(fragment);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ReviewModel(dbContext);

                var result = await pageModel.OnGetAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.NotNull(pageModel.Analysis);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);
                var unchangedFragment = Assert.Single(unchanged.ContextFragments);

                Assert.Equal(originalUpdatedAt, unchanged.UpdatedAt);
                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, unchanged.Status);
                Assert.Equal(fragment.Id, unchangedFragment.Id);
                Assert.Equal(ContextFragmentType.PreviousDecision, unchangedFragment.Type);
                Assert.Equal("Decision log", unchangedFragment.Source);
                Assert.Equal("Approved previous decision.", unchangedFragment.Text);
                Assert.Equal(fragment.CreatedAt, unchangedFragment.CreatedAt);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReviewPage_RunAnalysisHandlerCallsApplicationServiceAndRedirectsToDetails()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.ReadyForAnalysis,
                new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = new CapturingAnalysisExecutionService(new AnalysisExecutionOutcome(
                    AnalysisExecutionOutcomeKind.Completed,
                    analysis.Id,
                    AiAnalysisResultStatus.Completed,
                    "Preliminary AI analysis completed."));
                var pageModel = new ReviewModel(dbContext, service);

                var result = await pageModel.OnPostRunAnalysisAsync(analysis.Id);
                var redirect = Assert.IsType<RedirectToPageResult>(result);

                Assert.Equal(analysis.Id, service.LastAnalysisId);
                Assert.Equal(1, service.CallCount);
                Assert.Equal("/Analyses/Details", redirect.PageName);
                Assert.Equal(analysis.Id, redirect.RouteValues?["id"]);
                Assert.Equal("Preliminary AI analysis completed.", pageModel.AnalysisRunMessage);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReviewPage_RunAnalysisHandlerStaysOnReviewWhenServiceReportsInvalidInput()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Incomplete request",
                AnalysisStatus.InputIncomplete,
                new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero));
            analysis.ProjectRequest = string.Empty;

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var service = new CapturingAnalysisExecutionService(new AnalysisExecutionOutcome(
                    AnalysisExecutionOutcomeKind.InvalidInput,
                    analysis.Id,
                    ResultStatus: null,
                    Message: "Minimum analysis fields are not fully filled."));
                var pageModel = new ReviewModel(dbContext, service);

                var result = await pageModel.OnPostRunAnalysisAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.Equal(analysis.Id, service.LastAnalysisId);
                Assert.Equal(1, service.CallCount);
                Assert.NotNull(pageModel.Analysis);
                Assert.False(pageModel.ModelState.IsValid);
                Assert.Contains(
                    pageModel.ModelState[string.Empty]!.Errors,
                    error => error.ErrorMessage.Contains("Minimum analysis fields", StringComparison.Ordinal));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task DetailsPage_AddsManualContextFragmentAndUpdatesAnalysisWithoutChangingStatus()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.InputIncomplete, originalUpdatedAt);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext)
                {
                    ContextFragmentInput = new DetailsModel.ManualContextFragmentInput
                    {
                        Type = ContextFragmentType.ArchitecturalConstraint,
                        Source = "  Architecture note  ",
                        Text = "  Keep the gateway contract backward compatible.  "
                    }
                };

                var result = await pageModel.OnPostAddContextFragmentAsync(analysis.Id);
                var redirect = Assert.IsType<RedirectToPageResult>(result);

                Assert.Equal("/Analyses/Details", redirect.PageName);
                Assert.Equal(analysis.Id, redirect.RouteValues?["id"]);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updated = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);
                var fragment = Assert.Single(updated.ContextFragments);

                Assert.Equal(ContextFragmentType.ArchitecturalConstraint, fragment.Type);
                Assert.Equal("Architecture note", fragment.Source);
                Assert.Equal("Keep the gateway contract backward compatible.", fragment.Text);
                Assert.Null(fragment.FileName);
                Assert.Null(fragment.FilePath);
                Assert.NotEqual(default, fragment.CreatedAt);
                Assert.True(updated.UpdatedAt > originalUpdatedAt);
                Assert.Equal(AnalysisStatus.InputIncomplete, updated.Status);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task DetailsPage_ReturnsValidationErrorsForMissingManualContextInput()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.ReadyForAnalysis, originalUpdatedAt);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext);

                var result = await pageModel.OnPostAddContextFragmentAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.False(pageModel.ModelState.IsValid);
                Assert.Equal(2, pageModel.ModelState.ErrorCount);
                Assert.NotNull(pageModel.Analysis);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(originalUpdatedAt, unchanged.UpdatedAt);
                Assert.Empty(unchanged.ContextFragments);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Theory]
    [InlineData("context.md")]
    [InlineData("context.TXT")]
    [InlineData("context.JsOn")]
    public async Task DetailsPage_UploadsAllowedContextFileExtensionsCaseInsensitive(string fileName)
    {
        var databasePath = CreateDatabasePath();
        var contentRootPath = CreateContentRootPath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.ReadyForAnalysis,
                new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext, new TestWebHostEnvironment(contentRootPath))
                {
                    UploadContextFragmentInput = new DetailsModel.FileContextFragmentInput
                    {
                        Type = ContextFragmentType.Task,
                        File = CreateUploadFile(fileName, "Uploaded context text.")
                    }
                };

                var result = await pageModel.OnPostUploadContextFragmentAsync(analysis.Id);

                Assert.IsType<RedirectToPageResult>(result);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updated = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);
                var fragment = Assert.Single(updated.ContextFragments);

                Assert.Equal(fileName, fragment.FileName);
                Assert.Equal(fileName, fragment.Source);
                Assert.Equal("Uploaded context text.", fragment.Text);
                Assert.True(File.Exists(Path.Combine(contentRootPath, fragment.FilePath!)));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
            DeleteDirectory(contentRootPath);
        }
    }

    [Fact]
    public async Task DetailsPage_RejectsUnsupportedUploadExtensionWithoutCreatingFragmentOrFile()
    {
        var databasePath = CreateDatabasePath();
        var contentRootPath = CreateContentRootPath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.ReadyForAnalysis, originalUpdatedAt);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext, new TestWebHostEnvironment(contentRootPath))
                {
                    UploadContextFragmentInput = new DetailsModel.FileContextFragmentInput
                    {
                        Type = ContextFragmentType.Task,
                        File = CreateUploadFile("context.pdf", "Unsupported context text.")
                    }
                };

                var result = await pageModel.OnPostUploadContextFragmentAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.False(pageModel.ModelState.IsValid);
                Assert.NotNull(pageModel.Analysis);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(originalUpdatedAt, unchanged.UpdatedAt);
                Assert.Empty(unchanged.ContextFragments);
                Assert.False(Directory.Exists(Path.Combine(contentRootPath, "data", "uploads")));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
            DeleteDirectory(contentRootPath);
        }
    }

    [Fact]
    public async Task DetailsPage_RejectsOversizedUploadWithoutCreatingFragmentOrFile()
    {
        var databasePath = CreateDatabasePath();
        var contentRootPath = CreateContentRootPath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.ReadyForAnalysis, originalUpdatedAt);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext, new TestWebHostEnvironment(contentRootPath))
                {
                    UploadContextFragmentInput = new DetailsModel.FileContextFragmentInput
                    {
                        Type = ContextFragmentType.Task,
                        File = CreateUploadFile("context.md", new byte[1_048_577])
                    }
                };

                var result = await pageModel.OnPostUploadContextFragmentAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.False(pageModel.ModelState.IsValid);
                Assert.NotNull(pageModel.Analysis);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(originalUpdatedAt, unchanged.UpdatedAt);
                Assert.Empty(unchanged.ContextFragments);
                Assert.False(Directory.Exists(Path.Combine(contentRootPath, "data", "uploads")));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
            DeleteDirectory(contentRootPath);
        }
    }

    [Fact]
    public async Task DetailsPage_UploadsContextFileAndStoresMetadataTextAndUpdatedAtWithoutChangingStatus()
    {
        var databasePath = CreateDatabasePath();
        var contentRootPath = CreateContentRootPath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.InputIncomplete, originalUpdatedAt);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext, new TestWebHostEnvironment(contentRootPath))
                {
                    UploadContextFragmentInput = new DetailsModel.FileContextFragmentInput
                    {
                        Type = ContextFragmentType.ApiDescription,
                        Source = "  API file source  ",
                        File = CreateUploadFile("payment-api.JSON", "{\"endpoint\":\"/payments\"}")
                    }
                };
                pageModel.ModelState.AddModelError("ContextFragmentInput.Source", "Source is required.");
                pageModel.ModelState.AddModelError("ContextFragmentInput.Text", "Text is required.");

                var result = await pageModel.OnPostUploadContextFragmentAsync(analysis.Id);
                var redirect = Assert.IsType<RedirectToPageResult>(result);

                Assert.Equal("/Analyses/Details", redirect.PageName);
                Assert.Equal(analysis.Id, redirect.RouteValues?["id"]);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updated = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);
                var fragment = Assert.Single(updated.ContextFragments);
                var savedPath = Path.Combine(contentRootPath, fragment.FilePath!);

                Assert.Equal(ContextFragmentType.ApiDescription, fragment.Type);
                Assert.Equal("API file source", fragment.Source);
                Assert.Equal("payment-api.JSON", fragment.FileName);
                Assert.Equal("{\"endpoint\":\"/payments\"}", fragment.Text);
                Assert.StartsWith($"data/uploads/{analysis.Id}/", fragment.FilePath);
                Assert.EndsWith(".json", fragment.FilePath);
                Assert.Equal("{\"endpoint\":\"/payments\"}", await File.ReadAllTextAsync(savedPath));
                Assert.True(updated.UpdatedAt > originalUpdatedAt);
                Assert.Equal(AnalysisStatus.InputIncomplete, updated.Status);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
            DeleteDirectory(contentRootPath);
        }
    }

    [Fact]
    public async Task DetailsPage_UsesSafeUniqueStoredFileNamesWithoutOverwritingExistingUpload()
    {
        var databasePath = CreateDatabasePath();
        var contentRootPath = CreateContentRootPath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.ReadyForAnalysis,
                new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                const string unsafeFileName = @"..\nested/shared-name.md";
                var pageModel = new DetailsModel(dbContext, new TestWebHostEnvironment(contentRootPath))
                {
                    UploadContextFragmentInput = new DetailsModel.FileContextFragmentInput
                    {
                        Type = ContextFragmentType.Task,
                        File = CreateUploadFile(unsafeFileName, "First upload.")
                    }
                };

                await pageModel.OnPostUploadContextFragmentAsync(analysis.Id);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                const string unsafeFileName = @"..\nested/shared-name.md";
                var pageModel = new DetailsModel(dbContext, new TestWebHostEnvironment(contentRootPath))
                {
                    UploadContextFragmentInput = new DetailsModel.FileContextFragmentInput
                    {
                        Type = ContextFragmentType.Task,
                        File = CreateUploadFile(unsafeFileName, "Second upload.")
                    }
                };

                await pageModel.OnPostUploadContextFragmentAsync(analysis.Id);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var fragments = await dbContext.ContextFragments
                    .Where(candidate => candidate.AnalysisId == analysis.Id)
                    .ToListAsync();

                Assert.Equal(2, fragments.Count);
                Assert.All(fragments, fragment =>
                {
                    Assert.Equal("shared-name.md", fragment.FileName);
                    Assert.DoesNotContain("..", fragment.FileName);
                    Assert.DoesNotContain("/", fragment.FileName);
                    Assert.DoesNotContain("\\", fragment.FileName);
                    Assert.DoesNotContain("shared-name", fragment.FilePath);
                    Assert.DoesNotContain("..", fragment.FilePath);
                    Assert.True(File.Exists(Path.Combine(contentRootPath, fragment.FilePath!)));
                });
                Assert.NotEqual(fragments[0].FilePath, fragments[1].FilePath);
                Assert.Contains(
                    fragments,
                    fragment => File.ReadAllText(Path.Combine(contentRootPath, fragment.FilePath!)) == "First upload.");
                Assert.Contains(
                    fragments,
                    fragment => File.ReadAllText(Path.Combine(contentRootPath, fragment.FilePath!)) == "Second upload.");
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
            DeleteDirectory(contentRootPath);
        }
    }

    [Fact]
    public async Task DetailsPage_RemovesStoredUploadFileWhenDatabaseSaveFails()
    {
        var databasePath = CreateDatabasePath();
        var contentRootPath = CreateContentRootPath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.ReadyForAnalysis,
                new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext, new TestWebHostEnvironment(contentRootPath))
                {
                    UploadContextFragmentInput = new DetailsModel.FileContextFragmentInput
                    {
                        Type = ContextFragmentType.Task,
                        File = new CallbackFormFile(
                            "context.md",
                            "Uploaded context text.",
                            () => DropContextFragmentsTable(databasePath))
                    }
                };

                await Assert.ThrowsAsync<DbUpdateException>(
                    () => pageModel.OnPostUploadContextFragmentAsync(analysis.Id));

                var uploadsRoot = Path.Combine(contentRootPath, "data", "uploads");
                Assert.False(Directory.Exists(uploadsRoot) && Directory.EnumerateFiles(uploadsRoot, "*", SearchOption.AllDirectories).Any());
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
            DeleteDirectory(contentRootPath);
        }
    }

    [Fact]
    public async Task DetailsPage_DeletesManualContextFragmentAndUpdatesAnalysisWithoutChangingStatus()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.NeedsExpertEvaluation, originalUpdatedAt);
            var fragment = CreateContextFragment(
                analysis.Id,
                ContextFragmentType.TestCase,
                "Regression suite",
                "Gateway regression case.",
                new DateTimeOffset(2024, 01, 01, 09, 00, 00, TimeSpan.Zero));
            analysis.ContextFragments.Add(fragment);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext);

                var result = await pageModel.OnPostDeleteContextFragmentAsync(analysis.Id, fragment.Id);
                var redirect = Assert.IsType<RedirectToPageResult>(result);

                Assert.Equal("/Analyses/Details", redirect.PageName);
                Assert.Equal(analysis.Id, redirect.RouteValues?["id"]);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updated = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Empty(updated.ContextFragments);
                Assert.True(updated.UpdatedAt > originalUpdatedAt);
                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, updated.Status);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task DetailsPage_DeletesFileBackedContextFragmentAndStoredFile()
    {
        var databasePath = CreateDatabasePath();
        var contentRootPath = CreateContentRootPath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.NeedsExpertEvaluation, originalUpdatedAt);
            var filePath = Path.Combine("data", "uploads", analysis.Id.ToString(), "stored-file.md");
            var absoluteFilePath = Path.Combine(contentRootPath, filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteFilePath)!);
            await File.WriteAllTextAsync(absoluteFilePath, "Stored file text.");

            var fragment = CreateContextFragment(
                analysis.Id,
                ContextFragmentType.TestCase,
                "Stored file source",
                "Stored file text.",
                new DateTimeOffset(2024, 01, 01, 09, 00, 00, TimeSpan.Zero));
            fragment.FileName = "uploaded-file.md";
            fragment.FilePath = filePath;
            analysis.ContextFragments.Add(fragment);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext, new TestWebHostEnvironment(contentRootPath));

                var result = await pageModel.OnPostDeleteContextFragmentAsync(analysis.Id, fragment.Id);

                Assert.IsType<RedirectToPageResult>(result);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updated = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Empty(updated.ContextFragments);
                Assert.False(File.Exists(absoluteFilePath));
                Assert.True(updated.UpdatedAt > originalUpdatedAt);
                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, updated.Status);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
            DeleteDirectory(contentRootPath);
        }
    }

    [Fact]
    public async Task DetailsPage_KeepsFileBackedContextFragmentWhenStoredFileDeleteFails()
    {
        var databasePath = CreateDatabasePath();
        var contentRootPath = CreateContentRootPath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.NeedsExpertEvaluation, originalUpdatedAt);
            var filePath = Path.Combine("data", "uploads", analysis.Id.ToString(), "stored-file.md");
            var absoluteFilePath = Path.Combine(contentRootPath, filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteFilePath)!);
            await File.WriteAllTextAsync(absoluteFilePath, "Stored file text.");

            var fragment = CreateContextFragment(
                analysis.Id,
                ContextFragmentType.TestCase,
                "Stored file source",
                "Stored file text.",
                new DateTimeOffset(2024, 01, 01, 09, 00, 00, TimeSpan.Zero));
            fragment.FileName = "uploaded-file.md";
            fragment.FilePath = filePath;
            analysis.ContextFragments.Add(fragment);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext, new TestWebHostEnvironment(contentRootPath))
                {
                    DeleteStoredUploadFile = _ => throw new IOException("Simulated delete failure.")
                };

                await Assert.ThrowsAsync<IOException>(
                    () => pageModel.OnPostDeleteContextFragmentAsync(analysis.Id, fragment.Id));
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);
                var unchangedFragment = Assert.Single(unchanged.ContextFragments);

                Assert.Equal(originalUpdatedAt, unchanged.UpdatedAt);
                Assert.Equal(fragment.Id, unchangedFragment.Id);
                Assert.True(File.Exists(absoluteFilePath));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
            DeleteDirectory(contentRootPath);
        }
    }

    [Fact]
    public async Task DetailsPage_DoesNotDeleteStoredFileOutsideCurrentAnalysisUploadFolder()
    {
        var databasePath = CreateDatabasePath();
        var contentRootPath = CreateContentRootPath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.NeedsExpertEvaluation, originalUpdatedAt);
            var otherAnalysis = CreateAnalysis("Billing migration", AnalysisStatus.NeedsExpertEvaluation, originalUpdatedAt);
            var otherFilePath = Path.Combine("data", "uploads", otherAnalysis.Id.ToString(), "stored-file.md");
            var otherAbsoluteFilePath = Path.Combine(contentRootPath, otherFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(otherAbsoluteFilePath)!);
            await File.WriteAllTextAsync(otherAbsoluteFilePath, "Other analysis stored file text.");

            var fragment = CreateContextFragment(
                analysis.Id,
                ContextFragmentType.TestCase,
                "Stored file source",
                "Stored file text.",
                new DateTimeOffset(2024, 01, 01, 09, 00, 00, TimeSpan.Zero));
            fragment.FileName = "uploaded-file.md";
            fragment.FilePath = otherFilePath;
            analysis.ContextFragments.Add(fragment);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.AddRange(analysis, otherAnalysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext, new TestWebHostEnvironment(contentRootPath));

                var result = await pageModel.OnPostDeleteContextFragmentAsync(analysis.Id, fragment.Id);

                Assert.IsType<RedirectToPageResult>(result);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updated = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Empty(updated.ContextFragments);
                Assert.True(updated.UpdatedAt > originalUpdatedAt);
                Assert.True(File.Exists(otherAbsoluteFilePath));
                Assert.Equal("Other analysis stored file text.", await File.ReadAllTextAsync(otherAbsoluteFilePath));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
            DeleteDirectory(contentRootPath);
        }
    }

    [Fact]
    public async Task DetailsPage_DoesNotDeleteContextFragmentFromAnotherAnalysis()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var firstUpdatedAt = new DateTimeOffset(2024, 01, 01, 08, 00, 00, TimeSpan.Zero);
            var secondUpdatedAt = new DateTimeOffset(2024, 01, 01, 09, 00, 00, TimeSpan.Zero);
            var firstAnalysis = CreateAnalysis("Gateway migration", AnalysisStatus.ReadyForAnalysis, firstUpdatedAt);
            var secondAnalysis = CreateAnalysis("Billing migration", AnalysisStatus.ReadyForAnalysis, secondUpdatedAt);
            var foreignFragment = CreateContextFragment(
                secondAnalysis.Id,
                ContextFragmentType.PreviousDecision,
                "Decision log",
                "Billing decision context.",
                new DateTimeOffset(2024, 01, 01, 10, 00, 00, TimeSpan.Zero));
            secondAnalysis.ContextFragments.Add(foreignFragment);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.AddRange(firstAnalysis, secondAnalysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new DetailsModel(dbContext);

                var result = await pageModel.OnPostDeleteContextFragmentAsync(firstAnalysis.Id, foreignFragment.Id);

                Assert.IsType<NotFoundResult>(result);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchangedFirst = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == firstAnalysis.Id);
                var unchangedSecond = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .SingleAsync(candidate => candidate.Id == secondAnalysis.Id);

                Assert.Equal(firstUpdatedAt, unchangedFirst.UpdatedAt);
                Assert.Empty(unchangedFirst.ContextFragments);
                Assert.Equal(secondUpdatedAt, unchangedSecond.UpdatedAt);
                Assert.Equal(foreignFragment.Id, Assert.Single(unchangedSecond.ContextFragments).Id);
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

    private static string CreateContentRootPath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"requirement-impact-assistant-content-{Guid.NewGuid():N}");

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

    private static ContextFragment CreateContextFragment(
        Guid analysisId,
        ContextFragmentType type,
        string source,
        string text,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            AnalysisId = analysisId,
            Type = type,
            Source = source,
            Text = text,
            CreatedAt = createdAt
        };

    private static IFormFile CreateUploadFile(string fileName, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        return CreateUploadFile(fileName, bytes);
    }

    private static IFormFile CreateUploadFile(string fileName, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, bytes.Length, "UploadContextFragmentInput.File", fileName);
    }

    private static void DropContextFragmentsTable(string databasePath)
    {
        SqliteConnection.ClearAllPools();

        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE ContextFragments";
        command.ExecuteNonQuery();
    }

    private static void DeleteDatabase(string databasePath)
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private static void DeleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = contentRootPath;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ApplicationName { get; set; } = "RequirementImpactAssistant.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = "Development";
    }

    private sealed class CapturingAnalysisExecutionService(AnalysisExecutionOutcome outcome)
        : IAnalysisExecutionService
    {
        public int CallCount { get; private set; }

        public Guid LastAnalysisId { get; private set; }

        public Task<AnalysisExecutionOutcome> RunAsync(
            Guid analysisId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastAnalysisId = analysisId;

            return Task.FromResult(outcome);
        }
    }

    private sealed class CallbackFormFile(string fileName, string content, Action afterCopy) : IFormFile
    {
        private readonly byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);

        public string ContentType => "text/markdown";

        public string ContentDisposition => string.Empty;

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public long Length => bytes.Length;

        public string Name => "UploadContextFragmentInput.File";

        public string FileName => fileName;

        public void CopyTo(Stream target)
        {
            target.Write(bytes);
            afterCopy();
        }

        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            await target.WriteAsync(bytes, cancellationToken);
            afterCopy();
        }

        public Stream OpenReadStream() => new MemoryStream(bytes);
    }
}

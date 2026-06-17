using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Data;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;
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
                Assert.Equal(ProjectRequestType.NewFunctionality, pageModel.Analysis.ProjectRequestType);
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
            var fileBackedFragment = CreateContextFragment(
                analysis.Id,
                ContextFragmentType.ApiDescription,
                "API notes",
                "Latest API context.",
                new DateTimeOffset(2024, 01, 01, 11, 00, 00, TimeSpan.Zero));
            fileBackedFragment.FileName = "gateway-api.md";
            fileBackedFragment.FilePath = $"data/uploads/{analysis.Id}/gateway-api.md";
            analysis.ContextFragments.Add(fileBackedFragment);
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
                        Assert.Equal("gateway-api.md", item.FileName);
                        Assert.Equal($"data/uploads/{analysis.Id}/gateway-api.md", item.FilePath);
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
    public async Task DetailsPage_BuildsPassiveSemanticStatusSummaryFromSavedData()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.ExpertConclusionFixed,
                new DateTimeOffset(2026, 06, 13, 09, 30, 00, TimeSpan.Zero));
            analysis.ContextFragments.Add(CreateContextFragment(
                analysis.Id,
                ContextFragmentType.Task,
                "Task tracker",
                "Manual context.",
                new DateTimeOffset(2026, 06, 13, 09, 45, 00, TimeSpan.Zero)));
            var aiResult = CreateAiAnalysisResult(
                analysis.Id,
                AiAnalysisResultStatus.CompletedWithWarnings,
                CreateImpactMap());
            aiResult.Metadata = new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = "external-analysis-engine",
                RetrievedContextState = RetrievedContextState.Partial,
                Warnings = ["Retrieved context was partial."]
            };
            analysis.AiAnalysisResult = aiResult;
            analysis.ExpertEvaluation = CreateExpertEvaluation(analysis.Id);
            analysis.ExpertConclusion = new ExpertConclusion
            {
                AnalysisId = analysis.Id,
                ConclusionType = ExpertConclusionType.AcceptWithLimitations,
                Comment = "Accepted with limitations.",
                Rationale = "Human expert confirmed the material.",
                FixedAt = new DateTimeOffset(2026, 06, 13, 10, 00, 00, TimeSpan.Zero)
            };

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
                Assert.Collection(
                    pageModel.Analysis.StatusSummaryItems,
                    item =>
                    {
                        Assert.Equal("input", item.Key);
                        Assert.Equal("Входные данные", item.Title);
                        Assert.Equal("Экспертное заключение зафиксировано", item.StatusLabel);
                        Assert.Equal("analysis-input", item.Anchor);
                        Assert.Equal("/Analyses/Review", Assert.Single(item.Actions).PageName);
                    },
                    item =>
                    {
                        Assert.Equal("manual-context", item.Key);
                        Assert.Equal("Добавлено: 1", item.StatusLabel);
                        Assert.Empty(item.Actions);
                    },
                    item =>
                    {
                        Assert.Equal("preliminary-result", item.Key);
                        Assert.Equal("Завершено с предупреждениями", item.StatusLabel);
                        Assert.Empty(item.Actions);
                    },
                    item =>
                    {
                        Assert.Equal("grounds-limitations", item.Key);
                        Assert.Equal("Предупреждения: 1", item.StatusLabel);
                        Assert.Contains("External AI/RAG", item.Description, StringComparison.Ordinal);
                    },
                    item =>
                    {
                        Assert.Equal("expert-evaluation", item.Key);
                        Assert.Equal("Зафиксирована", item.StatusLabel);
                        Assert.Equal("/Analyses/ExpertEvaluation", Assert.Single(item.Actions).PageName);
                    },
                    item =>
                    {
                        Assert.Equal("expert-conclusion", item.Key);
                        Assert.Equal("Зафиксировано", item.StatusLabel);
                        Assert.Equal("/Analyses/ExpertConclusion", Assert.Single(item.Actions).PageName);
                    },
                    item =>
                    {
                        Assert.Equal("export", item.Key);
                        Assert.Equal("Доступен", item.StatusLabel);
                        Assert.Collection(
                            item.Actions,
                            action => Assert.Equal("ExportJson", action.HandlerName),
                            action => Assert.Equal("ExportMarkdown", action.HandlerName));
                    });
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.ContextFragments)
                    .Include(candidate => candidate.AiAnalysisResult)
                    .Include(candidate => candidate.ExpertEvaluation)
                    .Include(candidate => candidate.ExpertConclusion)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(AnalysisStatus.ExpertConclusionFixed, unchanged.Status);
                Assert.Single(unchanged.ContextFragments);
                Assert.NotNull(unchanged.AiAnalysisResult);
                Assert.NotNull(unchanged.ExpertEvaluation);
                Assert.NotNull(unchanged.ExpertConclusion);
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
                Assert.Equal(ProjectRequestType.NewFunctionality, pageModel.Analysis.ProjectRequestType);
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
    public void ReviewPage_SourceRendersAnalysisModeSelectionControls()
    {
        var source = ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Review.cshtml");

        Assert.Contains("name=\"Input.AnalysisMode\"", source, StringComparison.Ordinal);
        Assert.Contains("value=\"@nameof(AnalysisMode.DirectLlm)\"", source, StringComparison.Ordinal);
        Assert.Contains("value=\"@nameof(AnalysisMode.ExternalRag)\"", source, StringComparison.Ordinal);
        Assert.Contains("? nameof(AnalysisMode.DirectLlm)", source, StringComparison.Ordinal);
        Assert.Contains(
            "checked=\"@(selectedAnalysisMode == AnalysisMode.DirectLlm)\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains("Выбранный режим:", source, StringComparison.Ordinal);
        Assert.Contains("AnalysisUiText.AnalysisModeLabel(AnalysisMode.DirectLlm)", source, StringComparison.Ordinal);
        Assert.Contains("AnalysisUiText.AnalysisModeLabel(AnalysisMode.ExternalRag)", source, StringComparison.Ordinal);
        Assert.Contains("AnalysisUiText.AnalysisModeReviewDescription(selectedAnalysisMode)", source, StringComparison.Ordinal);
        Assert.Contains("AnalysisUiText.AnalysisModeReviewDescription(AnalysisMode.DirectLlm)", source, StringComparison.Ordinal);
        Assert.Contains("AnalysisUiText.AnalysisModeReviewDescription(AnalysisMode.ExternalRag)", source, StringComparison.Ordinal);
        Assert.Contains("не выполняет сетевую проверку", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewPage_ModeDescriptionsAreProviderNeutralAndDoNotMakeDirectModeLookBroken()
    {
        var directDescription = AnalysisUiText.AnalysisModeReviewDescription(AnalysisMode.DirectLlm);
        var externalDescription = AnalysisUiText.AnalysisModeReviewDescription(AnalysisMode.ExternalRag);
        var combinedDescription = directDescription + Environment.NewLine + externalDescription;

        Assert.Contains("настроенный в приложении LLM provider", directDescription, StringComparison.Ordinal);
        Assert.Contains("без проверки внешнего AI/RAG-контура", directDescription, StringComparison.Ordinal);
        Assert.DoesNotContain("недоступ", directDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mock fallback", externalDescription, StringComparison.Ordinal);
        Assert.Contains("внешний adapter", externalDescription, StringComparison.Ordinal);
        Assert.Contains("только при запуске анализа", externalDescription, StringComparison.Ordinal);

        Assert.All(
            new[]
            {
                "Dify",
                "DeepSeek",
                "endpoint",
                "api key",
                "bearer",
                "cookie",
                "csrf",
                "raw provider payload"
            },
            token => Assert.DoesNotContain(token, combinedDescription, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReviewPageModel_DependsOnlyOnApplicationExecutionBoundaryForAnalysisRun()
    {
        var constructorParameterTypes = typeof(ReviewModel)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(typeof(ApplicationDbContext), constructorParameterTypes);
        Assert.Contains(typeof(IAnalysisExecutionService), constructorParameterTypes);

        Assert.All(
            new[]
            {
                "DifyExternalRagAdapter",
                "DifyExternalRagOptions",
                "IExternalRagAdapter",
                "ILlmProvider",
                "HttpClient"
            },
            forbiddenTypeName => Assert.DoesNotContain(
                constructorParameterTypes,
                parameterType => string.Equals(parameterType.Name, forbiddenTypeName, StringComparison.Ordinal)));
    }

    [Fact]
    public void ReviewPage_SourceDoesNotReferenceProviderAdaptersNetworkOrSecretBearingUi()
    {
        var combinedSource = string.Join(
            Environment.NewLine,
            ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Review.cshtml"),
            ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Review.cshtml.cs"));

        Assert.Contains("IAnalysisExecutionService", combinedSource, StringComparison.Ordinal);

        Assert.All(
            new[]
            {
                "DifyExternalRagAdapter",
                "DifyExternalRagOptions",
                "IExternalRagAdapter",
                "ILlmProvider",
                "HttpClient",
                "DifyWorkflow",
                "DifyAgent",
                "Endpoint",
                "ApiKey",
                "Bearer",
                "Cookie",
                "CSRF",
                "RawResponse",
                "RawPayload",
                "Authorization"
            },
            token => Assert.DoesNotContain(token, combinedSource, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InputFieldUiText_UsesMvp3Terminology()
    {
        Assert.Equal("Текущее состояние", AnalysisUiText.OriginalDescriptionLabel);
        Assert.Equal("Проектное изменение", AnalysisUiText.ProjectRequestLabel);
        Assert.Equal("Ситуация и причина изменения", AnalysisUiText.SituationDescriptionLabel);
        Assert.Equal("Источник изменения", AnalysisUiText.ChangeSourceLabel);

        Assert.Contains("точку отсчета", AnalysisUiText.OriginalDescriptionHelpText, StringComparison.Ordinal);
        Assert.Contains("без предположения, что оно уже принято", AnalysisUiText.ProjectRequestHelpText, StringComparison.Ordinal);
        Assert.Contains("причину", AnalysisUiText.SituationDescriptionHelpText, StringComparison.Ordinal);
        Assert.Contains("источник запроса", AnalysisUiText.ChangeSourceHelpText, StringComparison.Ordinal);
    }

    [Fact]
    public void InputFieldPageSourcesUseCentralizedUiText()
    {
        var sources = new[]
        {
            ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Create.cshtml"),
            ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Edit.cshtml"),
            ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml"),
            ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Review.cshtml")
        };

        Assert.All(
            sources,
            source =>
            {
                Assert.Contains("AnalysisUiText.OriginalDescriptionLabel", source, StringComparison.Ordinal);
                Assert.Contains("AnalysisUiText.ProjectRequestLabel", source, StringComparison.Ordinal);
                Assert.Contains("AnalysisUiText.SituationDescriptionLabel", source, StringComparison.Ordinal);
                Assert.Contains("AnalysisUiText.ChangeSourceLabel", source, StringComparison.Ordinal);
                Assert.DoesNotContain("Исходное описание", source, StringComparison.Ordinal);
                Assert.DoesNotContain("Проектный запрос", source, StringComparison.Ordinal);
                Assert.DoesNotContain("Описание ситуации", source, StringComparison.Ordinal);
            });

        var formInputSource = ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/AnalysisFormInput.cs");
        Assert.Contains("AnalysisUiText.OriginalDescriptionRequiredMessage", formInputSource, StringComparison.Ordinal);
        Assert.Contains("AnalysisUiText.ProjectRequestRequiredMessage", formInputSource, StringComparison.Ordinal);
        Assert.Contains("AnalysisUiText.SituationDescriptionRequiredMessage", formInputSource, StringComparison.Ordinal);
        Assert.Contains("AnalysisUiText.ChangeSourceRequiredMessage", formInputSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailsPage_SourceRendersSemanticSummaryForExistingSectionsAndActions()
    {
        var detailsSource = ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml");
        var detailsModelSource = ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml.cs");
        var combinedSource = detailsSource + detailsModelSource;

        Assert.Contains("Model.Analysis.StatusSummaryItems", detailsSource, StringComparison.Ordinal);
        Assert.Contains("href=\"#@summaryItem.Anchor\"", detailsSource, StringComparison.Ordinal);
        Assert.Contains("id=\"analysis-input\"", detailsSource, StringComparison.Ordinal);
        Assert.Contains("id=\"manual-context\"", detailsSource, StringComparison.Ordinal);
        Assert.Contains("id=\"preliminary-result\"", detailsSource, StringComparison.Ordinal);
        Assert.Contains("id=\"grounds-limitations\"", detailsSource, StringComparison.Ordinal);
        Assert.Contains("id=\"expert-evaluation\"", detailsSource, StringComparison.Ordinal);
        Assert.Contains("id=\"expert-conclusion\"", detailsSource, StringComparison.Ordinal);
        Assert.Contains("id=\"export\"", detailsSource, StringComparison.Ordinal);

        Assert.Contains("new(\"Проверить ввод\", \"/Analyses/Review\", null)", detailsModelSource, StringComparison.Ordinal);
        Assert.Contains("new(\"Открыть оценку\", \"/Analyses/ExpertEvaluation\", null)", detailsModelSource, StringComparison.Ordinal);
        Assert.Contains("new(\"Открыть заключение\", \"/Analyses/ExpertConclusion\", null)", detailsModelSource, StringComparison.Ordinal);
        Assert.Contains("new(\"Скачать JSON\", null, \"ExportJson\")", detailsModelSource, StringComparison.Ordinal);
        Assert.Contains("new(\"Скачать Markdown\", null, \"ExportMarkdown\")", detailsModelSource, StringComparison.Ordinal);

        Assert.All(
            new[]
            {
                "Dashboard",
                "Taskboard",
                "Approval",
                "OnPostApprove",
                "OnPostStartWorkflow",
                "OnPostRunAnalysis"
            },
            token => Assert.DoesNotContain(token, combinedSource, StringComparison.OrdinalIgnoreCase));
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
                    "Предварительный AI-анализ завершен."));
                var pageModel = new ReviewModel(dbContext, service);

                var result = await pageModel.OnPostRunAnalysisAsync(analysis.Id);
                var redirect = Assert.IsType<RedirectToPageResult>(result);

                Assert.Equal(analysis.Id, service.LastAnalysisId);
                Assert.Equal(AnalysisMode.DirectLlm, service.LastAnalysisMode);
                Assert.Equal(1, service.CallCount);
                Assert.Equal("/Analyses/Details", redirect.PageName);
                Assert.Equal(analysis.Id, redirect.RouteValues?["id"]);
                Assert.Equal("Предварительный AI-анализ завершен.", pageModel.AnalysisRunMessage);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReviewPage_RunAnalysisHandlerPassesExternalRagModeToApplicationService()
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
                    "Analysis completed."));
                var pageModel = new ReviewModel(dbContext, service)
                {
                    Input = new ReviewModel.RunAnalysisInput
                    {
                        AnalysisMode = nameof(AnalysisMode.ExternalRag)
                    }
                };

                var result = await pageModel.OnPostRunAnalysisAsync(analysis.Id);

                Assert.IsType<RedirectToPageResult>(result);
                Assert.Equal(analysis.Id, service.LastAnalysisId);
                Assert.Equal(AnalysisMode.ExternalRag, service.LastAnalysisMode);
                Assert.Equal(1, service.CallCount);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("Unknown")]
    public async Task ReviewPage_RunAnalysisHandlerRejectsInvalidAnalysisModeBeforeCallingService(
        string analysisMode)
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
                    "Analysis completed."));
                var pageModel = new ReviewModel(dbContext, service)
                {
                    Input = new ReviewModel.RunAnalysisInput
                    {
                        AnalysisMode = analysisMode
                    }
                };

                var result = await pageModel.OnPostRunAnalysisAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.Equal(0, service.CallCount);
                Assert.NotNull(pageModel.Analysis);
                Assert.False(pageModel.ModelState.IsValid);
                Assert.Contains(
                    pageModel.ModelState["Input.AnalysisMode"]!.Errors,
                    error => error.ErrorMessage.Contains("Analysis mode", StringComparison.Ordinal));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReviewPage_RunAnalysisHandlerReturnsNotFoundWhenServiceReportsNotFound()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var missingAnalysisId = Guid.NewGuid();
                var service = new CapturingAnalysisExecutionService(new AnalysisExecutionOutcome(
                    AnalysisExecutionOutcomeKind.NotFound,
                    missingAnalysisId,
                    ResultStatus: null,
                    Message: "Analysis not found."));
                var pageModel = new ReviewModel(dbContext, service);

                var result = await pageModel.OnPostRunAnalysisAsync(missingAnalysisId);

                Assert.IsType<NotFoundResult>(result);
                Assert.Equal(missingAnalysisId, service.LastAnalysisId);
                Assert.Equal(AnalysisMode.DirectLlm, service.LastAnalysisMode);
                Assert.Equal(1, service.CallCount);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ReviewPage_RunAnalysisHandlerSurfacesSnapshotLockedMessageOnDetailsRedirect()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
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
                    AnalysisExecutionOutcomeKind.SnapshotLocked,
                    analysis.Id,
                    AiAnalysisResultStatus.Completed,
                    "Повторный запуск AI-анализа заблокирован, потому что сохранена экспертная оценка. Сохраненный результат AI-анализа не изменен."));
                var pageModel = new ReviewModel(dbContext, service);

                var result = await pageModel.OnPostRunAnalysisAsync(analysis.Id);
                var redirect = Assert.IsType<RedirectToPageResult>(result);

                Assert.Equal(analysis.Id, service.LastAnalysisId);
                Assert.Equal(1, service.CallCount);
                Assert.Equal("/Analyses/Details", redirect.PageName);
                Assert.Equal(analysis.Id, redirect.RouteValues?["id"]);
                Assert.Contains("заблокирован", pageModel.AnalysisRunMessage, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("не изменен", pageModel.AnalysisRunMessage, StringComparison.OrdinalIgnoreCase);
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
                    Message: "Минимальные поля анализа заполнены не полностью."));
                var pageModel = new ReviewModel(dbContext, service);

                var result = await pageModel.OnPostRunAnalysisAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.Equal(analysis.Id, service.LastAnalysisId);
                Assert.Equal(1, service.CallCount);
                Assert.NotNull(pageModel.Analysis);
                Assert.False(pageModel.ModelState.IsValid);
                Assert.Contains(
                    pageModel.ModelState[string.Empty]!.Errors,
                    error => error.ErrorMessage.Contains("Минимальные поля анализа", StringComparison.Ordinal));
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

    [Fact]
    public void DetailsPage_SourceDoesNotContainContextFileUploadUiOrHandler()
    {
        var detailsSource = ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml");
        var detailsModelSource = ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml.cs");

        Assert.DoesNotContain("asp-page-handler=\"UploadContextFragment\"", detailsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("enctype=\"multipart/form-data\"", detailsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"file\"", detailsSource, StringComparison.Ordinal);
        Assert.DoesNotContain("UploadContextFragmentInput", detailsSource, StringComparison.Ordinal);

        Assert.DoesNotContain("OnPostUploadContextFragmentAsync", detailsModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("UploadContextFragmentInput", detailsModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FileContextFragmentInput", detailsModelSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IFormFile", detailsModelSource, StringComparison.Ordinal);

        Assert.Contains("asp-page-handler=\"AddContextFragment\"", detailsSource, StringComparison.Ordinal);
        Assert.Contains("ContextFragmentInput.Text", detailsSource, StringComparison.Ordinal);
        Assert.Contains("string? FileName", detailsModelSource, StringComparison.Ordinal);
        Assert.Contains("string? FilePath", detailsModelSource, StringComparison.Ordinal);
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
    public async Task DetailsPage_DirectLlmResultMetadataShowsDirectLlmWithoutRetrievedContextBasis()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            var aiResult = CreateAiAnalysisResult(analysis.Id, AiAnalysisResultStatus.Completed, CreateImpactMap());
            aiResult.Metadata = AiAnalysisResultMetadata.CreateDefaultDirectLlm(
                engineName: "direct-llm-engine",
                providerName: "demo-provider",
                modelWorkflowProfileName: "demo-profile");
            analysis.AiAnalysisResult = aiResult;

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
                var metadata = pageModel.Analysis.AiAnalysisResult!.Metadata;
                Assert.Equal(AnalysisMode.DirectLlm, metadata.AnalysisMode);
                Assert.Equal("Direct LLM", AnalysisUiText.AnalysisModeLabel(metadata.AnalysisMode));
                Assert.Equal("direct-llm-engine", metadata.EngineName);
                Assert.Equal("demo-provider", metadata.ProviderName);
                Assert.Equal("demo-profile", metadata.ModelWorkflowProfileName);
                Assert.Equal("{ \"raw\": \"response\" }", pageModel.Analysis.AiAnalysisResult.RawResponse);
                Assert.Equal(RetrievedContextState.Unavailable, metadata.RetrievedContextState);
                Assert.Equal(
                    "Контекст не сохранен",
                    AnalysisUiText.RetrievedContextStateLabel(metadata.RetrievedContextState));
                Assert.False(metadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Empty(metadata.Warnings);
                Assert.Empty(metadata.RetrievedContextItems);
            }

            var detailsSource = ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml");
            Assert.DoesNotContain("основан на извлеченном контексте", detailsSource, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task DetailsPage_ExternalResultMetadataShowsSavedExternalSummaryAndWarnings()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            var aiResult = CreateAiAnalysisResult(
                analysis.Id,
                AiAnalysisResultStatus.CompletedWithWarnings,
                CreateImpactMap());
            aiResult.Metadata = new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = "external-analysis-engine",
                ProviderName = "neutral-provider",
                AdapterName = "neutral-adapter",
                ModelWorkflowProfileName = "impact-profile",
                RetrievedContextState = RetrievedContextState.Partial,
                ManualContextForwardedToExternalAiOrRag = true,
                Warnings =
                [
                    "Retrieved context was partial.",
                    "Manual context forwarding was limited."
                ]
            };
            analysis.AiAnalysisResult = aiResult;

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
                var metadata = pageModel.Analysis.AiAnalysisResult!.Metadata;
                Assert.Equal(AnalysisMode.ExternalRag, metadata.AnalysisMode);
                Assert.Equal("External AI/RAG", AnalysisUiText.AnalysisModeLabel(metadata.AnalysisMode));
                Assert.Equal("external-analysis-engine", metadata.EngineName);
                Assert.Equal("neutral-provider", metadata.ProviderName);
                Assert.Equal("neutral-adapter", metadata.AdapterName);
                Assert.Equal("impact-profile", metadata.ModelWorkflowProfileName);
                Assert.Equal(RetrievedContextState.Partial, metadata.RetrievedContextState);
                Assert.Equal(
                    "Контекст сохранен частично",
                    AnalysisUiText.RetrievedContextStateLabel(metadata.RetrievedContextState));
                Assert.True(metadata.ManualContextForwardedToExternalAiOrRag);
                Assert.Equal(
                    "Передавался во внешний контур",
                    AnalysisUiText.ManualContextForwardingLabel(metadata.ManualContextForwardedToExternalAiOrRag));
                Assert.Collection(
                    metadata.Warnings,
                    warning => Assert.Equal("Retrieved context was partial.", warning),
                    warning => Assert.Equal("Manual context forwarding was limited.", warning));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task DetailsPage_AvailableRetrievedContextShowsSavedTextAndNeutralMetadata()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            analysis.ContextFragments.Add(CreateContextFragment(
                analysis.Id,
                ContextFragmentType.Task,
                "Manual task context",
                "Manual context must stay separate.",
                new DateTimeOffset(2026, 06, 13, 08, 30, 00, TimeSpan.Zero)));

            var aiResult = CreateAiAnalysisResult(analysis.Id, AiAnalysisResultStatus.Completed, CreateImpactMap());
            aiResult.Metadata = new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = "external-analysis-engine",
                ProviderName = "result-level-provider",
                AdapterName = "result-level-adapter",
                RetrievedContextState = RetrievedContextState.Available,
                RetrievedContextItems =
                [
                    new RetrievedContextItem
                    {
                        SourceTitle = "Gateway API requirement",
                        SourceId = "REQ-42",
                        ExternalReference = "kb-doc-42",
                        FragmentId = "fragment-7",
                        Text = "Saved full retrieved context text.",
                        UrlOrReference = "kb://requirements/REQ-42",
                        Rank = 1,
                        Score = 0.92,
                        ProviderName = "item-provider-should-not-be-projected",
                        AdapterName = "item-adapter-should-not-be-projected",
                        Completeness = RetrievedContextItemCompleteness.FullText
                    }
                ]
            };
            analysis.AiAnalysisResult = aiResult;

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
                var metadata = pageModel.Analysis.AiAnalysisResult!.Metadata;
                Assert.Equal(RetrievedContextState.Available, metadata.RetrievedContextState);
                var item = Assert.Single(metadata.RetrievedContextItems);
                Assert.Equal("Gateway API requirement", item.SourceTitle);
                Assert.Equal("REQ-42", item.SourceId);
                Assert.Equal("kb-doc-42", item.ExternalReference);
                Assert.Equal("fragment-7", item.FragmentId);
                Assert.Equal("Saved full retrieved context text.", item.Text);
                Assert.Null(item.Excerpt);
                Assert.Equal("kb://requirements/REQ-42", item.UrlOrReference);
                Assert.Equal(1, item.Rank);
                Assert.Equal(0.92, item.Score);
                Assert.Equal(RetrievedContextItemCompleteness.FullText, item.Completeness);
                Assert.Null(item.WarningOrLimitationNote);
                Assert.DoesNotContain(
                    nameof(RetrievedContextItem.ProviderName),
                    typeof(DetailsModel.RetrievedContextItemDetails).GetProperties().Select(property => property.Name));
                Assert.DoesNotContain(
                    nameof(RetrievedContextItem.AdapterName),
                    typeof(DetailsModel.RetrievedContextItemDetails).GetProperties().Select(property => property.Name));
                Assert.Single(pageModel.Analysis.ContextFragments);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task DetailsPage_MetadataOnlyRetrievedContextDoesNotPretendFullTextExists()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            var aiResult = CreateAiAnalysisResult(analysis.Id, AiAnalysisResultStatus.Completed, CreateImpactMap());
            aiResult.Metadata = new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = "external-analysis-engine",
                RetrievedContextState = RetrievedContextState.MetadataOnly,
                RetrievedContextItems =
                [
                    new RetrievedContextItem
                    {
                        SourceTitle = "Integration inventory",
                        ExternalReference = "inventory-record-12",
                        UrlOrReference = "kb://inventory/12",
                        Rank = 2,
                        Score = 0.81,
                        Completeness = RetrievedContextItemCompleteness.MetadataOnly,
                        WarningOrLimitationNote = "External circuit returned source metadata without fragment text."
                    }
                ]
            };
            analysis.AiAnalysisResult = aiResult;

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
                var metadata = pageModel.Analysis.AiAnalysisResult!.Metadata;
                Assert.Equal(RetrievedContextState.MetadataOnly, metadata.RetrievedContextState);
                Assert.Equal(
                    "Сохранены только сведения об источниках; полный текст и выдержки не сохранены.",
                    AnalysisUiText.RetrievedContextStateDescription(metadata.RetrievedContextState));
                var item = Assert.Single(metadata.RetrievedContextItems);
                Assert.Equal(RetrievedContextItemCompleteness.MetadataOnly, item.Completeness);
                Assert.Null(item.Text);
                Assert.Null(item.Excerpt);
                Assert.Equal("Integration inventory", item.SourceTitle);
                Assert.Equal("inventory-record-12", item.ExternalReference);
                Assert.Equal("External circuit returned source metadata without fragment text.", item.WarningOrLimitationNote);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task DetailsPage_PartialRetrievedContextShowsLimitationAndSavedTextExcerptMix()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            var aiResult = CreateAiAnalysisResult(analysis.Id, AiAnalysisResultStatus.CompletedWithWarnings, CreateImpactMap());
            aiResult.Metadata = new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = "external-analysis-engine",
                RetrievedContextState = RetrievedContextState.Partial,
                RetrievedContextItems =
                [
                    new RetrievedContextItem
                    {
                        SourceTitle = "Partial source",
                        Text = "Saved full text.",
                        Excerpt = "Saved excerpt.",
                        Rank = 1,
                        Completeness = RetrievedContextItemCompleteness.FullText,
                        WarningOrLimitationNote = "Only part of retrieved basis was available for saved display."
                    }
                ],
                Warnings = ["Retrieved context was partial."]
            };
            analysis.AiAnalysisResult = aiResult;

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
                var metadata = pageModel.Analysis.AiAnalysisResult!.Metadata;
                Assert.Equal(RetrievedContextState.Partial, metadata.RetrievedContextState);
                Assert.Contains("ограничения", AnalysisUiText.RetrievedContextStateDescription(metadata.RetrievedContextState));
                var item = Assert.Single(metadata.RetrievedContextItems);
                Assert.Equal("Partial source", item.SourceTitle);
                Assert.Equal("Saved full text.", item.Text);
                Assert.Equal("Saved excerpt.", item.Excerpt);
                Assert.Equal(RetrievedContextItemCompleteness.FullText, item.Completeness);
                Assert.Equal("Only part of retrieved basis was available for saved display.", item.WarningOrLimitationNote);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task DetailsPage_UnavailableRetrievedContextShowsReproducibilityLimitation()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            var aiResult = CreateAiAnalysisResult(analysis.Id, AiAnalysisResultStatus.CompletedWithWarnings, CreateImpactMap());
            aiResult.Metadata = new AiAnalysisResultMetadata
            {
                AnalysisMode = AnalysisMode.ExternalRag,
                EngineName = "external-analysis-engine",
                RetrievedContextState = RetrievedContextState.Unavailable,
                Warnings = ["Retrieved context was unavailable."]
            };
            analysis.AiAnalysisResult = aiResult;

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
                var metadata = pageModel.Analysis.AiAnalysisResult!.Metadata;
                Assert.Equal(RetrievedContextState.Unavailable, metadata.RetrievedContextState);
                Assert.Empty(metadata.RetrievedContextItems);
                Assert.Contains(
                    "воспроизводимость",
                    AnalysisUiText.RetrievedContextStateDescription(metadata.RetrievedContextState));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public void DetailsPage_SourceDoesNotContainProviderSpecificUiPayloadFields()
    {
        var detailsSource = ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml");
        var detailsModelSource = ReadProjectFile("src/RequirementImpactAssistant.Web/Pages/Analyses/Details.cshtml.cs");
        var combinedSource = detailsSource + detailsModelSource;

        var forbiddenTokens = new[]
        {
            "Dify",
            "DifyExternal",
            "workflow_run_id",
            "workflow_id",
            "task_id",
            "manual_context",
            "providerStatus",
            "responseShape",
            "SanitizedDiagnosticSnapshot",
            "SanitizedProperties"
        };

        Assert.All(
            forbiddenTokens,
            token => Assert.DoesNotContain(token, combinedSource, StringComparison.OrdinalIgnoreCase));

        Assert.Contains("@aiResult.RawResponse", detailsSource, StringComparison.Ordinal);
        Assert.Contains("string RawResponse", detailsModelSource, StringComparison.Ordinal);
        Assert.Contains("RetrievedContextItems", combinedSource, StringComparison.Ordinal);
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
                Assert.Equal(ProjectRequestType.ApiOrIntegrationChange, analysis.ProjectRequestType);
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
    public async Task CreatePage_SavesExplicitOtherProjectRequestType()
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
                var input = CreateInput("Other request");
                input.ProjectRequestType = ProjectRequestType.Other;
                var pageModel = new CreateModel(dbContext)
                {
                    Input = input
                };

                var result = await pageModel.OnPostAsync();
                var redirect = Assert.IsType<RedirectToPageResult>(result);
                analysisId = Assert.IsType<Guid>(redirect.RouteValues?["id"]);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var analysis = await dbContext.Analyses.SingleAsync(candidate => candidate.Id == analysisId);

                Assert.Equal(ProjectRequestType.Other, analysis.ProjectRequestType);
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
                Assert.Equal(6, pageModel.ModelState.ErrorCount);
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
                Assert.Equal(ProjectRequestType.ApiOrIntegrationChange, updated.ProjectRequestType);
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
                Assert.Equal(6, pageModel.ModelState.ErrorCount);
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
    public async Task ExpertEvaluationPage_OpensOnlyForCompletedAiResultWithImpactMap()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            analysis.AiAnalysisResult = CreateAiAnalysisResult(
                analysis.Id,
                AiAnalysisResultStatus.Completed,
                CreateImpactMap());

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertEvaluationModel(dbContext);

                var result = await pageModel.OnGetAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.NotNull(pageModel.Analysis);
                Assert.Equal(analysis.Id, pageModel.Analysis.Id);
                Assert.Contains(
                    pageModel.Analysis.ImpactSections.SelectMany(section => section.Items),
                    item => item.Id == "affected-requirement-001");
                Assert.Contains(
                    pageModel.Input.EvaluatedItems,
                    item => item.TargetId == "affected-requirement-001");
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Theory]
    [InlineData(AiAnalysisResultStatus.Failed, true)]
    [InlineData(AiAnalysisResultStatus.InvalidResponse, true)]
    [InlineData(AiAnalysisResultStatus.Completed, false)]
    public async Task ExpertEvaluationPage_DoesNotCreateEvaluationForFailedInvalidOrMissingImpactMap(
        AiAnalysisResultStatus resultStatus,
        bool hasImpactMap)
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.LlmAnalysisFailed,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            analysis.AiAnalysisResult = CreateAiAnalysisResult(
                analysis.Id,
                resultStatus,
                hasImpactMap ? CreateImpactMap() : null);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertEvaluationModel(dbContext)
                {
                    Input = CreateExpertEvaluationInput(CreateImpactMap())
                };

                var result = await pageModel.OnPostAsync(analysis.Id);

                Assert.IsType<NotFoundResult>(result);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.ExpertEvaluation)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Null(unchanged.ExpertEvaluation);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExpertEvaluationPage_ReturnsValidationErrorForDuplicateEvaluatedItemTargets()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero);
            var impactMap = CreateImpactMap();
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.NeedsExpertEvaluation, originalUpdatedAt);
            analysis.AiAnalysisResult = CreateAiAnalysisResult(analysis.Id, AiAnalysisResultStatus.Completed, impactMap);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var input = CreateExpertEvaluationInput(impactMap);
                input.EvaluatedItems.Add(new ExpertEvaluationModel.EvaluatedImpactItemInput
                {
                    TargetId = "risk-001",
                    Mark = ExpertMark.Confirmed,
                    Comment = "Duplicate malformed post."
                });

                var pageModel = new ExpertEvaluationModel(dbContext)
                {
                    Input = input
                };

                var result = await pageModel.OnPostAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.False(pageModel.ModelState.IsValid);
                Assert.Contains(
                    pageModel.ModelState.Values.SelectMany(value => value.Errors),
                    error => error.ErrorMessage == "Целевой элемент карты влияния должен быть уникальным.");
                Assert.NotNull(pageModel.Analysis);
                Assert.Equal(
                    EnumerateImpactItems(impactMap).Count(),
                    pageModel.Input.EvaluatedItems.Count);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.ExpertEvaluation)
                    .Include(candidate => candidate.ExpertConclusion)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, unchanged.Status);
                Assert.Equal(originalUpdatedAt, unchanged.UpdatedAt);
                Assert.Null(unchanged.ExpertEvaluation);
                Assert.Null(unchanged.ExpertConclusion);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExpertEvaluationPage_SavesMarksMissedItemsCorrectionsRatingsAndGeneralComment()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero);
            var impactMap = CreateImpactMap();
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.NeedsExpertEvaluation, originalUpdatedAt);
            analysis.AiAnalysisResult = CreateAiAnalysisResult(analysis.Id, AiAnalysisResultStatus.Completed, impactMap);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertEvaluationModel(dbContext)
                {
                    Input = CreateExpertEvaluationInput(impactMap)
                };

                var result = await pageModel.OnPostAsync(analysis.Id);
                var redirect = Assert.IsType<RedirectToPageResult>(result);

                Assert.Equal("/Analyses/ExpertEvaluation", redirect.PageName);
                Assert.Equal(analysis.Id, redirect.RouteValues?["id"]);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updated = await dbContext.Analyses
                    .Include(candidate => candidate.ExpertEvaluation)
                        .ThenInclude(candidate => candidate!.EvaluatedItems)
                    .Include(candidate => candidate.ExpertEvaluation)
                        .ThenInclude(candidate => candidate!.MissedItems)
                    .Include(candidate => candidate.ExpertEvaluation)
                        .ThenInclude(candidate => candidate!.Corrections)
                    .Include(candidate => candidate.ExpertConclusion)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, updated.Status);
                Assert.True(updated.UpdatedAt > originalUpdatedAt);
                Assert.Null(updated.ExpertConclusion);

                var evaluation = Assert.IsType<ExpertEvaluation>(updated.ExpertEvaluation);
                Assert.Equal(ContextSufficiencyRating.PartiallySufficient, evaluation.ContextSufficiency);
                Assert.Equal(ResultUsefulnessRating.Useful, evaluation.ResultUsefulness);
                Assert.Equal("Useful enough for expert review.", evaluation.GeneralComment);

                Assert.Contains(
                    evaluation.EvaluatedItems,
                    item =>
                        item.TargetId == "affected-requirement-001" &&
                        item.Mark == ExpertMark.Corrected &&
                        item.Comment == "Requirement impact needs wording." &&
                        item.CorrectionText == "Clarify affected acceptance criteria.");
                Assert.Contains(
                    evaluation.EvaluatedItems,
                    item =>
                        item.TargetId == "risk-001" &&
                        item.Mark == ExpertMark.Rejected &&
                        item.Comment == "Risk is overstated." &&
                        item.CorrectionText == string.Empty);

                var missedItem = Assert.Single(evaluation.MissedItems);
                Assert.Equal(ImpactMapItemType.AffectedTask, missedItem.ItemType);
                Assert.Equal("Regression task", missedItem.Title);
                Assert.Equal("Add a regression test task.", missedItem.Description);
                Assert.Equal(ImpactSeverity.Medium, missedItem.Severity);
                Assert.Equal("Missed by preliminary analysis.", missedItem.Comment);

                var correction = Assert.Single(evaluation.Corrections);
                Assert.Equal("risk-001", correction.TargetId);
                Assert.Equal(ExpertEvaluationTargetType.ImpactItem, correction.TargetType);
                Assert.Equal(ImpactMapItemType.Risk, correction.ItemType);
                Assert.Equal("Risk should mention rollout sequencing.", correction.Text);
                Assert.Equal("Additional expert correction.", correction.Comment);

                Assert.Equal(1, await dbContext.ExpertEvaluations.CountAsync(candidate => candidate.AnalysisId == analysis.Id));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExpertEvaluationPage_RepeatedSaveUpdatesSingleEvaluationWithoutCallingAiServices()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var impactMap = CreateImpactMap();
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            analysis.AiAnalysisResult = CreateAiAnalysisResult(
                analysis.Id,
                AiAnalysisResultStatus.CompletedWithWarnings,
                impactMap);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertEvaluationModel(dbContext)
                {
                    Input = CreateExpertEvaluationInput(impactMap)
                };

                Assert.IsType<RedirectToPageResult>(await pageModel.OnPostAsync(analysis.Id));
            }

            Guid evaluationId;
            await using (var dbContext = new ApplicationDbContext(options))
            {
                evaluationId = await dbContext.ExpertEvaluations
                    .Where(candidate => candidate.AnalysisId == analysis.Id)
                    .Select(candidate => candidate.Id)
                    .SingleAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updatedInput = CreateExpertEvaluationInput(impactMap);
                updatedInput.ContextSufficiency = ContextSufficiencyRating.Sufficient;
                updatedInput.ResultUsefulness = ResultUsefulnessRating.PartiallyUseful;
                updatedInput.GeneralComment = "Updated expert evaluation.";
                updatedInput.MissedItems.Clear();
                updatedInput.Corrections.Clear();
                updatedInput.EvaluatedItems.Single(item => item.TargetId == "risk-001").Mark = ExpertMark.NeedsClarification;
                updatedInput.EvaluatedItems.Single(item => item.TargetId == "risk-001").Comment = "Clarify probability.";

                var pageModel = new ExpertEvaluationModel(dbContext)
                {
                    Input = updatedInput
                };

                Assert.IsType<RedirectToPageResult>(await pageModel.OnPostAsync(analysis.Id));
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var evaluation = await dbContext.ExpertEvaluations
                    .Include(candidate => candidate.EvaluatedItems)
                    .Include(candidate => candidate.MissedItems)
                    .Include(candidate => candidate.Corrections)
                    .SingleAsync(candidate => candidate.AnalysisId == analysis.Id);

                Assert.Equal(evaluationId, evaluation.Id);
                Assert.Equal(ContextSufficiencyRating.Sufficient, evaluation.ContextSufficiency);
                Assert.Equal(ResultUsefulnessRating.PartiallyUseful, evaluation.ResultUsefulness);
                Assert.Equal("Updated expert evaluation.", evaluation.GeneralComment);
                Assert.Empty(evaluation.MissedItems);
                Assert.Empty(evaluation.Corrections);
                Assert.Equal(1, await dbContext.ExpertEvaluations.CountAsync(candidate => candidate.AnalysisId == analysis.Id));
                Assert.Contains(
                    evaluation.EvaluatedItems,
                    item =>
                        item.TargetId == "risk-001" &&
                        item.Mark == ExpertMark.NeedsClarification &&
                        item.Comment == "Clarify probability.");
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExpertConclusionPage_OpensOnlyForAnalysisWithExpertEvaluation()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            analysis.ExpertEvaluation = CreateExpertEvaluation(analysis.Id);
            var withoutEvaluation = CreateAnalysis(
                "Billing migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 09, 00, 00, TimeSpan.Zero));

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.AddRange(analysis, withoutEvaluation);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertConclusionModel(dbContext);

                var result = await pageModel.OnGetAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.NotNull(pageModel.Analysis);
                Assert.Equal(analysis.Id, pageModel.Analysis.Id);
                Assert.Equal(ExpertConclusionType.NotSet, pageModel.Input.ConclusionType);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertConclusionModel(dbContext);

                var result = await pageModel.OnGetAsync(withoutEvaluation.Id);

                Assert.IsType<NotFoundResult>(result);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExpertConclusionPage_DoesNotCreateConclusionOrChangeStatusWithoutExpertEvaluation()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.NeedsExpertEvaluation, originalUpdatedAt);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertConclusionModel(dbContext)
                {
                    Input = new ExpertConclusionModel.ExpertConclusionInput
                    {
                        ConclusionType = ExpertConclusionType.Accept,
                        Comment = "Accepted by expert.",
                        Rationale = "The saved evaluation supports acceptance."
                    }
                };

                var result = await pageModel.OnPostAsync(analysis.Id);

                Assert.IsType<NotFoundResult>(result);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.ExpertConclusion)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, unchanged.Status);
                Assert.Equal(originalUpdatedAt, unchanged.UpdatedAt);
                Assert.Null(unchanged.FixedAt);
                Assert.Null(unchanged.ExpertConclusion);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExpertConclusionPage_SavesConclusionFieldsFixedAtAndStatusWithoutCallingAiServices()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.NeedsExpertEvaluation, originalUpdatedAt);
            analysis.ExpertEvaluation = CreateExpertEvaluation(analysis.Id);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            var beforeSave = DateTimeOffset.UtcNow;
            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertConclusionModel(dbContext)
                {
                    Input = new ExpertConclusionModel.ExpertConclusionInput
                    {
                        ConclusionType = ExpertConclusionType.AcceptWithLimitations,
                        Comment = " Accept with rollout limits. ",
                        Rationale = " Expert evaluation confirms core impact map with limitations. "
                    }
                };

                var result = await pageModel.OnPostAsync(analysis.Id);
                var redirect = Assert.IsType<RedirectToPageResult>(result);

                Assert.Equal("/Analyses/ExpertConclusion", redirect.PageName);
                Assert.Equal(analysis.Id, redirect.RouteValues?["id"]);
            }

            var afterSave = DateTimeOffset.UtcNow;
            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updated = await dbContext.Analyses
                    .Include(candidate => candidate.ExpertConclusion)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(AnalysisStatus.ExpertConclusionFixed, updated.Status);
                Assert.True(updated.UpdatedAt >= beforeSave);
                Assert.True(updated.UpdatedAt <= afterSave);
                Assert.NotNull(updated.FixedAt);
                Assert.True(updated.FixedAt >= beforeSave);
                Assert.True(updated.FixedAt <= afterSave);

                var conclusion = Assert.IsType<ExpertConclusion>(updated.ExpertConclusion);
                Assert.Equal(ExpertConclusionType.AcceptWithLimitations, conclusion.ConclusionType);
                Assert.Equal("Accept with rollout limits.", conclusion.Comment);
                Assert.Equal("Expert evaluation confirms core impact map with limitations.", conclusion.Rationale);
                Assert.NotNull(conclusion.FixedAt);
                Assert.Equal(updated.FixedAt, conclusion.FixedAt);
                Assert.True(conclusion.FixedAt >= beforeSave);
                Assert.True(conclusion.FixedAt <= afterSave);
                Assert.Equal(1, await dbContext.ExpertConclusions.CountAsync(candidate => candidate.AnalysisId == analysis.Id));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExpertConclusionPage_ReturnsValidationErrorForMissingConclusionTypeAndRationale()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var originalUpdatedAt = new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero);
            var analysis = CreateAnalysis("Gateway migration", AnalysisStatus.NeedsExpertEvaluation, originalUpdatedAt);
            analysis.ExpertEvaluation = CreateExpertEvaluation(analysis.Id);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertConclusionModel(dbContext)
                {
                    Input = new ExpertConclusionModel.ExpertConclusionInput
                    {
                        ConclusionType = ExpertConclusionType.NotSet,
                        Comment = "Incomplete conclusion.",
                        Rationale = " "
                    }
                };

                var result = await pageModel.OnPostAsync(analysis.Id);

                Assert.IsType<PageResult>(result);
                Assert.False(pageModel.ModelState.IsValid);
                Assert.NotNull(pageModel.Analysis);
                Assert.Contains(
                    pageModel.ModelState.Values.SelectMany(value => value.Errors),
                    error => error.ErrorMessage == "Тип заключения обязателен.");
                Assert.Contains(
                    pageModel.ModelState.Values.SelectMany(value => value.Errors),
                    error => error.ErrorMessage == "Обоснование обязательно.");
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var unchanged = await dbContext.Analyses
                    .Include(candidate => candidate.ExpertConclusion)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(AnalysisStatus.NeedsExpertEvaluation, unchanged.Status);
                Assert.Equal(originalUpdatedAt, unchanged.UpdatedAt);
                Assert.Null(unchanged.FixedAt);
                Assert.Null(unchanged.ExpertConclusion);
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Fact]
    public async Task ExpertConclusionPage_RepeatedSaveUpdatesSingleConclusion()
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            analysis.ExpertEvaluation = CreateExpertEvaluation(analysis.Id);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertConclusionModel(dbContext)
                {
                    Input = new ExpertConclusionModel.ExpertConclusionInput
                    {
                        ConclusionType = ExpertConclusionType.Accept,
                        Comment = "Accepted.",
                        Rationale = "Evaluation is sufficient."
                    }
                };

                Assert.IsType<RedirectToPageResult>(await pageModel.OnPostAsync(analysis.Id));
            }

            Guid conclusionId;
            DateTimeOffset firstFixedAt;
            await using (var dbContext = new ApplicationDbContext(options))
            {
                var conclusion = await dbContext.ExpertConclusions.SingleAsync(candidate => candidate.AnalysisId == analysis.Id);
                conclusionId = conclusion.Id;
                firstFixedAt = Assert.IsType<DateTimeOffset>(conclusion.FixedAt);
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertConclusionModel(dbContext)
                {
                    Input = new ExpertConclusionModel.ExpertConclusionInput
                    {
                        ConclusionType = ExpertConclusionType.SendForClarification,
                        Comment = "Need clarification.",
                        Rationale = "Evaluation exposed unresolved product questions."
                    }
                };

                Assert.IsType<RedirectToPageResult>(await pageModel.OnPostAsync(analysis.Id));
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updated = await dbContext.Analyses
                    .Include(candidate => candidate.ExpertConclusion)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                var conclusion = Assert.IsType<ExpertConclusion>(updated.ExpertConclusion);
                Assert.Equal(conclusionId, conclusion.Id);
                Assert.Equal(ExpertConclusionType.SendForClarification, conclusion.ConclusionType);
                Assert.Equal("Need clarification.", conclusion.Comment);
                Assert.Equal("Evaluation exposed unresolved product questions.", conclusion.Rationale);
                Assert.True(conclusion.FixedAt >= firstFixedAt);
                Assert.Equal(AnalysisStatus.ExpertConclusionFixed, updated.Status);
                Assert.Equal(1, await dbContext.ExpertConclusions.CountAsync(candidate => candidate.AnalysisId == analysis.Id));
            }
        }
        finally
        {
            DeleteDatabase(databasePath);
        }
    }

    [Theory]
    [InlineData(ExpertConclusionType.SplitIntoSeveralTasks)]
    [InlineData(ExpertConclusionType.ReturnForReanalysis)]
    public async Task ExpertConclusionPage_PassiveWorkflowConclusionTypesAreOnlySavedAsEnumValues(
        ExpertConclusionType conclusionType)
    {
        var databasePath = CreateDatabasePath();

        try
        {
            var options = CreateOptions(databasePath);
            var analysis = CreateAnalysis(
                "Gateway migration",
                AnalysisStatus.NeedsExpertEvaluation,
                new DateTimeOffset(2026, 06, 13, 08, 00, 00, TimeSpan.Zero));
            analysis.ExpertEvaluation = CreateExpertEvaluation(analysis.Id);

            await using (var dbContext = new ApplicationDbContext(options))
            {
                await dbContext.Database.MigrateAsync();
                dbContext.Analyses.Add(analysis);
                await dbContext.SaveChangesAsync();
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var pageModel = new ExpertConclusionModel(dbContext)
                {
                    Input = new ExpertConclusionModel.ExpertConclusionInput
                    {
                        ConclusionType = conclusionType,
                        Comment = "Passive expert conclusion.",
                        Rationale = "This records the expert outcome only."
                    }
                };

                Assert.IsType<RedirectToPageResult>(await pageModel.OnPostAsync(analysis.Id));
            }

            await using (var dbContext = new ApplicationDbContext(options))
            {
                var updated = await dbContext.Analyses
                    .Include(candidate => candidate.ExpertEvaluation)
                    .Include(candidate => candidate.ExpertConclusion)
                    .SingleAsync(candidate => candidate.Id == analysis.Id);

                Assert.Equal(AnalysisStatus.ExpertConclusionFixed, updated.Status);
                Assert.NotNull(updated.ExpertEvaluation);
                var conclusion = Assert.IsType<ExpertConclusion>(updated.ExpertConclusion);
                Assert.Equal(conclusionType, conclusion.ConclusionType);
                Assert.Equal(1, await dbContext.ExpertEvaluations.CountAsync(candidate => candidate.AnalysisId == analysis.Id));
                Assert.Equal(1, await dbContext.ExpertConclusions.CountAsync(candidate => candidate.AnalysisId == analysis.Id));
                Assert.Empty(await dbContext.ContextFragments.Where(candidate => candidate.AnalysisId == analysis.Id).ToListAsync());
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

    private static string ReadProjectFile(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "RequirementImpactAssistant.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

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
            ProjectRequestType = ProjectRequestType.NewFunctionality,
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
            ProjectRequestType = ProjectRequestType.ApiOrIntegrationChange,
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

    private static AiAnalysisResult CreateAiAnalysisResult(
        Guid analysisId,
        AiAnalysisResultStatus status,
        ImpactMap? impactMap) =>
        new()
        {
            AnalysisId = analysisId,
            Status = status,
            GeneratedAt = new DateTimeOffset(2026, 06, 13, 09, 00, 00, TimeSpan.Zero),
            EngineName = "test-engine",
            ProviderName = "test-provider",
            ModelName = "test-model",
            PromptVersion = "test-v1",
            InputSnapshot = "{ \"input\": \"snapshot\" }",
            RawResponse = "{ \"raw\": \"response\" }",
            ErrorMessage = status is AiAnalysisResultStatus.Failed or AiAnalysisResultStatus.InvalidResponse
                ? "Analysis did not produce a usable structured impact map."
                : string.Empty,
            ImpactMap = impactMap
        };

    private static ImpactMap CreateImpactMap()
    {
        var impactMap = new ImpactMap
        {
            ChangeSummary =
            {
                Title = "Gateway change summary",
                Description = "Gateway API behavior changes.",
                Severity = ImpactSeverity.Medium,
                Notes = "Preliminary summary."
            },
            PreliminaryAssessment =
            {
                Title = "Requires expert review",
                Description = "The change needs human validation.",
                Severity = ImpactSeverity.High,
                Notes = "Preliminary only."
            }
        };

        var affectedRequirement = impactMap.AddAffectedRequirement();
        affectedRequirement.Title = "Gateway requirement";
        affectedRequirement.Description = "Update gateway acceptance criteria.";
        affectedRequirement.Severity = ImpactSeverity.High;
        affectedRequirement.Notes = "Confirm with analyst.";

        var risk = impactMap.AddRisk();
        risk.Title = "Gateway compatibility risk";
        risk.Description = "Existing clients may depend on previous behavior.";
        risk.Severity = ImpactSeverity.Medium;
        risk.Notes = "Validate rollout.";

        return impactMap;
    }

    private static ExpertEvaluationModel.ExpertEvaluationInput CreateExpertEvaluationInput(ImpactMap impactMap)
    {
        var input = new ExpertEvaluationModel.ExpertEvaluationInput
        {
            ContextSufficiency = ContextSufficiencyRating.PartiallySufficient,
            ResultUsefulness = ResultUsefulnessRating.Useful,
            GeneralComment = "Useful enough for expert review."
        };

        foreach (var item in EnumerateImpactItems(impactMap))
        {
            input.EvaluatedItems.Add(new ExpertEvaluationModel.EvaluatedImpactItemInput
            {
                TargetId = item.Id,
                Mark = item.Id switch
                {
                    "affected-requirement-001" => ExpertMark.Corrected,
                    "risk-001" => ExpertMark.Rejected,
                    _ => ExpertMark.Confirmed
                },
                Comment = item.Id switch
                {
                    "affected-requirement-001" => "Requirement impact needs wording.",
                    "risk-001" => "Risk is overstated.",
                    _ => "Accepted."
                },
                CorrectionText = item.Id == "affected-requirement-001"
                    ? "Clarify affected acceptance criteria."
                    : string.Empty
            });
        }

        input.MissedItems.Add(new ExpertEvaluationModel.MissedItemInput
        {
            ItemType = ImpactMapItemType.AffectedTask,
            Title = "Regression task",
            Description = "Add a regression test task.",
            Severity = ImpactSeverity.Medium,
            Comment = "Missed by preliminary analysis."
        });

        input.Corrections.Add(new ExpertEvaluationModel.CorrectionInput
        {
            TargetId = "risk-001",
            ItemType = ImpactMapItemType.Risk,
            Text = "Risk should mention rollout sequencing.",
            Comment = "Additional expert correction."
        });

        return input;
    }

    private static ExpertEvaluation CreateExpertEvaluation(Guid analysisId) =>
        new()
        {
            AnalysisId = analysisId,
            ContextSufficiency = ContextSufficiencyRating.PartiallySufficient,
            ResultUsefulness = ResultUsefulnessRating.Useful,
            GeneralComment = "Saved expert evaluation."
        };

    private static IEnumerable<ImpactMapItem> EnumerateImpactItems(ImpactMap impactMap) =>
    [
        impactMap.ChangeSummary,
        ..impactMap.AffectedRequirements,
        ..impactMap.AffectedTasks,
        ..impactMap.AffectedProjectDecisions,
        ..impactMap.AffectedApiInterfacesDocumentsTests,
        ..impactMap.AffectedArchitecturalConstraints,
        ..impactMap.AffectedOrganizationalContextItems,
        ..impactMap.Contradictions,
        ..impactMap.MissingInformation,
        ..impactMap.ClarificationQuestions,
        ..impactMap.Risks,
        ..impactMap.OptionsForExpertReview,
        impactMap.PreliminaryAssessment
    ];

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

        public AnalysisMode LastAnalysisMode { get; private set; } = AnalysisMode.DirectLlm;

        public Task<AnalysisExecutionOutcome> RunAsync(
            Guid analysisId,
            CancellationToken cancellationToken = default)
        {
            return RunAsync(analysisId, AnalysisMode.DirectLlm, cancellationToken);
        }

        public Task<AnalysisExecutionOutcome> RunAsync(
            Guid analysisId,
            AnalysisMode analysisMode,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastAnalysisId = analysisId;
            LastAnalysisMode = analysisMode;

            return Task.FromResult(outcome);
        }
    }

}

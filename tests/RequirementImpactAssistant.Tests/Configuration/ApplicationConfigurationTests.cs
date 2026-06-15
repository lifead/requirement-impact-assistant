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
using RequirementImpactAssistant.Web.Extensions;

namespace RequirementImpactAssistant.Tests.Configuration;

public sealed class ApplicationConfigurationTests
{
    private const string NonSecretConfigurationValue = "provided-by-test-configuration";

    [Fact]
    public void Configuration_LoadsDevelopmentSqliteConnectionString()
    {
        var configuration = CreateApplicationConfiguration();

        var connectionString = configuration.GetConnectionString("ApplicationDb");

        Assert.Equal(
            "Data Source=App_Data/requirement-impact-assistant.development.db",
            connectionString);
    }

    [Fact]
    public void ApplicationDbConnectionString_DoesNotContainSecrets()
    {
        var configuration = CreateApplicationConfiguration();

        var connectionString = configuration.GetConnectionString("ApplicationDb");

        Assert.NotNull(connectionString);
        Assert.DoesNotContain("Password=", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Pwd=", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User Id=", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Uid=", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Configuration_LoadsAiAnalysisProviderSelection()
    {
        var configuration = CreateApplicationConfiguration();
        var services = new ServiceCollection();
        services.AddApplicationAnalysis(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<AiAnalysisOptions>>().Value;

        Assert.Equal(LlmProviderNames.Demo, options.Provider);
        Assert.Equal("deepseek-chat", options.DeepSeek.Model);
        Assert.Equal("https://api.deepseek.com", options.DeepSeek.BaseUrl);
        Assert.Null(options.DeepSeek.ApiKey);
    }

    [Fact]
    public void AiAnalysisConfiguration_DoesNotContainProviderSecrets()
    {
        var configuration = CreateApplicationConfiguration();

        var keys = configuration
            .AsEnumerable()
            .Select(pair => pair.Key)
            .ToList();

        Assert.DoesNotContain(keys, key => key.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(keys, key => key.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(keys, key => key.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApplicationAnalysisRegistration_UsesDirectEngineAndExternalProviderBoundary()
    {
        var configuration = CreateApplicationConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton<ILlmProvider, NoopLlmProvider>();

        services.AddApplicationAnalysis(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IAiAnalysisEngine>();
        var selector = scope.ServiceProvider.GetRequiredService<IAiAnalysisEngineSelector>();
        var externalAdapter = scope.ServiceProvider.GetRequiredService<IExternalRagAdapter>();

        Assert.IsType<DirectLlmAnalysisEngine>(engine);
        Assert.IsType<MockExternalRagAdapter>(externalAdapter);
        Assert.IsType<DirectLlmAnalysisEngine>(selector.Select(AnalysisMode.DirectLlm));
        Assert.IsType<ExternalRagAnalysisEngine>(selector.Select(AnalysisMode.ExternalRag));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAnalysisExecutionService));
    }

    [Fact]
    public void ApplicationAnalysisRegistration_ConfiguredDifyKeepsDirectLlmDefault()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiAnalysis:Provider"] = LlmProviderNames.Demo,
                ["ExternalRag:Dify:Enabled"] = "true",
                ["ExternalRag:Dify:Endpoint"] = new UriBuilder(Uri.UriSchemeHttps, "dify.invalid").Uri.ToString(),
                ["ExternalRag:Dify:WorkflowOrAppId"] = "workflow-placeholder",
                ["ExternalRag:Dify:ApiKey"] = NonSecretConfigurationValue
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<ILlmProvider, NoopLlmProvider>();

        services.AddApplicationAnalysis(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var engine = scope.ServiceProvider.GetRequiredService<IAiAnalysisEngine>();
        var selector = scope.ServiceProvider.GetRequiredService<IAiAnalysisEngineSelector>();
        var externalAdapter = scope.ServiceProvider.GetRequiredService<IExternalRagAdapter>();

        Assert.IsType<DirectLlmAnalysisEngine>(engine);
        Assert.Same(engine, selector.Select(AnalysisMode.DirectLlm));
        Assert.IsType<ExternalRagAnalysisEngine>(selector.Select(AnalysisMode.ExternalRag));
        Assert.IsType<DifyExternalRagAdapter>(externalAdapter);
    }

    [Fact]
    public async Task ApplicationAnalysisRegistration_WiresMockAdapterForExternalRagMode()
    {
        var configuration = CreateApplicationConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton<ILlmProvider, NoopLlmProvider>();

        services.AddApplicationAnalysis(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var selector = scope.ServiceProvider.GetRequiredService<IAiAnalysisEngineSelector>();

        var response = await selector
            .Select(AnalysisMode.ExternalRag)
            .AnalyzeAsync(CreateAnalysisRequest());

        Assert.Equal(AiAnalysisResponseStatus.Succeeded, response.Status);
        Assert.NotNull(response.ResultMetadata);
        Assert.Equal(AnalysisMode.ExternalRag, response.ResultMetadata.AnalysisMode);
        Assert.Equal(nameof(ExternalRagAnalysisEngine), response.ResultMetadata.EngineName);
        Assert.Equal("LocalMockKnowledgeSource", response.ResultMetadata.ProviderName);
        Assert.Equal(nameof(MockExternalRagAdapter), response.ResultMetadata.AdapterName);
        Assert.Equal(RetrievedContextState.Available, response.ResultMetadata.RetrievedContextState);
    }

    [Fact]
    public void ApplicationAnalysisRegistration_SelectsDeepSeekProviderFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiAnalysis:Provider"] = LlmProviderNames.DeepSeek,
                ["AiAnalysis:DeepSeek:Model"] = "deepseek-chat",
                ["AiAnalysis:DeepSeek:BaseUrl"] = "https://api.deepseek.com",
                ["AiAnalysis:DeepSeek:ApiKey"] = NonSecretConfigurationValue
            })
            .Build();
        var services = new ServiceCollection();

        services.AddApplicationAnalysis(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var provider = scope.ServiceProvider.GetRequiredService<ILlmProvider>();
        var engine = scope.ServiceProvider.GetRequiredService<IAiAnalysisEngine>();

        Assert.IsType<DeepSeekLlmProvider>(provider);
        Assert.IsType<DirectLlmAnalysisEngine>(engine);
    }

    [Fact]
    public void PersistenceRegistration_RequiresOnlySqliteConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ApplicationDb"] = "Data Source=App_Data/test.db"
            })
            .Build();

        var services = new ServiceCollection();

        var contentRootPath = GetWebProjectPath();

        services.AddApplicationPersistence(configuration, contentRootPath);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.NotNull(dbContext);
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", dbContext.Database.ProviderName);
        Assert.Contains(
            Path.Combine(contentRootPath, "App_Data", "test.db"),
            dbContext.Database.GetConnectionString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersistenceRegistration_FailsWhenConnectionStringIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddApplicationPersistence(configuration, GetWebProjectPath()));

        Assert.Contains("Connection string 'ApplicationDb' is not configured.", exception.Message);
    }

    [Fact]
    public void DesignTimeFactory_CreatesSqliteApplicationDbContext()
    {
        var factory = new ApplicationDbContextFactory(GetWebProjectPath);

        using var dbContext = factory.CreateDbContext([]);

        Assert.NotNull(dbContext);
        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", dbContext.Database.ProviderName);
        Assert.Contains(
            Path.Combine(GetWebProjectPath(), "App_Data", "requirement-impact-assistant.development.db"),
            dbContext.Database.GetConnectionString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NoopLlmProvider : ILlmProvider
    {
        public Task<LlmProviderResponse> CompleteAsync(
            LlmProviderRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmProviderResponse(
                LlmProviderResponseStatus.Failed,
                ImpactMap: null,
                RawResponse: string.Empty,
                Errors: ["No provider implementation is registered in this configuration test."]));
    }

    private static IConfigurationRoot CreateApplicationConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(GetWebProjectPath())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();
    }

    private static AiAnalysisRequest CreateAnalysisRequest()
    {
        var snapshot = new AnalysisInputSnapshot(
            AnalysisId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Analysis: new AnalysisInputFields(
                Title: "Gateway migration",
                OriginalDescription: "Original requirement",
                ProjectRequest: "Project request",
                SituationDescription: "Current situation",
                ChangeSource: "Change source"),
            ContextFragments: []);

        return new AiAnalysisRequest(
            InputSnapshot: snapshot,
            InputSnapshotJson: "{\"analysisId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"}",
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default);
    }

    private static string GetWebProjectPath()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "RequirementImpactAssistant.Web"));
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Domain;
using RequirementImpactAssistant.Web.Domain.Enums;
using RequirementImpactAssistant.Web.Domain.Impact;
using RequirementImpactAssistant.Web.Extensions;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class Stage8ArchitectureRegressionTests
{
    [Fact]
    public void Selector_DirectLlmModeReturnsDirectEngineInstance()
    {
        var directEngine = CreateDirectEngine(new CapturingLlmProvider(CreateLlmResponse()));
        var externalEngine = new ExternalRagAnalysisEngine();
        var selector = new AiAnalysisEngineSelector(directEngine, externalEngine);

        var selectedEngine = selector.Select(AnalysisMode.DirectLlm);

        Assert.Same(directEngine, selectedEngine);
        Assert.IsType<DirectLlmAnalysisEngine>(selectedEngine);
    }

    [Fact]
    public void Selector_ExternalRagModeReturnsExternalEngineInstance()
    {
        var directEngine = CreateDirectEngine(new CapturingLlmProvider(CreateLlmResponse()));
        var externalEngine = new ExternalRagAnalysisEngine();
        var selector = new AiAnalysisEngineSelector(directEngine, externalEngine);

        var selectedEngine = selector.Select(AnalysisMode.ExternalRag);

        Assert.Same(externalEngine, selectedEngine);
        Assert.IsType<ExternalRagAnalysisEngine>(selectedEngine);
    }

    [Fact]
    public void Selector_UnsupportedModeThrowsControlledException()
    {
        var selector = new AiAnalysisEngineSelector(
            CreateDirectEngine(new CapturingLlmProvider(CreateLlmResponse())),
            new ExternalRagAnalysisEngine());

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => selector.Select((AnalysisMode)999));

        Assert.Equal("analysisMode", exception.ParamName);
        Assert.Contains("Unsupported analysis mode", exception.Message);
    }

    [Fact]
    public void ApplicationAnalysisRegistry_KeepsSelectorConcreteEnginesAndDirectDefault()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILlmProvider, CapturingLlmProvider>(_ => new CapturingLlmProvider(CreateLlmResponse()));

        services.AddApplicationAnalysis(CreateAnalysisConfiguration());

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(DirectLlmAnalysisEngine));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ExternalRagAnalysisEngine));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAiAnalysisEngineSelector));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IAiAnalysisEngine));

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var directEngine = scope.ServiceProvider.GetRequiredService<DirectLlmAnalysisEngine>();
        var externalEngine = scope.ServiceProvider.GetRequiredService<ExternalRagAnalysisEngine>();
        var defaultEngine = scope.ServiceProvider.GetRequiredService<IAiAnalysisEngine>();
        var selector = scope.ServiceProvider.GetRequiredService<IAiAnalysisEngineSelector>();

        Assert.Same(directEngine, defaultEngine);
        Assert.Same(directEngine, selector.Select(AnalysisMode.DirectLlm));
        Assert.Same(externalEngine, selector.Select(AnalysisMode.ExternalRag));
    }

    [Fact]
    public async Task SelectedDirectModeDoesNotCallExternalAdapterAndExternalModeDoesNotCallDirectProvider()
    {
        var provider = new CapturingLlmProvider(CreateLlmResponse());
        var adapter = new CapturingExternalRagAdapter(CreateExternalResponse());
        var selector = new AiAnalysisEngineSelector(
            CreateDirectEngine(provider),
            new ExternalRagAnalysisEngine(adapter));
        var request = CreateAnalysisRequest();

        var directResponse = await selector
            .Select(AnalysisMode.DirectLlm)
            .AnalyzeAsync(request);

        Assert.Equal(AiAnalysisResponseStatus.Succeeded, directResponse.Status);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(0, adapter.CallCount);

        var externalResponse = await selector
            .Select(AnalysisMode.ExternalRag)
            .AnalyzeAsync(request);

        Assert.Equal(AiAnalysisResponseStatus.Succeeded, externalResponse.Status);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, adapter.CallCount);
    }

    private static DirectLlmAnalysisEngine CreateDirectEngine(ILlmProvider provider) =>
        new(
            provider,
            Options.Create(new AiAnalysisOptions
            {
                Provider = LlmProviderNames.Demo
            }));

    private static IConfiguration CreateAnalysisConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiAnalysis:Provider"] = LlmProviderNames.Demo
            })
            .Build();

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
            ContextFragments:
            [
                new AnalysisContextFragmentSnapshot(
                    Id: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Type: "Task",
                    Source: "Task tracker",
                    Text: "Task context",
                    FileName: null)
            ]);

        return new AiAnalysisRequest(
            InputSnapshot: snapshot,
            InputSnapshotJson: "{\"analysisId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"}",
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default);
    }

    private static LlmProviderResponse CreateLlmResponse() =>
        new(
            LlmProviderResponseStatus.Succeeded,
            CreateImpactMap(),
            "direct response",
            []);

    private static ExternalRagAdapterResponse CreateExternalResponse() =>
        new(
            Status: ExternalRagAdapterResponseStatus.Completed,
            ImpactMap: CreateImpactMap(),
            Metadata: new ExternalRagAdapterResponseMetadata(
                ProviderName: "external-provider",
                AdapterName: "capturing-adapter",
                ModelName: "external-model",
                WorkflowName: "impact-workflow",
                ProfileName: "architecture-regression",
                SanitizedProperties: new Dictionary<string, string>()),
            RetrievedContextState: RetrievedContextState.MetadataOnly,
            RetrievedContextItems:
            [
                new RetrievedContextItem
                {
                    SourceTitle = "External context metadata",
                    ExternalReference = "external-context-1",
                    Completeness = RetrievedContextItemCompleteness.MetadataOnly,
                    ProviderName = "external-provider",
                    AdapterName = "capturing-adapter"
                }
            ],
            Warnings: [],
            Errors: [],
            SanitizedDiagnosticSnapshot: "external response");

    private static ImpactMap CreateImpactMap() =>
        new()
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

    private sealed class CapturingLlmProvider(LlmProviderResponse response) : ILlmProvider
    {
        public int CallCount { get; private set; }

        public Task<LlmProviderResponse> CompleteAsync(
            LlmProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return Task.FromResult(response);
        }
    }

    private sealed class CapturingExternalRagAdapter(ExternalRagAdapterResponse response) : IExternalRagAdapter
    {
        public int CallCount { get; private set; }

        public Task<ExternalRagAdapterResponse> AnalyzeAsync(
            ExternalRagAdapterRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return Task.FromResult(response);
        }
    }
}

using System.Text.Json;
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
    private static readonly string[] Stage8ForbiddenDiagnostics =
    [
        "sk-test-stage8-secret",
        "Authorization: Bearer",
        "https://stage8-secret.invalid"
    ];

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

    [Theory]
    [MemberData(nameof(RetrievedContextStateMatrixCases))]
    public async Task ExternalRagEngine_MapsRetrievedContextStateMatrixWithoutLeakingDiagnostics(
        string caseName,
        ExternalRagAdapterResponseStatus adapterStatus,
        AiAnalysisResponseStatus expectedStatus,
        RetrievedContextState expectedRetrievedContextState,
        IReadOnlyList<RetrievedContextItem> retrievedContextItems,
        IReadOnlyList<string> warnings,
        IReadOnlyList<ExternalRagAdapterError> errors,
        bool hasStructuredImpactMap,
        bool requiresWarningOrLimitation)
    {
        var adapter = new CapturingExternalRagAdapter(new ExternalRagAdapterResponse(
            Status: adapterStatus,
            ImpactMap: hasStructuredImpactMap ? CreateImpactMap() : null,
            Metadata: CreateExternalMetadata(caseName),
            RetrievedContextState: expectedRetrievedContextState,
            RetrievedContextItems: retrievedContextItems,
            Warnings: warnings,
            Errors: errors,
            SanitizedDiagnosticSnapshot: $$"""
                {"case":"{{caseName}}","diagnostic":"sanitized stage8 adapter diagnostic"}
                """));
        var engine = new ExternalRagAnalysisEngine(adapter);

        var response = await engine.AnalyzeAsync(CreateAnalysisRequest());

        Assert.Equal(1, adapter.CallCount);
        Assert.Equal(expectedStatus, response.Status);
        Assert.Equal(hasStructuredImpactMap && expectedStatus != AiAnalysisResponseStatus.Failed, response.ImpactMap is not null);
        Assert.NotNull(response.ResultMetadata);
        Assert.Equal(AnalysisMode.ExternalRag, response.ResultMetadata.AnalysisMode);
        Assert.Equal(expectedRetrievedContextState, response.ResultMetadata.RetrievedContextState);
        Assert.Equal(retrievedContextItems.Count, response.ResultMetadata.RetrievedContextItems.Count);

        if (expectedRetrievedContextState == RetrievedContextState.Available)
        {
            Assert.All(response.ResultMetadata.RetrievedContextItems, item =>
            {
                Assert.Equal(RetrievedContextItemCompleteness.FullText, item.Completeness);
                Assert.False(string.IsNullOrWhiteSpace(item.Text));
            });
            Assert.Empty(response.Errors);
            Assert.Empty(response.ResultMetadata.Warnings);
        }
        else
        {
            Assert.DoesNotContain(
                response.ResultMetadata.RetrievedContextItems,
                item => item.Completeness == RetrievedContextItemCompleteness.FullText &&
                    string.IsNullOrWhiteSpace(item.WarningOrLimitationNote) &&
                    expectedRetrievedContextState != RetrievedContextState.Partial);
        }

        if (requiresWarningOrLimitation)
        {
            Assert.NotEmpty(response.Errors);
            Assert.NotEmpty(response.ResultMetadata.Warnings);
        }

        if (expectedRetrievedContextState == RetrievedContextState.MetadataOnly)
        {
            var item = Assert.Single(response.ResultMetadata.RetrievedContextItems);
            Assert.Equal(RetrievedContextItemCompleteness.MetadataOnly, item.Completeness);
            Assert.NotNull(item.WarningOrLimitationNote);
            Assert.Null(item.Text);
            Assert.Null(item.Excerpt);
        }

        if (expectedRetrievedContextState == RetrievedContextState.Partial)
        {
            Assert.Contains(
                response.ResultMetadata.RetrievedContextItems,
                item => item.Completeness == RetrievedContextItemCompleteness.ExcerptOnly);
            Assert.Contains(
                response.ResultMetadata.RetrievedContextItems,
                item => item.Completeness == RetrievedContextItemCompleteness.FullText);
        }

        if (expectedRetrievedContextState == RetrievedContextState.Unavailable)
        {
            Assert.Empty(response.ResultMetadata.RetrievedContextItems);
        }

        AssertAnalysisResponseSanitized(response, Stage8ForbiddenDiagnostics);
    }

    [Fact]
    public async Task ExternalRagEngine_KeepsManualContextSeparateFromRetrievedContextItems()
    {
        var retrievedItem = new RetrievedContextItem
        {
            SourceTitle = "External knowledge base",
            ExternalReference = "EXT-1",
            Text = "Retrieved context from external source only.",
            Excerpt = "Retrieved context from external source only.",
            Completeness = RetrievedContextItemCompleteness.FullText,
            ProviderName = "external-provider",
            AdapterName = "capturing-adapter"
        };
        var adapter = new CapturingExternalRagAdapter(new ExternalRagAdapterResponse(
            Status: ExternalRagAdapterResponseStatus.Completed,
            ImpactMap: CreateImpactMap(),
            Metadata: CreateExternalMetadata("manual-context-separation"),
            RetrievedContextState: RetrievedContextState.Available,
            RetrievedContextItems: [retrievedItem],
            Warnings: [],
            Errors: [],
            SanitizedDiagnosticSnapshot: "{\"diagnostic\":\"sanitized stage8 adapter diagnostic\"}"));
        var engine = new ExternalRagAnalysisEngine(adapter);

        var response = await engine.AnalyzeAsync(CreateAnalysisRequest("Manual context: human-curated local note."));

        Assert.NotNull(adapter.LastRequest);
        Assert.NotNull(adapter.LastRequest.ManualContext);
        Assert.Equal("Manual context: human-curated local note.", adapter.LastRequest.ManualContext.CombinedText);
        Assert.True(adapter.LastRequest.CanForwardManualContextToExternalAiOrRag);

        Assert.NotNull(response.ResultMetadata);
        Assert.True(response.ResultMetadata.ManualContextForwardedToExternalAiOrRag);
        var savedRetrievedItem = Assert.Single(response.ResultMetadata.RetrievedContextItems);
        Assert.Equal("Retrieved context from external source only.", savedRetrievedItem.Text);
        Assert.DoesNotContain("Manual context:", savedRetrievedItem.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("human-curated local note", savedRetrievedItem.Excerpt, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> RetrievedContextStateMatrixCases()
    {
        yield return
        [
            "Available",
            ExternalRagAdapterResponseStatus.Completed,
            AiAnalysisResponseStatus.Succeeded,
            RetrievedContextState.Available,
            CreateAvailableRetrievedContextItems(),
            Array.Empty<string>(),
            Array.Empty<ExternalRagAdapterError>(),
            true,
            false
        ];

        yield return
        [
            "MetadataOnly",
            ExternalRagAdapterResponseStatus.CompletedWithWarnings,
            AiAnalysisResponseStatus.Partial,
            RetrievedContextState.MetadataOnly,
            CreateMetadataOnlyRetrievedContextItems(),
            new[] { "Retrieved context metadata was available without source text." },
            Array.Empty<ExternalRagAdapterError>(),
            true,
            true
        ];

        yield return
        [
            "Partial",
            ExternalRagAdapterResponseStatus.Partial,
            AiAnalysisResponseStatus.Partial,
            RetrievedContextState.Partial,
            CreatePartialRetrievedContextItems(),
            new[] { "Retrieved context was only partially available." },
            Array.Empty<ExternalRagAdapterError>(),
            true,
            true
        ];

        yield return
        [
            "Unavailable",
            ExternalRagAdapterResponseStatus.CompletedWithWarnings,
            AiAnalysisResponseStatus.Partial,
            RetrievedContextState.Unavailable,
            Array.Empty<RetrievedContextItem>(),
            new[] { "Structured result was returned without retrieved context." },
            Array.Empty<ExternalRagAdapterError>(),
            true,
            true
        ];

        yield return
        [
            "Failed",
            ExternalRagAdapterResponseStatus.Failed,
            AiAnalysisResponseStatus.Failed,
            RetrievedContextState.Unavailable,
            Array.Empty<RetrievedContextItem>(),
            new[] { "External adapter returned a sanitized failure." },
            new[]
            {
                new ExternalRagAdapterError(
                    Code: "external_unavailable",
                    Message: "External adapter result failed.",
                    DiagnosticDetails: "Sensitive provider details were withheld.")
            },
            false,
            true
        ];
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

    private static AiAnalysisRequest CreateAnalysisRequest(string contextText = "Task context")
    {
        var snapshot = new AnalysisInputSnapshot(
            AnalysisId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Analysis: new AnalysisInputFields(
                Title: "Gateway migration",
                ProjectRequestType: "ApiOrIntegrationChange",
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
                    Text: contextText,
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

    private static ExternalRagAdapterResponseMetadata CreateExternalMetadata(string caseName) =>
        new(
            ProviderName: "external-provider",
            AdapterName: "capturing-adapter",
            ModelName: "external-model",
            WorkflowName: "impact-workflow",
            ProfileName: $"stage8-{caseName}",
            SanitizedProperties: new Dictionary<string, string>
            {
                ["diagnostic"] = "sanitized"
            });

    private static IReadOnlyList<RetrievedContextItem> CreateAvailableRetrievedContextItems() =>
    [
        new RetrievedContextItem
        {
            SourceTitle = "External requirement catalogue",
            ExternalReference = "REQ-1",
            Text = "Full retrieved context text for the requirement catalogue.",
            Excerpt = "Full retrieved context text.",
            Completeness = RetrievedContextItemCompleteness.FullText,
            ProviderName = "external-provider",
            AdapterName = "capturing-adapter"
        },
        new RetrievedContextItem
        {
            SourceTitle = "External architecture decision",
            ExternalReference = "ADR-1",
            Text = "Full retrieved context text for the architecture decision.",
            Excerpt = "Architecture decision retrieved context.",
            Completeness = RetrievedContextItemCompleteness.FullText,
            ProviderName = "external-provider",
            AdapterName = "capturing-adapter"
        }
    ];

    private static IReadOnlyList<RetrievedContextItem> CreateMetadataOnlyRetrievedContextItems() =>
    [
        new RetrievedContextItem
        {
            SourceTitle = "External source metadata",
            SourceId = "source-1",
            ExternalReference = "META-1",
            Completeness = RetrievedContextItemCompleteness.MetadataOnly,
            ProviderName = "external-provider",
            AdapterName = "capturing-adapter",
            WarningOrLimitationNote = "Only source metadata was available."
        }
    ];

    private static IReadOnlyList<RetrievedContextItem> CreatePartialRetrievedContextItems() =>
    [
        new RetrievedContextItem
        {
            SourceTitle = "External requirement excerpt",
            ExternalReference = "REQ-2",
            Excerpt = "Excerpt-only retrieved context.",
            Completeness = RetrievedContextItemCompleteness.ExcerptOnly,
            ProviderName = "external-provider",
            AdapterName = "capturing-adapter",
            WarningOrLimitationNote = "Only an excerpt was available."
        },
        new RetrievedContextItem
        {
            SourceTitle = "External decision full text",
            ExternalReference = "ADR-2",
            Text = "Full retrieved context text for one mixed item.",
            Excerpt = "Full retrieved context text.",
            Completeness = RetrievedContextItemCompleteness.FullText,
            ProviderName = "external-provider",
            AdapterName = "capturing-adapter"
        }
    ];

    private static void AssertAnalysisResponseSanitized(
        AiAnalysisResponse response,
        IReadOnlyList<string> forbiddenTokens)
    {
        var serializedResponse = JsonSerializer.Serialize(response);
        var inspectedStrings = new List<string?>
        {
            serializedResponse,
            response.RawResponse,
            response.ResultMetadata?.ProviderName,
            response.ResultMetadata?.AdapterName,
            response.ResultMetadata?.ModelWorkflowProfileName
        };

        inspectedStrings.AddRange(response.Errors);
        if (response.ResultMetadata is not null)
        {
            inspectedStrings.AddRange(response.ResultMetadata.Warnings);
            inspectedStrings.AddRange(response.ResultMetadata.RetrievedContextItems.SelectMany(item => new[]
            {
                item.SourceTitle,
                item.SourceId,
                item.ExternalReference,
                item.FragmentId,
                item.Text,
                item.Excerpt,
                item.UrlOrReference,
                item.ProviderName,
                item.AdapterName,
                item.WarningOrLimitationNote
            }));
        }

        foreach (var forbiddenToken in forbiddenTokens)
        {
            Assert.All(inspectedStrings, value =>
                Assert.DoesNotContain(forbiddenToken, value ?? string.Empty, StringComparison.Ordinal));
        }
    }

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
}

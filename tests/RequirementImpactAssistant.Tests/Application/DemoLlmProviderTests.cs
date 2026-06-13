using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Domain.Impact;
using RequirementImpactAssistant.Web.Extensions;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class DemoLlmProviderTests
{
    [Fact]
    public async Task CompleteAsync_ReturnsSuccessfulResponseWithValidImpactMap()
    {
        var provider = new DemoLlmProvider();

        var response = await provider.CompleteAsync(CreateProviderRequest());

        Assert.Equal(LlmProviderResponseStatus.Succeeded, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Empty(response.Errors);
        Assert.False(string.IsNullOrWhiteSpace(response.RawResponse));
        Assert.Equal(ImpactMapItemType.ChangeSummary, response.ImpactMap.ChangeSummary.ItemType);
        Assert.Equal("Gateway migration", response.ImpactMap.ChangeSummary.Title);
        Assert.Equal(ImpactMapItemType.PreliminaryAssessment, response.ImpactMap.PreliminaryAssessment.ItemType);
        Assert.NotEmpty(response.ImpactMap.ChangeSummary.Description);
        Assert.NotEmpty(response.ImpactMap.PreliminaryAssessment.Description);

        var affectedRequirement = Assert.Single(response.ImpactMap.AffectedRequirements);
        Assert.Equal(ImpactMapItemType.AffectedRequirement, affectedRequirement.ItemType);
        Assert.Equal("affected-requirement-001", affectedRequirement.Id);
        Assert.Equal([Guid.Parse("11111111-1111-1111-1111-111111111111")], affectedRequirement.RelatedContextFragmentIds);

        Assert.Single(response.ImpactMap.AffectedTasks);
        Assert.Single(response.ImpactMap.Risks);
        Assert.Single(response.ImpactMap.ClarificationQuestions);
        Assert.Single(response.ImpactMap.OptionsForExpertReview);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsDeterministicResultForSameRequest()
    {
        var provider = new DemoLlmProvider();
        var request = CreateProviderRequest();

        var first = await provider.CompleteAsync(request);
        var second = await provider.CompleteAsync(request);

        Assert.Equal(first.RawResponse, second.RawResponse);
        Assert.Equal(
            JsonSerializer.Serialize(CreateComparableSnapshot(first.ImpactMap)),
            JsonSerializer.Serialize(CreateComparableSnapshot(second.ImpactMap)));
    }

    [Fact]
    public async Task CompleteAsync_DoesNotRequireSecretsOrNetworkDependencies()
    {
        var provider = new DemoLlmProvider();

        var response = await provider.CompleteAsync(CreateProviderRequest());

        Assert.Equal(LlmProviderResponseStatus.Succeeded, response.Status);
        Assert.Contains("No external provider, network, SDK, or secret is used.", response.RawResponse);
        Assert.DoesNotContain(
            typeof(DemoLlmProvider).GetConstructors().SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType.FullName?.Contains("HttpClient", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(
            typeof(DemoLlmProvider).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            field => field.FieldType.Namespace?.StartsWith("System.Net", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ApplicationAnalysisRegistration_SelectsDemoProviderFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiAnalysis:Provider"] = LlmProviderNames.Demo
            })
            .Build();
        var services = new ServiceCollection();

        services.AddApplicationAnalysis(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ILlmProvider>();
        var engine = scope.ServiceProvider.GetRequiredService<IAiAnalysisEngine>();

        Assert.IsType<DemoLlmProvider>(provider);
        Assert.IsType<DirectLlmAnalysisEngine>(engine);

        var response = await engine.AnalyzeAsync(CreateAnalysisRequest());

        Assert.Equal(AiAnalysisResponseStatus.Succeeded, response.Status);
        Assert.NotNull(response.ImpactMap);
        Assert.Empty(response.Errors);
    }

    private static LlmProviderRequest CreateProviderRequest() =>
        new(
            LlmProviderNames.Demo,
            "Demo prompt for Gateway migration",
            CreateAnalysisRequest());

    private static AiAnalysisRequest CreateAnalysisRequest()
    {
        var analysisId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var fragmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var snapshot = new AnalysisInputSnapshot(
            AnalysisId: analysisId,
            Analysis: new AnalysisInputFields(
                Title: "Gateway migration",
                OriginalDescription: "Original requirement for authentication gateway.",
                ProjectRequest: "Move the gateway authentication flow to the new service.",
                SituationDescription: "Current gateway is shared by several integrations.",
                ChangeSource: "Architecture review"),
            ContextFragments:
            [
                new AnalysisContextFragmentSnapshot(
                    Id: fragmentId,
                    Type: "Task",
                    Source: "Task tracker",
                    Text: "Update dependent implementation tasks.",
                    FileName: null)
            ]);

        return new AiAnalysisRequest(
            InputSnapshot: snapshot,
            InputSnapshotJson:
                "{\"analysisId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"analysis\":{\"title\":\"Gateway migration\",\"originalDescription\":\"Original requirement for authentication gateway.\",\"projectRequest\":\"Move the gateway authentication flow to the new service.\",\"situationDescription\":\"Current gateway is shared by several integrations.\",\"changeSource\":\"Architecture review\"},\"contextFragments\":[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"type\":\"Task\",\"source\":\"Task tracker\",\"text\":\"Update dependent implementation tasks.\",\"fileName\":null}]}",
            ExpectedResult: ExpectedAnalysisResultStructure.Default,
            BoundaryNotice: AnalysisBoundaryNotice.Default);
    }

    private static object CreateComparableSnapshot(ImpactMap? impactMap)
    {
        Assert.NotNull(impactMap);

        return new
        {
            impactMap.ChangeSummary,
            impactMap.AffectedRequirements,
            impactMap.AffectedTasks,
            impactMap.AffectedProjectDecisions,
            impactMap.AffectedApiInterfacesDocumentsTests,
            impactMap.AffectedArchitecturalConstraints,
            impactMap.AffectedOrganizationalContextItems,
            impactMap.Contradictions,
            impactMap.MissingInformation,
            impactMap.ClarificationQuestions,
            impactMap.Risks,
            impactMap.OptionsForExpertReview,
            impactMap.PreliminaryAssessment
        };
    }
}

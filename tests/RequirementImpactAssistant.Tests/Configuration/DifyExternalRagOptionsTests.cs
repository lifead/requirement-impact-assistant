using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Application.Analysis.External.Dify;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Extensions;

namespace RequirementImpactAssistant.Tests.Configuration;

public sealed class DifyExternalRagOptionsTests
{
    private const string NonSecretConfigurationValue = "provided-by-test-configuration";

    [Fact]
    public void Defaults_AreDisabledAndDoNotContainSecrets()
    {
        var options = new DifyExternalRagOptions();

        var status = options.GetConfigurationStatus();

        Assert.False(options.Enabled);
        Assert.False(options.IsConfigured);
        Assert.False(status.IsEnabled);
        Assert.False(status.IsConfigured);
        Assert.False(status.IsUnavailable);
        Assert.Empty(status.Reasons);
        Assert.Null(options.Endpoint);
        Assert.Null(options.WorkflowOrAppId);
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void EnabledOptions_WithMissingRequiredFields_ReturnUnavailableStatus()
    {
        var options = new DifyExternalRagOptions
        {
            Enabled = true
        };

        var status = options.GetConfigurationStatus();

        Assert.True(status.IsEnabled);
        Assert.False(status.IsConfigured);
        Assert.True(status.IsUnavailable);
        Assert.Contains("Dify endpoint is not configured.", status.Reasons);
        Assert.Contains("Dify workflow or application identifier is not configured.", status.Reasons);
        Assert.Contains("Dify API key is not configured.", status.Reasons);
    }

    [Fact]
    public void EnabledOptions_WithInvalidEndpointAndTimeout_ReturnUnavailableStatus()
    {
        var options = new DifyExternalRagOptions
        {
            Enabled = true,
            Endpoint = "not-a-uri",
            WorkflowOrAppId = "workflow-placeholder",
            ApiKey = NonSecretConfigurationValue,
            TimeoutSeconds = 0
        };

        var status = options.GetConfigurationStatus();

        Assert.False(status.IsConfigured);
        Assert.True(status.IsUnavailable);
        Assert.Contains("Dify endpoint must be an absolute HTTP or HTTPS URI.", status.Reasons);
        Assert.Contains("Dify timeout must be greater than zero seconds when configured.", status.Reasons);
    }

    [Fact]
    public void EnabledOptions_WithCompleteSettings_ReturnConfiguredStatus()
    {
        var options = new DifyExternalRagOptions
        {
            Enabled = true,
            Endpoint = new UriBuilder(Uri.UriSchemeHttps, "dify.invalid").Uri.ToString(),
            WorkflowOrAppId = "workflow-placeholder",
            ApiKey = NonSecretConfigurationValue,
            TimeoutSeconds = 30,
            ProfileName = "integration-test-profile"
        };

        var status = options.GetConfigurationStatus();

        Assert.True(options.IsConfigured);
        Assert.True(status.IsEnabled);
        Assert.True(status.IsConfigured);
        Assert.False(status.IsUnavailable);
        Assert.Empty(status.Reasons);
    }

    [Fact]
    public void ApplicationAnalysisRegistration_BindsDifyOptionsWithoutSwitchingExternalAdapter()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiAnalysis:Provider"] = LlmProviderNames.Demo,
                ["ExternalRag:Dify:Enabled"] = "true",
                ["ExternalRag:Dify:Endpoint"] = new UriBuilder(Uri.UriSchemeHttps, "dify.invalid").Uri.ToString(),
                ["ExternalRag:Dify:WorkflowOrAppId"] = "workflow-placeholder",
                ["ExternalRag:Dify:ApiKey"] = NonSecretConfigurationValue,
                ["ExternalRag:Dify:TimeoutSeconds"] = "30",
                ["ExternalRag:Dify:ProfileName"] = "integration-test-profile"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddApplicationAnalysis(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<IOptions<DifyExternalRagOptions>>().Value;
        var externalAdapter = scope.ServiceProvider.GetRequiredService<IExternalRagAdapter>();

        Assert.True(options.IsConfigured);
        Assert.Equal("workflow-placeholder", options.WorkflowOrAppId);
        Assert.Equal("integration-test-profile", options.ProfileName);
        Assert.IsType<MockExternalRagAdapter>(externalAdapter);
    }

    [Fact]
    public void ApplicationAnalysisRegistration_MissingDifySectionLeavesOptionsDisabled()
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

        var options = serviceProvider.GetRequiredService<IOptions<DifyExternalRagOptions>>().Value;
        var status = options.GetConfigurationStatus();

        Assert.False(options.Enabled);
        Assert.False(options.IsConfigured);
        Assert.False(status.IsUnavailable);
    }
}

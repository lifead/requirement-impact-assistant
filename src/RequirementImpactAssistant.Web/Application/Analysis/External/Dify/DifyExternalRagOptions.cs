namespace RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

public sealed class DifyExternalRagOptions
{
    public const string SectionName = "ExternalRag:Dify";

    public bool Enabled { get; set; }

    public string? Endpoint { get; set; }

    public string? WorkflowOrAppId { get; set; }

    // Bind from external configuration or user secrets only; do not store in appsettings*.json.
    public string? ApiKey { get; set; }

    public int? TimeoutSeconds { get; set; }

    public string? ProfileName { get; set; }

    public bool IsConfigured => GetConfigurationStatus().IsConfigured;

    public DifyExternalRagConfigurationStatus GetConfigurationStatus()
    {
        if (!Enabled)
        {
            return DifyExternalRagConfigurationStatus.Disabled();
        }

        var reasons = new List<string>();

        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            reasons.Add("Dify endpoint is not configured.");
        }
        else if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
        {
            reasons.Add("Dify endpoint must be an absolute HTTP or HTTPS URI.");
        }

        if (string.IsNullOrWhiteSpace(WorkflowOrAppId))
        {
            reasons.Add("Dify workflow or application identifier is not configured.");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            reasons.Add("Dify API key is not configured.");
        }

        if (TimeoutSeconds is <= 0)
        {
            reasons.Add("Dify timeout must be greater than zero seconds when configured.");
        }

        return reasons.Count == 0
            ? DifyExternalRagConfigurationStatus.Configured()
            : DifyExternalRagConfigurationStatus.Unavailable(reasons);
    }
}

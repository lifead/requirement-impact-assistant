namespace RequirementImpactAssistant.Web.Application.Analysis.External;

public sealed record ExternalRagAdapterResponseMetadata(
    string? ProviderName,
    string? AdapterName,
    string? ModelName,
    string? WorkflowName,
    string? ProfileName,
    IReadOnlyDictionary<string, string> SanitizedProperties);

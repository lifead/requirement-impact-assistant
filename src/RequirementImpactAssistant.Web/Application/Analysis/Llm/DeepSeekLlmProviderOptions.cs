namespace RequirementImpactAssistant.Web.Application.Analysis.Llm;

public sealed class DeepSeekLlmProviderOptions
{
    public string Model { get; set; } = "deepseek-chat";

    public string? BaseUrl { get; set; }

    // Bind from external configuration or user secrets only; do not store in appsettings*.json.
    public string? ApiKey { get; set; }
}

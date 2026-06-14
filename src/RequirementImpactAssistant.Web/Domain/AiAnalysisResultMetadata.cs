using RequirementImpactAssistant.Web.Domain.Enums;

namespace RequirementImpactAssistant.Web.Domain;

public sealed class AiAnalysisResultMetadata
{
    private List<string> warnings = [];
    private List<RetrievedContextItem> retrievedContextItems = [];

    public AnalysisMode AnalysisMode { get; set; } = AnalysisMode.DirectLlm;

    public string EngineName { get; set; } = string.Empty;

    public string? ProviderName { get; set; }

    public string? AdapterName { get; set; }

    public string? ModelWorkflowProfileName { get; set; }

    public RetrievedContextState RetrievedContextState { get; set; } = RetrievedContextState.Unavailable;

    public List<RetrievedContextItem> RetrievedContextItems
    {
        get => retrievedContextItems;
        set => retrievedContextItems = value ?? [];
    }

    public List<string> Warnings
    {
        get => warnings;
        set => warnings = value ?? [];
    }

    public bool ManualContextForwardedToExternalAiOrRag { get; set; }

    public static AiAnalysisResultMetadata CreateDefaultDirectLlm(
        string engineName = "",
        string? providerName = null,
        string? modelWorkflowProfileName = null,
        IEnumerable<string>? warnings = null) =>
        new()
        {
            AnalysisMode = AnalysisMode.DirectLlm,
            EngineName = engineName,
            ProviderName = NormalizeOptional(providerName),
            AdapterName = null,
            ModelWorkflowProfileName = NormalizeOptional(modelWorkflowProfileName),
            RetrievedContextState = RetrievedContextState.Unavailable,
            Warnings = NormalizeWarnings(warnings),
            ManualContextForwardedToExternalAiOrRag = false
        };

    public static AiAnalysisResultMetadata CreateLegacyMvp0Default(
        string engineName = "",
        string? providerName = null,
        string? modelName = null) =>
        CreateDefaultDirectLlm(
            engineName,
            providerName,
            modelName);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<string> NormalizeWarnings(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList()
        ?? [];
}

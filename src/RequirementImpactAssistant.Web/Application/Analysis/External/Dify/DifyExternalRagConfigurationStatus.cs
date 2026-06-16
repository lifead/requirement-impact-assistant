namespace RequirementImpactAssistant.Web.Application.Analysis.External.Dify;

public sealed record DifyExternalRagConfigurationStatus(
    bool IsEnabled,
    bool IsConfigured,
    IReadOnlyList<string> Reasons)
{
    public bool IsUnavailable => IsEnabled && !IsConfigured;

    public static DifyExternalRagConfigurationStatus Disabled() =>
        new(IsEnabled: false, IsConfigured: false, Reasons: []);

    public static DifyExternalRagConfigurationStatus Configured() =>
        new(IsEnabled: true, IsConfigured: true, Reasons: []);

    public static DifyExternalRagConfigurationStatus Unavailable(IReadOnlyList<string> reasons) =>
        new(IsEnabled: true, IsConfigured: false, Reasons: reasons);
}

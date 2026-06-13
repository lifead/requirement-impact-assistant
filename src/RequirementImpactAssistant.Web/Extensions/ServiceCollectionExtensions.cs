using Microsoft.EntityFrameworkCore;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Data;

namespace RequirementImpactAssistant.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ApplicationDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ApplicationDb' is not configured.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString));

        return services;
    }

    public static IServiceCollection AddApplicationAnalysis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AiAnalysisOptions>()
            .Bind(configuration.GetSection(AiAnalysisOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Provider), "AI analysis provider is required.")
            .Validate(
                options => !string.Equals(options.Provider, LlmProviderNames.DeepSeek, StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(options.DeepSeek.Model),
                "DeepSeek model is required when DeepSeek provider is selected.");

        services.AddScoped<IAiAnalysisEngine, DirectLlmAnalysisEngine>();

        return services;
    }
}

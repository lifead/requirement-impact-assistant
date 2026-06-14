using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;
using RequirementImpactAssistant.Web.Application.Export;
using RequirementImpactAssistant.Web.Data;

namespace RequirementImpactAssistant.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var connectionString = configuration.GetConnectionString("ApplicationDb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ApplicationDb' is not configured.");
        }

        connectionString = SqliteConnectionStringResolver.ResolveFileDataSource(
            connectionString,
            contentRootPath);

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
        services.AddScoped<IAnalysisExecutionService, AnalysisExecutionService>();
        services.AddScoped<IAnalysisInputAssembler, AnalysisInputAssembler>();
        services.TryAddScoped<DemoLlmProvider>();
        services.AddHttpClient<DeepSeekLlmProvider>();
        services.TryAddScoped<ILlmProvider>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiAnalysisOptions>>().Value;

            if (string.Equals(options.Provider, LlmProviderNames.Demo, StringComparison.OrdinalIgnoreCase))
            {
                return serviceProvider.GetRequiredService<DemoLlmProvider>();
            }

            if (string.Equals(options.Provider, LlmProviderNames.DeepSeek, StringComparison.OrdinalIgnoreCase))
            {
                return serviceProvider.GetRequiredService<DeepSeekLlmProvider>();
            }

            throw new InvalidOperationException(
                $"LLM provider '{options.Provider}' is configured, but no provider implementation is registered for it.");
        });

        return services;
    }

    public static IServiceCollection AddApplicationExport(this IServiceCollection services)
    {
        services.AddScoped<IAnalysisMarkdownExportService, AnalysisMarkdownExportService>();
        services.AddScoped<IAnalysisJsonExportService, AnalysisJsonExportService>();

        return services;
    }
}

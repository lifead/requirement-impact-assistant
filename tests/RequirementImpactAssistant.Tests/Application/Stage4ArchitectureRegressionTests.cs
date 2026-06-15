using System.Reflection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class Stage4ArchitectureRegressionTests
{
    private static readonly Assembly WebAssembly = typeof(IAiAnalysisEngine).Assembly;

    [Fact]
    public void ProductionCode_ContainsOnlyLocalMockExternalRagAdapterImplementation()
    {
        var implementations = WebAssembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.IsAssignableTo(typeof(IExternalRagAdapter)))
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([typeof(MockExternalRagAdapter).FullName], implementations);
    }

    [Fact]
    public void Stage4BoundaryTypes_DoNotDependOnNetworkSecretsProviderConfigOrRetrievalPipeline()
    {
        var forbiddenTypes = new[]
        {
            typeof(HttpClient),
            typeof(HttpMessageHandler),
            typeof(HttpRequestMessage),
            typeof(HttpResponseMessage),
            typeof(IConfiguration),
            typeof(IOptions<>)
        };

        var violations = GetStage4BoundaryTypes()
            .SelectMany(type => GetReferencedTypes(type)
                .Where(referencedType => forbiddenTypes.Any(forbiddenType => IsForbiddenTypeReference(forbiddenType, referencedType)))
                .Select(referencedType => $"{type.FullName} -> {referencedType.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Stage4BoundarySource_DoesNotIntroduceDifyRealCallsSecretsProviderConfigOrRetrievalPipeline()
    {
        var repositoryRoot = FindRepositoryRoot();

        var forbiddenTokens = new[]
        {
            "Dify",
            "HttpClient",
            "IHttpClientFactory",
            "HttpMessageHandler",
            "HttpRequestMessage",
            "HttpResponseMessage",
            "ApiKey",
            "Endpoint",
            "Secret",
            "UserSecrets",
            "IConfiguration",
            "IOptions",
            "GetSection",
            "GetEnvironmentVariable",
            "Embedding",
            "Embeddings",
            "Rerank",
            "RetrievalPipeline",
            "Vector",
            "VectorDb",
            "VectorDatabase",
            "Qdrant",
            "Pinecone",
            "Chroma",
            "Milvus"
        };

        var violations = GetStage4BoundarySourceFiles(repositoryRoot)
            .SelectMany(file =>
            {
                var source = File.ReadAllText(file);

                return forbiddenTokens
                    .Where(token => source.Contains(token, StringComparison.Ordinal))
                    .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} contains {token}");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void PageModels_DoNotDependOnStage4MockExternalAdapterBoundaryOrNetworkClients()
    {
        var forbiddenTypes = new[]
        {
            typeof(IExternalRagAdapter),
            typeof(MockExternalRagAdapter),
            typeof(ExternalRagAnalysisEngine),
            typeof(IAiAnalysisEngineSelector),
            typeof(HttpClient),
            typeof(HttpMessageHandler),
            typeof(HttpRequestMessage),
            typeof(HttpResponseMessage)
        };

        var violations = WebAssembly
            .GetTypes()
            .Where(type => type.IsAssignableTo(typeof(PageModel)))
            .SelectMany(type => GetReferencedTypes(type)
                .Where(referencedType =>
                    IsExternalAdapterModel(referencedType) ||
                    forbiddenTypes.Any(forbiddenType => forbiddenType.IsAssignableFrom(referencedType)))
                .Select(referencedType => $"{type.FullName} -> {referencedType.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void DirectLlmAnalysisEngine_DoesNotDependOnStage4MockOrExternalRagBoundary()
    {
        var forbiddenTypes = new[]
        {
            typeof(IExternalRagAdapter),
            typeof(MockExternalRagAdapter),
            typeof(ExternalRagAnalysisEngine),
            typeof(IAiAnalysisEngineSelector)
        };

        var violations = GetReferencedTypes(typeof(DirectLlmAnalysisEngine))
            .Where(referencedType =>
                IsExternalAdapterModel(referencedType) ||
                forbiddenTypes.Any(forbiddenType => forbiddenType.IsAssignableFrom(referencedType)))
            .Select(referencedType => $"{typeof(DirectLlmAnalysisEngine).FullName} -> {referencedType.FullName}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void DirectLlmSource_DoesNotReferenceStage4MockOrExternalRagTokens()
    {
        var repositoryRoot = FindRepositoryRoot();
        var file = Path.Combine(
            repositoryRoot,
            "src",
            "RequirementImpactAssistant.Web",
            "Application",
            "Analysis",
            "DirectLlmAnalysisEngine.cs");

        var forbiddenTokens = new[]
        {
            "RequirementImpactAssistant.Web.Application.Analysis.External",
            "IExternalRagAdapter",
            "ExternalRagAdapter",
            "MockExternalRagAdapter",
            "ExternalRagAnalysisEngine"
        };

        var source = File.ReadAllText(file);
        var violations = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} contains {token}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static bool IsExternalAdapterModel(Type type) =>
        type.Namespace == "RequirementImpactAssistant.Web.Application.Analysis.External";

    private static Type[] GetStage4BoundaryTypes() =>
        typeof(MockExternalRagAdapter).Assembly
            .GetTypes()
            .Where(type =>
                type.Namespace == "RequirementImpactAssistant.Web.Application.Analysis.External" ||
                type == typeof(ExternalRagAnalysisEngine) ||
                type == typeof(AiAnalysisEngineSelector) ||
                type == typeof(IAiAnalysisEngineSelector))
            .ToArray();

    private static string[] GetStage4BoundarySourceFiles(string repositoryRoot)
    {
        var analysisDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "RequirementImpactAssistant.Web",
            "Application",
            "Analysis");

        var externalDirectory = Path.Combine(analysisDirectory, "External");

        return Directory.EnumerateFiles(externalDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsUnderDifySpecificDirectory(file, externalDirectory))
            .Concat(new[]
            {
                Path.Combine(analysisDirectory, "ExternalRagAnalysisEngine.cs"),
                Path.Combine(analysisDirectory, "AiAnalysisEngineSelector.cs"),
                Path.Combine(analysisDirectory, "IAiAnalysisEngineSelector.cs")
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsUnderDifySpecificDirectory(string file, string externalDirectory)
    {
        var relativePath = Path.GetRelativePath(externalDirectory, file);

        return relativePath.StartsWith($"Dify{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
            relativePath.StartsWith($"Dify{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForbiddenTypeReference(Type forbiddenType, Type referencedType)
    {
        if (forbiddenType.IsGenericTypeDefinition &&
            referencedType.IsGenericType &&
            referencedType.GetGenericTypeDefinition() == forbiddenType)
        {
            return true;
        }

        return forbiddenType.IsAssignableFrom(referencedType);
    }

    private static IEnumerable<Type> GetReferencedTypes(Type type)
    {
        var flags = BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        foreach (var referencedType in ExpandType(type.BaseType))
        {
            yield return referencedType;
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            foreach (var referencedType in ExpandType(interfaceType))
            {
                yield return referencedType;
            }
        }

        foreach (var constructor in type.GetConstructors(flags))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                foreach (var referencedType in ExpandType(parameter.ParameterType))
                {
                    yield return referencedType;
                }
            }
        }

        foreach (var field in type.GetFields(flags))
        {
            foreach (var referencedType in ExpandType(field.FieldType))
            {
                yield return referencedType;
            }
        }

        foreach (var property in type.GetProperties(flags))
        {
            foreach (var referencedType in ExpandType(property.PropertyType))
            {
                yield return referencedType;
            }
        }

        foreach (var method in type.GetMethods(flags))
        {
            foreach (var referencedType in ExpandType(method.ReturnType))
            {
                yield return referencedType;
            }

            foreach (var parameter in method.GetParameters())
            {
                foreach (var referencedType in ExpandType(parameter.ParameterType))
                {
                    yield return referencedType;
                }
            }
        }
    }

    private static IEnumerable<Type> ExpandType(Type? type)
    {
        if (type is null)
        {
            yield break;
        }

        if (type.IsArray || type.IsByRef || type.IsPointer)
        {
            foreach (var referencedType in ExpandType(type.GetElementType()))
            {
                yield return referencedType;
            }

            yield break;
        }

        yield return type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            foreach (var referencedType in ExpandType(genericArgument))
            {
                yield return referencedType;
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "RequirementImpactAssistant.sln");

            if (File.Exists(solutionPath))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate RequirementImpactAssistant.sln.");
    }
}

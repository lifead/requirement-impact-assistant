using System.Reflection;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RequirementImpactAssistant.Web.Application.Analysis;
using RequirementImpactAssistant.Web.Application.Analysis.External;
using RequirementImpactAssistant.Web.Application.Analysis.Llm;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class Stage3ArchitectureRegressionTests
{
    private static readonly Assembly WebAssembly = typeof(IAiAnalysisEngine).Assembly;

    [Fact]
    public void PageModels_DoNotDependOnExternalAdapterProviderSpecificTypesOrNetworkClients()
    {
        var pageModelTypes = WebAssembly
            .GetTypes()
            .Where(type => type.IsAssignableTo(typeof(PageModel)))
            .ToArray();

        var forbiddenTypes = new[]
        {
            typeof(IExternalRagAdapter),
            typeof(ExternalRagAnalysisEngine),
            typeof(IAiAnalysisEngineSelector),
            typeof(ILlmProvider),
            typeof(DeepSeekLlmProvider),
            typeof(HttpClient),
            typeof(HttpMessageHandler),
            typeof(HttpRequestMessage),
            typeof(HttpResponseMessage)
        };

        var violations = pageModelTypes
            .SelectMany(type => GetReferencedTypes(type)
                .Where(IsExternalAdapterModelOrForbiddenType)
                .Select(referencedType => $"{type.FullName} -> {referencedType.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);

        bool IsExternalAdapterModelOrForbiddenType(Type referencedType) =>
            IsExternalAdapterModel(referencedType) ||
            forbiddenTypes.Any(forbiddenType => forbiddenType.IsAssignableFrom(referencedType));
    }

    [Fact]
    public void DirectLlmAnalysisEngine_DoesNotDependOnExternalAdapterBoundary()
    {
        var violations = GetReferencedTypes(typeof(DirectLlmAnalysisEngine))
            .Where(IsExternalAdapterModel)
            .Select(referencedType => $"{typeof(DirectLlmAnalysisEngine).FullName} -> {referencedType.FullName}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void PageModelsAndDirectLlmSource_DoNotReferenceExternalAdapterOrNetworkBoundaryTokens()
    {
        var repositoryRoot = FindRepositoryRoot();
        var webProjectDirectory = Path.Combine(repositoryRoot, "src", "RequirementImpactAssistant.Web");
        var pagesDirectory = Path.Combine(webProjectDirectory, "Pages");

        var files = Directory.EnumerateFiles(pagesDirectory, "*.cs", SearchOption.AllDirectories)
            .Append(Path.Combine(webProjectDirectory, "Application", "Analysis", "DirectLlmAnalysisEngine.cs"))
            .ToArray();

        var forbiddenTokens = new[]
        {
            "RequirementImpactAssistant.Web.Application.Analysis.External",
            "IExternalRagAdapter",
            "ExternalRagAdapter",
            "ExternalRagAnalysisEngine",
            "HttpClient",
            "HttpMessageHandler",
            "HttpRequestMessage",
            "HttpResponseMessage"
        };

        var violations = files
            .SelectMany(file => forbiddenTokens
                .Where(token => File.ReadAllText(file).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} contains {token}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ProductionCode_DoesNotContainExternalRagAdapterImplementation()
    {
        var implementations = WebAssembly
            .GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => type.IsAssignableTo(typeof(IExternalRagAdapter)))
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(implementations);
    }

    [Fact]
    public void Stage3BoundarySourceFiles_DoNotIntroduceProviderSpecificNetworkSecretOrRagImplementationTokens()
    {
        var repositoryRoot = FindRepositoryRoot();
        var analysisDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "RequirementImpactAssistant.Web",
            "Application",
            "Analysis");
        var externalDirectory = Path.Combine(analysisDirectory, "External");

        var files = Directory.EnumerateFiles(externalDirectory, "*.cs")
            .Concat(
            [
                Path.Combine(analysisDirectory, "ExternalRagAnalysisEngine.cs"),
                Path.Combine(analysisDirectory, "AiAnalysisEngineSelector.cs"),
                Path.Combine(analysisDirectory, "IAiAnalysisEngineSelector.cs")
            ])
            .ToArray();

        var forbiddenTokens = new[]
        {
            "Dify",
            "HttpClient",
            "HttpMessageHandler",
            "HttpRequestMessage",
            "HttpResponseMessage",
            "ApiKey",
            "Endpoint",
            "UserSecrets",
            "Secret",
            "MockExternalRagAdapter",
            "FakeExternalRagAdapter",
            "Embedding",
            "Embeddings",
            "Rerank",
            "VectorDb",
            "VectorDatabase",
            "Qdrant",
            "Pinecone",
            "Chroma",
            "Milvus"
        };

        var violations = files
            .SelectMany(file => forbiddenTokens
                .Where(token => File.ReadAllText(file).Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(repositoryRoot, file)} contains {token}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static bool IsExternalAdapterModel(Type type) =>
        type.Namespace == "RequirementImpactAssistant.Web.Application.Analysis.External";

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

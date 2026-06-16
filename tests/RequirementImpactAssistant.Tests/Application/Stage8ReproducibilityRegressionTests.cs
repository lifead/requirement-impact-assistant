using System.Text.Json;
using System.Text.RegularExpressions;

namespace RequirementImpactAssistant.Tests.Application;

public sealed class Stage8ReproducibilityRegressionTests
{
    private const string WebProjectName = "RequirementImpactAssistant.Web";
    private const string TestsProjectName = "RequirementImpactAssistant.Tests";
    private const string ThisFileName = nameof(Stage8ReproducibilityRegressionTests) + ".cs";
    private const string PublicDeepSeekBaseUrl = "https://api.deepseek.com";

    [Fact]
    public void AppsettingsFiles_DoNotContainDifyEndpointApiKeySecretsOrCorporateData()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appsettingsFiles = EnumerateAppsettingsFiles(repositoryRoot);
        var violations = appsettingsFiles
            .SelectMany(file => InspectAppsettingsFile(repositoryRoot, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(appsettingsFiles);
        Assert.Empty(violations);
    }

    [Fact]
    public void DefaultApplicationConfiguration_DoesNotMakeDifyOrProviderSecretsMandatory()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appsettingsFiles = EnumerateAppsettingsFiles(repositoryRoot);
        var violations = appsettingsFiles
            .SelectMany(file => InspectConfigurationKeys(repositoryRoot, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(appsettingsFiles);
        Assert.Empty(violations);
    }

    [Fact]
    public void MockExternalRagPath_IsLocalDeterministicAndDoesNotReadSecretsOrInitiateNetwork()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guardedFiles = new[]
        {
            "src/RequirementImpactAssistant.Web/Application/Analysis/External/MockExternalRagAdapter.cs",
            "tests/RequirementImpactAssistant.Tests/Application/MockExternalRagAdapterTests.cs",
            "tests/RequirementImpactAssistant.Tests/Support/" + "Mvp" + "1SmokeBaselineFixture.cs",
            "tests/RequirementImpactAssistant.Tests/Support/" + "Mvp" + "1SmokeBaselineFixtureTests.cs"
        };
        var forbiddenTokens = new[]
        {
            "HttpClient",
            "HttpMessageHandler",
            "HttpRequestMessage",
            "HttpResponseMessage",
            "IHttpClientFactory",
            "WebRequest",
            "SocketsHttpHandler",
            ".SendAsync(",
            ".GetAsync(",
            ".PostAsync(",
            ".GetStringAsync(",
            "Environment.GetEnvironmentVariable",
            "UserSecrets",
            "SecretManager",
            "IConfiguration",
            "ConfigurationBuilder",
            "ExternalRag:Dify",
            "DifyExternalRagAdapter",
            "DeepSeekLlmProvider",
            "Authorization",
            "Bearer"
        };

        var violations = guardedFiles
            .SelectMany(relativePath => FindForbiddenTokens(repositoryRoot, relativePath, forbiddenTokens))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void OptionalDifyAndDeepSeekProviderTests_UseFakeHttpMessageHandlersOnly()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guardedFiles = new[]
        {
            "tests/RequirementImpactAssistant.Tests/Application/DifyExternalRagAdapterTests.cs",
            "tests/RequirementImpactAssistant.Tests/Application/DeepSeekLlmProviderTests.cs",
            "tests/RequirementImpactAssistant.Tests/Application/AnalysisExecutionServiceTests.cs"
        };
        var violations = guardedFiles
            .SelectMany(relativePath => InspectOptionalProviderTestFile(repositoryRoot, relativePath))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void DifyManualRealProviderChecks_AreNotPartOfDefaultTestSuite()
    {
        var repositoryRoot = FindRepositoryRoot();
        var violations = EnumerateTestSourceFiles(repositoryRoot)
            .Where(file => !Path.GetFileName(file).Equals(ThisFileName, StringComparison.Ordinal))
            .SelectMany(file => FindDefaultSuiteRealProviderTestMarkers(repositoryRoot, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void DefaultSmokeArchitectureAndReproducibilityPaths_DoNotUseRealHttpClientOrHostedWebTests()
    {
        var repositoryRoot = FindRepositoryRoot();
        var guardedFiles = new[]
        {
            "tests/RequirementImpactAssistant.Tests/Application/Stage7SmokeRegressionTests.cs",
            "tests/RequirementImpactAssistant.Tests/Application/Stage8ArchitectureRegressionTests.cs",
            "tests/RequirementImpactAssistant.Tests/Application/Stage8ReproducibilityRegressionTests.cs"
        };
        var forbiddenTokens = new[]
        {
            "new " + "HttpClient(",
            "IHttp" + "ClientFactory ",
            "GetRequiredService<IHttp" + "ClientFactory>",
            "using Microsoft.AspNetCore.Mvc." + "Testing",
            "using Microsoft.AspNetCore." + "TestHost",
            "new WebApplication" + "Factory",
            ": WebApplication" + "Factory",
            "new Test" + "Server",
            "using Microsoft." + "Playwright",
            "using OpenQA." + "Selenium"
        };

        var violations = guardedFiles
            .SelectMany(relativePath => FindForbiddenTokens(repositoryRoot, relativePath, forbiddenTokens))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    private static IEnumerable<string> InspectAppsettingsFile(string repositoryRoot, string file)
    {
        var relativePath = NormalizeRelativePath(repositoryRoot, file);
        var json = File.ReadAllText(file);

        using var document = JsonDocument.Parse(json);
        foreach (var entry in FlattenJson(document.RootElement))
        {
            var key = entry.Path;
            var value = entry.Value;

            if (key.StartsWith("ExternalRag:Dify", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{relativePath} contains Dify configuration key {key}";
            }

            if (key.Contains("Dify", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{relativePath} contains Dify key {key}";
            }

            if (IsSecretLikeKey(key))
            {
                yield return $"{relativePath} contains secret-like key {key}";
            }

            if (value is null)
            {
                continue;
            }

            if (value.Equals(PublicDeepSeekBaseUrl, StringComparison.Ordinal))
            {
                continue;
            }

            if (LooksLikeSecretValue(value))
            {
                yield return $"{relativePath} contains secret-like value at {key}";
            }

            if (LooksLikeCorporateData(value))
            {
                yield return $"{relativePath} contains corporate-looking value at {key}";
            }
        }
    }

    private static IEnumerable<string> InspectConfigurationKeys(string repositoryRoot, string file)
    {
        var relativePath = NormalizeRelativePath(repositoryRoot, file);
        var json = File.ReadAllText(file);

        using var document = JsonDocument.Parse(json);
        foreach (var entry in FlattenJson(document.RootElement))
        {
            if (entry.Path.StartsWith("ExternalRag:Dify", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{relativePath} makes Dify configuration part of the committed default path";
            }

            if (entry.Path.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) ||
                entry.Path.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                entry.Path.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
                entry.Path.Contains("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{relativePath} contains provider credential key {entry.Path}";
            }
        }
    }

    private static IEnumerable<string> InspectOptionalProviderTestFile(string repositoryRoot, string relativePath)
    {
        var file = Path.Combine([repositoryRoot, ..relativePath.Split('/')]);
        if (!File.Exists(file))
        {
            yield return $"{relativePath} is missing";
            yield break;
        }

        var source = File.ReadAllText(file);
        var sourceWithoutNestedHandlers = RemoveNestedHandlerTypes(source);

        foreach (var violation in FindUnsafeHttpClientConstruction(relativePath, source))
        {
            yield return violation;
        }

        if (ContainsHttpClientFactoryUsage(source))
        {
            yield return $"{relativePath} uses IHttp" + "ClientFactory in optional provider tests";
        }

        if (source.Contains("TestServer", StringComparison.Ordinal) ||
            source.Contains("WebApplicationFactory", StringComparison.Ordinal) ||
            source.Contains("Microsoft.AspNetCore.Mvc.Testing", StringComparison.Ordinal) ||
            source.Contains("Microsoft.AspNetCore.TestHost", StringComparison.Ordinal))
        {
            yield return $"{relativePath} uses hosted web test infrastructure";
        }

        foreach (var testCase in EnumerateTestCases(source, relativePath))
        {
            if (!MentionsOptionalRealProvider(testCase.Source))
            {
                continue;
            }

            if (LooksLikeLiveProviderTestName(testCase.Name) &&
                !HasExplicitManualOrSkippedGuard(testCase.Attributes) &&
                !HasFakeHttpPath(testCase.Source, source))
            {
                yield return $"{relativePath}::{testCase.Name} looks like a live provider test without Skip/Explicit guard or fake HTTP";
            }

            if (ContainsProviderExecutionCall(testCase.Source) &&
                !HasFakeHttpPath(testCase.Source, source))
            {
                yield return $"{relativePath}::{testCase.Name} executes an optional provider without a local fake HttpMessageHandler path";
            }

            if (ContainsDefaultDiProviderExecutionPath(testCase.Source) &&
                !HasFakeHttpPath(testCase.Source, source))
            {
                yield return $"{relativePath}::{testCase.Name} resolves an optional provider through DI and can execute with the default HttpClient";
            }
        }

        if (sourceWithoutNestedHandlers.Contains(".SendAsync(", StringComparison.Ordinal) ||
            sourceWithoutNestedHandlers.Contains(".PostAsync(", StringComparison.Ordinal) ||
            sourceWithoutNestedHandlers.Contains(".GetAsync(", StringComparison.Ordinal) ||
            sourceWithoutNestedHandlers.Contains(".GetStringAsync(", StringComparison.Ordinal))
        {
            yield return $"{relativePath} initiates HTTP outside a fake HttpMessageHandler";
        }
    }

    private static IEnumerable<string> FindDefaultSuiteRealProviderTestMarkers(string repositoryRoot, string file)
    {
        var source = File.ReadAllText(file);
        var relativePath = NormalizeRelativePath(repositoryRoot, file);
        foreach (var testCase in EnumerateTestCases(source, relativePath))
        {
            if (!MentionsOptionalRealProvider(testCase.Source) &&
                !LooksLikeLiveProviderTestName(testCase.Name))
            {
                continue;
            }

            if (IsStaticInspectionTest(testCase))
            {
                continue;
            }

            if (HasFakeHttpPath(testCase.Source, source))
            {
                continue;
            }

            if (HasExplicitManualOrSkippedGuard(testCase.Attributes))
            {
                continue;
            }

            if (LooksLikeLiveProviderTestName(testCase.Name) ||
                ContainsRealProviderEnvironmentMarker(testCase.Source) ||
                ContainsDefaultHttpClientConstruction(testCase.Source) ||
                ContainsHttpClientFactoryUsage(testCase.Source) ||
                ContainsDefaultDiProviderExecutionPath(testCase.Source) ||
                ContainsProviderExecutionCall(testCase.Source) &&
                    ContainsProviderEndpointOrCredentialReference(testCase.Source))
            {
                yield return $"{relativePath}::{testCase.Name} can exercise a real optional provider in the default test suite";
            }
        }
    }

    private static IEnumerable<string> FindUnsafeHttpClientConstruction(string relativePath, string source)
    {
        foreach (Match match in Regex.Matches(source, @"new\s+HttpClient\s*\((?<arguments>[^;\r\n]*)\)"))
        {
            var arguments = match.Groups["arguments"].Value.Trim();
            if (string.IsNullOrWhiteSpace(arguments))
            {
                yield return $"{relativePath} creates an unhandled HttpClient";
                continue;
            }

            if (!LooksLikeFakeHttpHandlerArgument(arguments))
            {
                yield return $"{relativePath} creates an HttpClient without an explicit fake HttpMessageHandler argument";
            }
        }
    }

    private static bool ContainsDefaultHttpClientConstruction(string source) =>
        Regex.IsMatch(source, @"new\s+HttpClient\s*\(\s*\)");

    private static bool LooksLikeFakeHttpHandlerArgument(string arguments) =>
        arguments.Contains("handler", StringComparison.OrdinalIgnoreCase) &&
        !arguments.Contains("HttpClientHandler", StringComparison.Ordinal) &&
        !arguments.Contains("SocketsHttpHandler", StringComparison.Ordinal);

    private static bool ContainsHttpClientFactoryUsage(string source) =>
        source.Contains("IHttp" + "ClientFactory", StringComparison.Ordinal) ||
        source.Contains("GetRequiredService<IHttp" + "ClientFactory>", StringComparison.Ordinal) ||
        Regex.IsMatch(source, @"GetRequiredService\s*<\s*IHttp\s*ClientFactory\s*>");

    private static bool MentionsOptionalRealProvider(string source) =>
        source.Contains("DifyExternalRagAdapter", StringComparison.Ordinal) ||
        source.Contains("DifyExternalRagOptions", StringComparison.Ordinal) ||
        source.Contains("DeepSeekLlmProvider", StringComparison.Ordinal) ||
        source.Contains("LlmProviderNames.DeepSeek", StringComparison.Ordinal) ||
        source.Contains("AiAnalysis:DeepSeek", StringComparison.Ordinal) ||
        source.Contains("ExternalRag:Dify", StringComparison.Ordinal);

    private static bool ContainsProviderExecutionCall(string source) =>
        source.Contains(".CompleteAsync(", StringComparison.Ordinal) ||
        source.Contains(".AnalyzeAsync(", StringComparison.Ordinal) ||
        source.Contains(".RunAsync(", StringComparison.Ordinal) ||
        source.Contains(".SendAsync(", StringComparison.Ordinal) ||
        source.Contains(".PostAsync(", StringComparison.Ordinal) ||
        source.Contains(".GetAsync(", StringComparison.Ordinal) ||
        source.Contains(".GetStringAsync(", StringComparison.Ordinal);

    private static bool ContainsDefaultDiProviderExecutionPath(string source) =>
        source.Contains("AddApplicationAnalysis", StringComparison.Ordinal) &&
        ContainsProviderExecutionCall(source) &&
        (source.Contains("GetRequiredService<ILlmProvider>", StringComparison.Ordinal) ||
            source.Contains("GetRequiredService<DeepSeekLlmProvider>", StringComparison.Ordinal) ||
            source.Contains("GetRequiredService<DifyExternalRagAdapter>", StringComparison.Ordinal) ||
            source.Contains("GetRequiredService<IAnalysisExecutionService>", StringComparison.Ordinal) ||
            Regex.IsMatch(source, @"GetRequiredService\s*<\s*(ILlmProvider|DeepSeekLlmProvider|DifyExternalRagAdapter|IAnalysisExecutionService)\s*>"));

    private static bool HasFakeHttpPath(string testSource, string fileSource) =>
        testSource.Contains("ConfigurePrimaryHttpMessageHandler", StringComparison.Ordinal) ||
        testSource.Contains("HttpMessageHandler", StringComparison.Ordinal) &&
            testSource.Contains("handler", StringComparison.OrdinalIgnoreCase) ||
        testSource.Contains("CapturingHandler", StringComparison.Ordinal) ||
        testSource.Contains("CapturingHttpMessageHandler", StringComparison.Ordinal) ||
        testSource.Contains("DelayingHandler", StringComparison.Ordinal) ||
        Regex.IsMatch(testSource, @"Create(?:Adapter|Provider)\s*\(\s*handler\b") &&
            ContainsHandlerBackedFactory(fileSource) ||
        Regex.IsMatch(testSource, @"Create(?:Adapter|Provider)\s*\(\s*new\s+\w*Handler\b") &&
            ContainsHandlerBackedFactory(fileSource);

    private static bool ContainsHandlerBackedFactory(string source) =>
        Regex.IsMatch(source, @"Create(?:Adapter|Provider)\s*\([^)]*HttpMessageHandler\s+handler") &&
        Regex.IsMatch(source, @"new\s+HttpClient\s*\(\s*handler\s*\)");

    private static bool LooksLikeLiveProviderTestName(string testName)
    {
        if (!testName.Contains("Dify", StringComparison.OrdinalIgnoreCase) &&
            !testName.Contains("DeepSeek", StringComparison.OrdinalIgnoreCase) &&
            !testName.Contains("Provider", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return testName.Contains("LiveEndpoint", StringComparison.OrdinalIgnoreCase) ||
            testName.Contains("LiveProvider", StringComparison.OrdinalIgnoreCase) ||
            testName.Contains("LiveNetwork", StringComparison.OrdinalIgnoreCase) ||
            testName.Contains("RealProvider", StringComparison.OrdinalIgnoreCase) ||
            testName.Contains("RealNetwork", StringComparison.OrdinalIgnoreCase) ||
            testName.Contains("ManualProvider", StringComparison.OrdinalIgnoreCase) ||
            testName.Contains("ManualReal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStaticInspectionTest(TestCaseSource testCase) =>
        (testCase.Name.Contains("DoesNot", StringComparison.OrdinalIgnoreCase) ||
            testCase.Name.Contains("DoNot", StringComparison.OrdinalIgnoreCase) ||
            testCase.Name.Contains("OnlyInside", StringComparison.OrdinalIgnoreCase)) &&
        (testCase.Source.Contains("File.ReadAllText", StringComparison.Ordinal) ||
            testCase.Source.Contains("GetTypes()", StringComparison.Ordinal) ||
            testCase.Source.Contains("forbiddenTokens", StringComparison.Ordinal) ||
            testCase.Source.Contains("Assert.DoesNotContain", StringComparison.Ordinal));

    private static bool HasExplicitManualOrSkippedGuard(string attributes) =>
        attributes.Contains("Skip", StringComparison.OrdinalIgnoreCase) ||
        attributes.Contains("Explicit", StringComparison.OrdinalIgnoreCase) ||
        attributes.Contains("Category\", \"Manual", StringComparison.OrdinalIgnoreCase) ||
        attributes.Contains("Category\", \"Integration", StringComparison.OrdinalIgnoreCase) ||
        attributes.Contains("Category\", \"RealProvider", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsRealProviderEnvironmentMarker(string source)
    {
        var markers = new[]
        {
            "RUN_DIFY",
            "DIFY_API_KEY",
            "DIFY_ENDPOINT",
            "DEEPSEEK_API_KEY",
            "REAL_PROVIDER"
        };

        return markers.Any(marker => source.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsProviderEndpointOrCredentialReference(string source) =>
        source.Contains("https://api.deepseek.com", StringComparison.Ordinal) ||
        source.Contains("DIFY_ENDPOINT", StringComparison.OrdinalIgnoreCase) ||
        source.Contains("DEEPSEEK_API_KEY", StringComparison.OrdinalIgnoreCase) ||
        source.Contains("Environment.GetEnvironmentVariable", StringComparison.Ordinal);

    private static IEnumerable<TestCaseSource> EnumerateTestCases(string source, string relativePath)
    {
        var className = Regex.Match(source, @"\bclass\s+(?<name>[A-Za-z0-9_]+)").Groups["name"].Value;
        foreach (Match match in Regex.Matches(
            source,
            @"(?<attributes>(?:\s*\[[^\]]+\]\s*)+)\s*public\s+(?:async\s+)?(?:Task|void)\s+(?<name>[A-Za-z0-9_]+)\s*\("))
        {
            var attributes = match.Groups["attributes"].Value;
            if (!attributes.Contains("[Fact", StringComparison.Ordinal) &&
                !attributes.Contains("[Theory", StringComparison.Ordinal))
            {
                continue;
            }

            var bodyStart = source.IndexOf('{', match.Index + match.Length);
            if (bodyStart < 0)
            {
                continue;
            }

            var bodyEnd = FindMatchingBrace(source, bodyStart);
            if (bodyEnd < 0)
            {
                continue;
            }

            var methodSource = source[match.Index..(bodyEnd + 1)];
            var methodName = match.Groups["name"].Value;
            var qualifiedName = string.IsNullOrWhiteSpace(className)
                ? methodName
                : $"{className}.{methodName}";

            yield return new TestCaseSource(relativePath, qualifiedName, attributes, methodSource);
        }
    }

    private static IEnumerable<string> FindForbiddenTokens(
        string repositoryRoot,
        string relativePath,
        IEnumerable<string> forbiddenTokens)
    {
        var file = Path.Combine([repositoryRoot, ..relativePath.Split('/')]);
        if (!File.Exists(file))
        {
            yield return $"{relativePath} is missing";
            yield break;
        }

        var source = File.ReadAllText(file);

        foreach (var forbiddenToken in forbiddenTokens)
        {
            if (source.Contains(forbiddenToken, StringComparison.Ordinal))
            {
                yield return $"{relativePath} contains {forbiddenToken}";
            }
        }
    }

    private static IEnumerable<(string Path, string? Value)> FlattenJson(JsonElement element, string path = "")
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = string.IsNullOrWhiteSpace(path)
                    ? property.Name
                    : $"{path}:{property.Name}";

                foreach (var child in FlattenJson(property.Value, propertyPath))
                {
                    yield return child;
                }
            }

            yield break;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in FlattenJson(item, $"{path}:{index}"))
                {
                    yield return child;
                }

                index++;
            }

            yield break;
        }

        yield return (path, element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString());
    }

    private static bool IsSecretLikeKey(string key) =>
        key.Contains("ApiKey", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("Authorization", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("Bearer", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("Password", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSecretValue(string value)
    {
        if (value.Contains("Authorization", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Bearer", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("password=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("pwd=", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("api_key", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("apikey", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Regex.IsMatch(value, @"\b(sk|pk|pat|ghp|glpat|xox[baprs])-[A-Za-z0-9_\-]{8,}\b", RegexOptions.IgnoreCase);
    }

    private static bool LooksLikeCorporateData(string value) =>
        Regex.IsMatch(value, @"\b(company|corp|intranet|jira|confluence|sharepoint|ldap|adfs|sso)\.[A-Za-z0-9.-]+\b", RegexOptions.IgnoreCase) ||
        Regex.IsMatch(value, @"\b[A-Za-z0-9._%+-]+@(?!example\.|invalid\b)[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.IgnoreCase);

    private static string RemoveNestedHandlerTypes(string source)
    {
        var result = source;
        foreach (Match match in Regex.Matches(source, @"private sealed class \w+Handler\b"))
        {
            var typeStart = match.Index;
            var bodyStart = source.IndexOf('{', typeStart);
            if (bodyStart < 0)
            {
                continue;
            }

            var bodyEnd = FindMatchingBrace(source, bodyStart);
            if (bodyEnd < 0)
            {
                continue;
            }

            result = result.Replace(source[typeStart..(bodyEnd + 1)], string.Empty, StringComparison.Ordinal);
        }

        return result;
    }

    private static int FindMatchingBrace(string source, int openBraceIndex)
    {
        var depth = 0;

        for (var index = openBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static string[] EnumerateAppsettingsFiles(string repositoryRoot)
    {
        var productionRoot = Path.Combine(repositoryRoot, "src", WebProjectName);

        return Directory
            .EnumerateFiles(productionRoot, "appsettings*.json", SearchOption.TopDirectoryOnly)
            .Where(file => !IsUnderIgnoredDirectory(file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] EnumerateTestSourceFiles(string repositoryRoot)
    {
        var testRoot = Path.Combine(repositoryRoot, "tests", TestsProjectName);

        return Directory
            .EnumerateFiles(testRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsUnderIgnoredDirectory(file))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsUnderIgnoredDirectory(string file)
    {
        var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return parts.Any(part =>
            part.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeRelativePath(string repositoryRoot, string file) =>
        Path.GetRelativePath(repositoryRoot, file)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

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

    private sealed record TestCaseSource(
        string RelativePath,
        string Name,
        string Attributes,
        string Source);
}

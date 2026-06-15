namespace RequirementImpactAssistant.Tests.Application;

public sealed class StartupLoggingRegressionTests
{
    [Fact]
    public void Program_ConfiguresConsoleLoggingWithoutWindowsEventLogProvider()
    {
        var repositoryRoot = FindRepositoryRoot();
        var programPath = Path.Combine(repositoryRoot, "src", "RequirementImpactAssistant.Web", "Program.cs");
        var source = File.ReadAllText(programPath);

        var createBuilderIndex = source.IndexOf("WebApplication.CreateBuilder(args)", StringComparison.Ordinal);
        var clearProvidersIndex = source.IndexOf("builder.Logging.ClearProviders()", StringComparison.Ordinal);
        var addConsoleIndex = source.IndexOf("builder.Logging.AddConsole()", StringComparison.Ordinal);

        Assert.True(createBuilderIndex >= 0, "Program.cs should create a WebApplication builder.");
        Assert.True(clearProvidersIndex > createBuilderIndex, "Program.cs should clear default logging providers after builder creation.");
        Assert.True(addConsoleIndex > clearProvidersIndex, "Program.cs should add console logging after clearing default providers.");
        Assert.DoesNotContain("AddEventLog", source, StringComparison.Ordinal);
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

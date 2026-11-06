using System.Reflection;
using System.Xml.Linq;

namespace RideBound.ArchitectureTests;

public sealed class DependencyRuleTests
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedReferences =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["RideBound.Domain"] = [],
            ["RideBound.Contracts"] = [],
            ["RideBound.Application"] = ["RideBound.Domain"],
            ["RideBound.Algorithms"] =
                ["RideBound.Application", "RideBound.Domain"],
            ["RideBound.Solvers.OrTools"] =
                ["RideBound.Application", "RideBound.Domain"],
            ["RideBound.Runner"] =
                [
                    "RideBound.Algorithms",
                    "RideBound.Application",
                    "RideBound.Contracts",
                    "RideBound.Solvers.OrTools",
                ],
        };

    [Fact]
    public void Project_references_follow_clean_architecture()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectFiles = FindSourceProjectFiles(repositoryRoot);

        var failures = new List<string>();

        foreach (var projectFile in projectFiles)
        {
            var projectName = Path.GetFileNameWithoutExtension(projectFile);
            Assert.True(
                AllowedReferences.TryGetValue(projectName, out var allowed),
                $"Architecture policy is missing project '{projectName}'.");

            var actual = ReadProjectReferences(projectFile);
            var unexpected = actual.Except(allowed!, StringComparer.Ordinal).ToArray();

            if (unexpected.Length > 0)
            {
                failures.Add(
                    $"{projectName} has forbidden references: {string.Join(", ", unexpected)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData(@"..\RideBound.Domain\RideBound.Domain.csproj", "RideBound.Domain")]
    [InlineData("../RideBound.Domain/RideBound.Domain.csproj", "RideBound.Domain")]
    public void Project_reference_names_are_portable_across_path_styles(
        string projectReference,
        string expectedProjectName)
    {
        Assert.Equal(expectedProjectName, GetProjectName(projectReference));
    }

    [Theory]
    [InlineData("RideBound.Domain")]
    [InlineData("RideBound.Application")]
    [InlineData("RideBound.Contracts")]
    public void Inner_projects_do_not_reference_external_packages(string projectName)
    {
        var projectFile = FindProjectFile(projectName);
        var document = XDocument.Load(projectFile);
        var packages = document
            .Descendants("PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.True(
            packages.Length == 0,
            $"{projectName} must not reference packages: {string.Join(", ", packages)}");
    }

    [Fact]
    public void Domain_and_application_do_not_contain_framework_dependencies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var forbiddenTerms = new[]
        {
            "OptiGo",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Google.OrTools",
            "FleetPy",
            "RidePy",
            "AMoD2",
        };
        var failures = new List<string>();

        foreach (var projectName in new[] { "RideBound.Domain", "RideBound.Application" })
        {
            var projectDirectory = Path.Combine(repositoryRoot, projectName);

            var sourceFiles = Directory
                .GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path));

            foreach (var sourceFile in sourceFiles)
            {
                var source = File.ReadAllText(sourceFile);

                foreach (var forbiddenTerm in forbiddenTerms)
                {
                    if (source.Contains(forbiddenTerm, StringComparison.Ordinal))
                    {
                        failures.Add(
                            $"{Path.GetRelativePath(repositoryRoot, sourceFile)} contains " +
                            $"forbidden dependency term '{forbiddenTerm}'.");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void OrTools_package_is_pinned_only_in_the_solver_adapter()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projects = FindSourceProjectFiles(repositoryRoot);
        var references = projects
            .Select(
                path => new
                {
                    Path = path,
                    Packages = XDocument.Load(path)
                        .Descendants("PackageReference")
                        .Where(
                            element => string.Equals(
                                element.Attribute("Include")?.Value,
                                "Google.OrTools",
                                StringComparison.Ordinal))
                        .Select(
                            element => element.Attribute("Version")?.Value)
                        .ToArray(),
                })
            .Where(value => value.Packages.Length > 0)
            .ToArray();

        var solver = Assert.Single(references);
        Assert.Equal(
            "RideBound.Solvers.OrTools.csproj",
            Path.GetFileName(solver.Path));
        Assert.Equal("9.15.6755", Assert.Single(solver.Packages));
    }

    [Fact]
    public void Solver_neutral_port_is_defined_inside_application_source_boundary()
    {
        var repositoryRoot = FindRepositoryRoot();
        var portFile = Path.Combine(
            repositoryRoot,
            "RideBound.Application",
            "Optimization",
            "CandidateSelectionSolver.cs");

        Assert.True(File.Exists(portFile), "Application solver port is missing.");
        Assert.Contains(
            "public interface ICandidateSelectionSolver",
            File.ReadAllText(portFile),
            StringComparison.Ordinal);
    }

    private static string FindProjectFile(string projectName)
    {
        return Path.Combine(
            FindRepositoryRoot(),
            projectName,
            $"{projectName}.csproj");
    }

    // The source projects sit next to tests/ in the solution folder; the test projects are not checked.
    private static string[] FindSourceProjectFiles(string repositoryRoot)
    {
        var testsDirectory = Path.Combine(repositoryRoot, "tests") + Path.DirectorySeparatorChar;

        return Directory
            .GetFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(testsDirectory, StringComparison.Ordinal))
            .ToArray();
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ReadProjectReferences(string projectFile)
    {
        var document = XDocument.Load(projectFile);

        return document
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Select(value => GetProjectName(value!))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetProjectName(string projectReference)
    {
        var normalized = projectReference.Replace('\\', '/');
        var fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = typeof(DependencyRuleTests)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(
                attribute => attribute.Key == "RideBoundRepositoryRoot")
            ?.Value;

        if (configuredRoot is not null
            && File.Exists(Path.Combine(configuredRoot, "RideBound.slnx")))
        {
            return configuredRoot;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RideBound.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find the RideBound repository root.");
    }
}

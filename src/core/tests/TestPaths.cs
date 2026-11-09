using System.Reflection;

namespace RideBound.Tests;

/// <summary>
/// Finds src/core, the folder that holds RideBound.slnx, for the test projects that read files from the repository.
/// Each of those projects compiles this file (a link in its csproj).
/// </summary>
internal static class TestPaths
{
    /// <summary>
    /// The RideBoundRepositoryRoot value the test project writes into its assembly when that folder holds
    /// RideBound.slnx; otherwise the nearest folder above the test binaries that holds it.
    /// </summary>
    public static string Root()
    {
        var configured = typeof(TestPaths).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => attribute.Key == "RideBoundRepositoryRoot")
            ?.Value;

        if (configured is not null && File.Exists(Path.Combine(configured, "RideBound.slnx")))
        {
            return configured;
        }

        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "RideBound.slnx")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the folder that holds RideBound.slnx.");
    }
}

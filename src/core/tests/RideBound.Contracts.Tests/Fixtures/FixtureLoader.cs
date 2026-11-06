using System.Reflection;
using System.Text;

namespace RideBound.Contracts.Tests.Fixtures;

internal static class FixtureLoader
{
    private const string FixtureRoot = "benchmarks/schemas/fixtures";

    private const string SchemaRoot = "benchmarks/schemas";

    public static string ReadUtf8(string relativePath) =>
        ReadUnderRoot(FixtureRoot, relativePath);

    public static string ReadSchemaUtf8(string relativePath) =>
        ReadUnderRoot(SchemaRoot, relativePath);

    public static string GetSchemaPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalizedPath = ValidateRelativePath(relativePath, SchemaRoot);
        return BuildFullPath(SchemaRoot, normalizedPath);
    }

    private static string ReadUnderRoot(string assetRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalizedPath = ValidateRelativePath(relativePath, assetRoot);
        var fullPath = BuildFullPath(assetRoot, normalizedPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Asset '{normalizedPath}' was not found under '{assetRoot}'.",
                normalizedPath);
        }

        return File.ReadAllText(fullPath, new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true));
    }

    private static string ValidateRelativePath(string relativePath, string assetRoot)
    {
        var normalizedPath = relativePath.Replace('\\', '/');

        if (Path.IsPathRooted(normalizedPath)
            || normalizedPath.Split('/').Any(segment => segment == ".."))
        {
            throw new ArgumentException(
                $"Asset path must stay relative to {assetRoot}.",
                nameof(relativePath));
        }

        return normalizedPath;
    }

    private static string BuildFullPath(string assetRoot, string normalizedPath)
    {
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(
            repositoryRoot,
            assetRoot.Replace('/', Path.DirectorySeparatorChar),
            normalizedPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = typeof(FixtureLoader)
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
            "Could not find the RideBound repository root from the test output directory.");
    }
}

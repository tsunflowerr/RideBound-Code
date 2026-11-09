using System.Text;
using RideBound.Tests;

namespace RideBound.Contracts.Tests.Fixtures;

internal static class FixtureLoader
{
    private const string FixtureRoot = "protocol/fixtures";

    private const string SchemaRoot = "protocol/schemas";

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

    public static string GetFixturePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var normalizedPath = ValidateRelativePath(relativePath, FixtureRoot);
        return BuildFullPath(FixtureRoot, normalizedPath);
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
        return Path.Combine(
            TestPaths.Root(),
            assetRoot.Replace('/', Path.DirectorySeparatorChar),
            normalizedPath.Replace('/', Path.DirectorySeparatorChar));
    }
}

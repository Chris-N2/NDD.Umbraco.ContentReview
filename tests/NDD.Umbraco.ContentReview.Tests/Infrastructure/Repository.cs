namespace NDD.Umbraco.ContentReview.Tests.Infrastructure;

/// <summary>
/// Locates the repository the tests are running from, by walking up to the solution file.
/// </summary>
internal static class Repository
{
    public const string SolutionFileName = "NDD.Umbraco.ContentReview.slnx";

    public static string Root { get; } = FindRoot();

    public static string SourceDirectory => Path.Combine(Root, "src");

    private static string FindRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Could not find {SolutionFileName} above {AppContext.BaseDirectory}.");
    }
}

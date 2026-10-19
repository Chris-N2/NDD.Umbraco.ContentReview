namespace NDD.Umbraco.ContentReview.Tests.Infrastructure;

/// <summary>
/// The upper bound of a NuGet version range as written in a nuspec, e.g. "[17.7.1, 18.0.0)".
/// </summary>
internal sealed record UpperBound(Version Version, bool Inclusive);

internal static class NuGetRange
{
    /// <summary>
    /// Returns the range's upper bound, or null when it has none. A bare version ("17.7.1") means
    /// "17.7.1 or higher", which has no upper bound.
    /// </summary>
    public static UpperBound? GetUpperBound(string range)
    {
        range = range.Trim();
        if (range.Length == 0 || (range[0] != '[' && range[0] != '('))
        {
            return null;
        }

        bool inclusive = range[^1] == ']';
        string inner = range[1..^1];
        int comma = inner.IndexOf(',');

        // "[1.0]" is an exact version, so its own upper bound
        string upper = (comma < 0 ? inner : inner[(comma + 1)..]).Trim();
        if (upper.Length == 0)
        {
            return null;
        }

        int prerelease = upper.IndexOf('-');
        return new UpperBound(Version.Parse(prerelease < 0 ? upper : upper[..prerelease]), inclusive || comma < 0);
    }

    /// <summary>True when every version the range admits is below 18.0.0.</summary>
    public static bool ExcludesUmbraco18(string range) =>
        GetUpperBound(range) is { } bound
        && (bound.Version < new Version(18, 0, 0) || (bound.Version == new Version(18, 0, 0) && !bound.Inclusive));
}

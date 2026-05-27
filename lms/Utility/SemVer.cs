using System.Text.RegularExpressions;

namespace lms.Utility
{
    internal sealed record SemVer(
        uint Major,
        uint Minor,
        uint Patch,
        string PreChannel,
        int PreBuild,
        string Original)
    {
        public bool IsStable => PreChannel.Length == 0;
    }

    public static class SemVerHelper
    {
        private static readonly Regex VersionRegex = new(
            @"^(\d+)\.(\d+)\.(\d+)(?:-(alpha|beta|rc)(?:\.(\d+))?)?(?:\+[\w.\-]+)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static bool TryParse(string? raw, out SemVer? version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var match = VersionRegex.Match(raw.Trim());
            if (!match.Success)
            {
                return false;
            }

            if (!uint.TryParse(match.Groups[1].Value, out var major) ||
                !uint.TryParse(match.Groups[2].Value, out var minor) ||
                !uint.TryParse(match.Groups[3].Value, out var patch))
            {
                return false;
            }

            var preChannel = match.Groups[4].Success ? match.Groups[4].Value : "";
            var preBuild = 0;

            if (preChannel.Length > 0)
            {
                if (match.Groups[5].Success)
                {
                    if (!int.TryParse(match.Groups[5].Value, out preBuild) || preBuild < 1)
                    {
                        return false;
                    }
                }
                else
                {
                    preBuild = 1;
                }
            }

            version = new SemVer(major, minor, patch, preChannel, preBuild, raw);
            return true;
        }

        internal static int ChannelRank(string channel) => channel switch
        {
            "alpha" => 0,
            "beta" => 1,
            "rc" => 2,
            "" => 3,
            _ => 3,
        };

        internal static int Compare(SemVer a, SemVer b)
        {
            var cmp = a.Major.CompareTo(b.Major);
            if (cmp != 0) return cmp;

            cmp = a.Minor.CompareTo(b.Minor);
            if (cmp != 0) return cmp;

            cmp = a.Patch.CompareTo(b.Patch);
            if (cmp != 0) return cmp;

            cmp = ChannelRank(a.PreChannel).CompareTo(ChannelRank(b.PreChannel));
            if (cmp != 0) return cmp;

            if (!a.IsStable && !b.IsStable)
            {
                return a.PreBuild.CompareTo(b.PreBuild);
            }

            return 0;
        }

        public static List<string> SortDescending(IEnumerable<string> versions, ILogger? logger = null)
        {
            if (versions is null)
            {
                return new List<string>();
            }

            var parsed = new List<SemVer>();
            foreach (var raw in versions)
            {
                if (TryParse(raw, out var sv) && sv is not null)
                {
                    parsed.Add(sv);
                }
                else
                {
                    logger?.LogWarning("Skipping unparseable version string: {Version}", raw);
                }
            }

            parsed.Sort((a, b) => Compare(b, a));
            return parsed.Select(v => v.Original).ToList();
        }
    }
}

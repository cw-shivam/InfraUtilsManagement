using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace lms.Utility
{
    internal sealed record SemVer(
        uint Major,
        uint Minor,
        uint Patch,
        string PreChannel,
        int PreBuild,
        string Original
    )
    {
        public bool IsStable => PreChannel.Length == 0;
    }

    public static class SemVerHelper
    {
        // Group 1: Major, Group 2: Minor, Group 3: Patch
        // Group 4: The entire raw pre-release string (everything after the first '-')
        private static readonly Regex VersionRegex = new(
            @"^(\d+)\.(\d+)\.(\d+)(?:-([\w.\-]+))?(?:\+[\w.\-]+)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
        );

        // Helper regex used to extract the first sequence of digits found inside the pre-release string
        private static readonly Regex NumbersOnlyRegex = new(
            @"\d+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant
        );

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

            if (
                !uint.TryParse(match.Groups[1].Value, out var major)
                || !uint.TryParse(match.Groups[2].Value, out var minor)
                || !uint.TryParse(match.Groups[3].Value, out var patch)
            )
            {
                return false;
            }

            var rawPreRelease = match.Groups[4].Success ? match.Groups[4].Value : "";
            var preChannel = "";
            var preBuild = 0;

            if (rawPreRelease.Length > 0)
            {
                // 1. STRICT CHANNEL IDENTIFICATION
                // We verify that it explicitly contains one of our authorized channels.
                // If it doesn't, we explicitly reject the version.
                if (rawPreRelease.Contains("alpha", StringComparison.OrdinalIgnoreCase))
                    preChannel = "alpha";
                else if (rawPreRelease.Contains("beta", StringComparison.OrdinalIgnoreCase))
                    preChannel = "beta";
                else if (rawPreRelease.Contains("rc", StringComparison.OrdinalIgnoreCase))
                    preChannel = "rc";
                else
                {
                    // No valid channel name found (e.g., "1.2.3-preview" or "1.2.3-bta.1")
                    return false;
                }

                // 2. Extract the PreBuild number dynamically
                var numberMatch = NumbersOnlyRegex.Match(rawPreRelease);
                if (numberMatch.Success)
                {
                    if (!int.TryParse(numberMatch.Value, out preBuild) || preBuild < 1)
                    {
                        return false;
                    }
                }
                else
                {
                    // Default to 1 if the channel string is valid but has no trailing numbers (e.g., "1.2.3-beta")
                    preBuild = 1;
                }
            }

            version = new SemVer(major, minor, patch, preChannel, preBuild, raw);
            return true;
        }

        internal static int ChannelRank(string channel) =>
            channel.ToLowerInvariant() switch
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
            if (cmp != 0)
                return cmp;

            cmp = a.Minor.CompareTo(b.Minor);
            if (cmp != 0)
                return cmp;

            cmp = a.Patch.CompareTo(b.Patch);
            if (cmp != 0)
                return cmp;

            cmp = ChannelRank(a.PreChannel).CompareTo(ChannelRank(b.PreChannel));
            if (cmp != 0)
                return cmp;

            if (!a.IsStable && !b.IsStable)
            {
                return a.PreBuild.CompareTo(b.PreBuild);
            }

            return 0;
        }

        public static List<string> SortDescending(
            IEnumerable<string> versions,
            ILogger? logger = null
        )
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

namespace lms.Utility
{
    public class InvalidReleaseTypeException : Exception
    {
        public InvalidReleaseTypeException(string releaseType)
            : base($"invalid release type: {releaseType}") { }
    }

    public class ReverseChannelTransitionException : Exception
    {
        public ReverseChannelTransitionException(string currentChannel, string targetChannel)
            : base($"reverse channel transition not allowed: {currentChannel} -> {targetChannel}") { }
    }

    public class InvalidPreReleaseFormatException : Exception
    {
        public InvalidPreReleaseFormatException(string detail)
            : base($"invalid pre-release format: {detail}") { }
    }

    public class ReferenceVersionNotFoundException : Exception
    {
        public ReferenceVersionNotFoundException(string referenceVersion)
            : base($"reference version not found in available versions: {referenceVersion}") { }
    }

    public static class VersionBumper
    {
        public static string CalculateNextVersion(
            IEnumerable<string> availableVersions,
            string releaseType,
            ILogger? logger = null,
            string? referenceVersion = null)
        {
            var sorted = SemVerHelper.SortDescending(availableVersions, logger);

            string baseVersion;
            if (referenceVersion is null)
            {
                if (sorted.Count == 0)
                {
                    throw new InvalidOperationException("no valid versions to bump from");
                }
                baseVersion = sorted[0];
            }
            else
            {
                if (!SemVerHelper.TryParse(referenceVersion, out _) || !sorted.Contains(referenceVersion))
                {
                    throw new ReferenceVersionNotFoundException(referenceVersion);
                }
                baseVersion = referenceVersion;
            }

            if (!SemVerHelper.TryParse(baseVersion, out var current) || current is null)
            {
                throw new InvalidOperationException($"failed to parse base version: {baseVersion}");
            }

            var normalized = NormalizeReleaseType(releaseType);

            return normalized switch
            {
                "patch" or "minor" or "major" => StableBump(current, normalized),
                "alpha" or "beta" or "rc" => PreReleaseBump(current, normalized),
                _ => throw new InvalidReleaseTypeException(normalized),
            };
        }

        private static string NormalizeReleaseType(string releaseType)
        {
            var normalized = (releaseType ?? "").Trim().ToLowerInvariant();
            if (normalized == "breaking")
            {
                normalized = "major";
            }

            return normalized switch
            {
                "patch" or "minor" or "major" or "alpha" or "beta" or "rc" => normalized,
                _ => throw new InvalidReleaseTypeException(normalized),
            };
        }

        private static string StableBump(SemVer version, string releaseType)
        {
            var major = version.Major;
            var minor = version.Minor;
            var patch = version.Patch;

            return releaseType switch
            {
                "patch" => $"{major}.{minor}.{patch + 1}",
                "minor" => $"{major}.{minor + 1}.0",
                "major" => $"{major + 1}.0.0",
                _ => throw new InvalidReleaseTypeException(releaseType),
            };
        }

        private static string PreReleaseBump(SemVer version, string targetChannel)
        {
            var currentChannel = version.PreChannel;
            var currentBuild = version.PreBuild;

            if (currentChannel.Length == 0)
            {
                return FormatPreRelease(version.Major, version.Minor, version.Patch, targetChannel, 1);
            }

            if (currentChannel == targetChannel)
            {
                return FormatPreRelease(version.Major, version.Minor, version.Patch, targetChannel, currentBuild + 1);
            }

            if (SemVerHelper.ChannelRank(targetChannel) > SemVerHelper.ChannelRank(currentChannel))
            {
                return FormatPreRelease(version.Major, version.Minor, version.Patch, targetChannel, 1);
            }

            throw new ReverseChannelTransitionException(currentChannel, targetChannel);
        }

        private static string FormatPreRelease(uint major, uint minor, uint patch, string channel, int buildNumber)
        {
            if (buildNumber < 1)
            {
                throw new InvalidPreReleaseFormatException($"build number must be >= 1, got {buildNumber}");
            }
            return $"{major}.{minor}.{patch}-{channel}.{buildNumber}";
        }
    }
}

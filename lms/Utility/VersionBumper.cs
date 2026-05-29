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
            : base($"reverse channel transition not allowed: {currentChannel} -> {targetChannel}")
        { }
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
            ILogger? logger = null
        )
        {
            var sorted = SemVerHelper.SortDescending(availableVersions, logger);
            if (sorted == null || sorted.Count == 0)
            {
                throw new InvalidOperationException(
                    "No available versions provided to calculate the next step."
                );
            }

            string baseVersion = sorted[0];

            if (!SemVerHelper.TryParse(baseVersion, out var current) || current is null)
            {
                throw new InvalidOperationException($"failed to parse base version: {baseVersion}");
            }

            var normalized = NormalizeReleaseType(releaseType);

            return normalized switch
            {
                "patch" or "minor" or "major" => StableBump(current, normalized),
                "alpha"
                or "beta"
                or "rc"
                or "minor-alpha"
                or "minor-beta"
                or "minor-rc"
                or "major-alpha"
                or "major-beta"
                or "major-rc"
                or "patch-alpha"
                or "patch-beta"
                or "patch-rc" => PreReleaseBump(current, normalized),
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
                "patch"
                or "minor"
                or "major"
                or "alpha"
                or "beta"
                or "rc"
                or "minor-alpha"
                or "minor-beta"
                or "minor-rc"
                or "major-alpha"
                or "major-beta"
                or "major-rc"
                or "patch-alpha"
                or "patch-beta"
                or "patch-rc" => normalized,
                _ => throw new InvalidReleaseTypeException(normalized),
            };
        }

        private static string StableBump(SemVer version, string releaseType)
        {
            var major = version.Major;
            var minor = version.Minor;
            var patch = version.Patch;

            // If bumping a pre-release version to a clean stable version (e.g. 1.2.3-beta.1 -> stable patch)
            // standard semver dictates you consume the current values without incrementing the digits further.
            if (!version.IsStable)
            {
                return $"{major}.{minor}.{patch}";
            }

            return releaseType switch
            {
                "patch" => $"{major}.{minor}.{patch + 1}",
                "minor" => $"{major}.{minor + 1}.0",
                "major" => $"{major + 1}.0.0",
                _ => throw new InvalidReleaseTypeException(releaseType),
            };
        }

        private static string PreReleaseBump(SemVer version, string releaseType)
        {
            string bumpScope; // "major", "minor", "patch", or empty ""
            string targetChannel; // "alpha", "beta", or "rc"

            if (releaseType.Contains('-'))
            {
                var parts = releaseType.Split('-');
                bumpScope = parts[0];
                targetChannel = parts[1];
            }
            else
            {
                bumpScope = "";
                targetChannel = releaseType;
            }

            var currentChannel = version.PreChannel;
            var currentBuild = version.PreBuild;

            // CASE 1: Current version is a STABLE release (No pre-release active)
            if (version.IsStable)
            {
                if (string.IsNullOrEmpty(bumpScope))
                {
                    throw new InvalidPreReleaseFormatException(
                        $"Cannot use '{targetChannel}' only for latestVersion '{version.Original}'. A prefix scope (major-, minor-, patch-) is strictly required when transitioning from a stable version to preReleaseVersion. You can use (major|minor|patch)-{targetChannel} if the latest version is a stable release , else if the latest version is already on a prerelease channel you can use simple {targetChannel}, it will icrement the build number"
                    );
                }

                return bumpScope switch
                {
                    "major" => FormatPreRelease(version.Major + 1, 0, 0, targetChannel, 1),
                    "minor" => FormatPreRelease(
                        version.Major,
                        version.Minor + 1,
                        0,
                        targetChannel,
                        1
                    ),
                    "patch" => FormatPreRelease(
                        version.Major,
                        version.Minor,
                        version.Patch + 1,
                        targetChannel,
                        1
                    ),
                    _ => throw new InvalidReleaseTypeException(bumpScope),
                };
            }

            // CASE 2: Current version is ALREADY a pre-release
            // If the channel matches, increment the build integer sequence
            if (currentChannel.Equals(targetChannel, StringComparison.OrdinalIgnoreCase))
            {
                return FormatPreRelease(
                    version.Major,
                    version.Minor,
                    version.Patch,
                    currentChannel,
                    currentBuild + 1
                );
            }

            // If shifting to a new channel (e.g., beta -> rc), validate progression ranking
            int currentRank = SemVerHelper.ChannelRank(currentChannel);
            int targetRank = SemVerHelper.ChannelRank(targetChannel);

            if (targetRank > currentRank)
            {
                return FormatPreRelease(
                    version.Major,
                    version.Minor,
                    version.Patch,
                    targetChannel,
                    1
                );
            }

            throw new ReverseChannelTransitionException(currentChannel, targetChannel);
        }

        private static string FormatPreRelease(
            uint major,
            uint minor,
            uint patch,
            string channel,
            int buildNumber
        )
        {
            if (buildNumber < 1)
            {
                throw new InvalidPreReleaseFormatException(
                    $"build number must be >= 1, got {buildNumber}"
                );
            }
            return $"{major}.{minor}.{patch}-{channel}.{buildNumber}";
        }
    }
}

namespace lms.Utility
{
    public static class UrlMaker
    {
        public const string ServerNuget = "nuget";
        public const string ServerNpm = "npm";

        public static string createNugetUrl(string packageName, string nugetServer)
        {
            return $"{nugetServer.TrimEnd('/')}/package/{packageName}/index.json";
        }

        public static string createNpmUrl(string packageName, string npmServer)
        {
            return $"{npmServer.TrimEnd('/')}/{packageName}";
        }

        public static string createPackageVersionsUrl(
            string packageName,
            string server,
            string nugetServer,
            string npmServer
        )
        {
            if (string.IsNullOrWhiteSpace(server))
            {
                throw new ArgumentException(
                    $"server is required: must be exactly one of '{ServerNpm}' or '{ServerNuget}' (a package cannot belong to both).",
                    nameof(server)
                );
            }

            return server.Trim().ToLowerInvariant() switch
            {
                ServerNuget => createNugetUrl(packageName, nugetServer),
                ServerNpm => createNpmUrl(packageName, npmServer),
                _ => throw new ArgumentException(
                    $"unsupported server '{server}': a package must belong to exactly one of '{ServerNpm}' or '{ServerNuget}' (not both).",
                    nameof(server)
                ),
            };
        }
    }
}

using System.Text.Json;

namespace lms.Utility
{
    public static class RegistryVersionsParser
    {
        public static IEnumerable<string> ParseVersions(string server, string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return Array.Empty<string>();
            }

            using var doc = JsonDocument.Parse(jsonContent);
            if (!doc.RootElement.TryGetProperty("versions", out var versionsElement))
            {
                return Array.Empty<string>();
            }

            return server.Trim().ToLowerInvariant() switch
            {
                UrlMaker.ServerNuget => ParseNugetArray(versionsElement),
                UrlMaker.ServerNpm => ParseNpmObject(versionsElement),
                _ => throw new ArgumentException(
                    $"unsupported server: '{server}' (expected '{UrlMaker.ServerNuget}' or '{UrlMaker.ServerNpm}')",
                    nameof(server)
                ),
            };
        }

        private static List<string> ParseNugetArray(JsonElement versions)
        {
            if (versions.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    "nuget response: 'versions' is not a JSON array"
                );
            }

            var result = new List<string>();
            foreach (var item in versions.EnumerateArray())
            {
                var v = item.GetString();
                if (!string.IsNullOrWhiteSpace(v))
                {
                    result.Add(v!);
                }
            }
            return result;
        }

        private static List<string> ParseNpmObject(JsonElement versions)
        {
            if (versions.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    "npm response: 'versions' is not a JSON object"
                );
            }

            var result = new List<string>();
            foreach (var prop in versions.EnumerateObject())
            {
                if (!string.IsNullOrWhiteSpace(prop.Name))
                {
                    result.Add(prop.Name);
                }
            }
            return result;
        }
    }
}

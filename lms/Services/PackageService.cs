using lms.Entities;
using lms.Interfaces;
using lms.Utility;

namespace lms.Services
{
    public class PackageService : IPackageService
    {
        private readonly ILogger<PackageService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public PackageService(
            ILogger<PackageService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory
        )
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> CalculateNextTag(PackageEntity packageEntity)
        {
            if (packageEntity == null)
            {
                return "PackageEntity is null";
            }

            if (string.IsNullOrWhiteSpace(packageEntity.server))
            {
                _logger.LogWarning(
                    "Missing server for package {Package}",
                    packageEntity.packageName
                );
                return "server is required (npm or nuget)";
            }

            var nugetServer = _configuration.GetValue<string>("nugetServer") ?? "";
            var npmServer = _configuration.GetValue<string>("npmServer") ?? "";
            var packageUrl = UrlMaker.createPackageVersionsUrl(
                packageEntity.packageName,
                packageEntity.server,
                nugetServer,
                npmServer
            );

            _logger.LogInformation(
                "Fetching versions for {Package} from {Url}",
                packageEntity.packageName,
                packageUrl
            );

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(packageUrl);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var versions = RegistryVersionsParser
                .ParseVersions(packageEntity.server, content)
                .ToList();

            string? reference = string.IsNullOrWhiteSpace(packageEntity.latestVersion)
                ? null
                : packageEntity.latestVersion;

            var next = VersionBumper.CalculateNextVersion(
                versions,
                packageEntity.channel,
                _logger,
                reference
            );

            _logger.LogInformation(
                "Next version for {Package} ({Channel}): {Next}",
                packageEntity.packageName,
                packageEntity.channel,
                next
            );

            return next;
        }
    }
}

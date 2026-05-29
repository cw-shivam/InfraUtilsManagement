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
                throw new ArgumentNullException(
                    nameof(packageEntity),
                    "Package configuration data is missing."
                );
            }

            if (string.IsNullOrWhiteSpace(packageEntity.server))
            {
                throw new ArgumentException(
                    $"Registry server framework type (npm/nuget) is required, but was missing for package: '{packageEntity.packageName}'.",
                    nameof(packageEntity.server)
                );
            }

            if (string.IsNullOrWhiteSpace(packageEntity.packageName))
            {
                throw new ArgumentException(
                    "Package name cannot be empty or null.",
                    nameof(packageEntity.packageName)
                );
            }

            if (string.IsNullOrWhiteSpace(packageEntity.environment))
            {
                throw new ArgumentException(
                    "environment not specified",
                    nameof(packageEntity.packageName)
                );
            }

            string content;
            var nugetServer = "";
            var npmServer = "";
            if (packageEntity.environment == "dev")
            {
                nugetServer = _configuration.GetValue<string>("nugetServerDev") ?? "";
                npmServer = _configuration.GetValue<string>("npmServerDev") ?? "";
            }
            else
            {
                nugetServer = _configuration.GetValue<string>("nugetServerProd") ?? "";
                npmServer = _configuration.GetValue<string>("npmServerProd") ?? "";
            }
            if (npmServer == "" && nugetServer == "")
            {
                throw new Exception("Server url not found");
            }
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

            try
            {
                var client = _httpClientFactory.CreateClient();
                using var response = await client.GetAsync(packageUrl);

                if (!response.IsSuccessStatusCode)
                {
                    throw new PackageVersionsFetchException(
                        packageEntity.packageName,
                        packageEntity.server,
                        $"Remote registry server returned an error error ({response.StatusCode}) while looking up package '{packageEntity.packageName}'. Verify the package name exists on your target ecosystem.",
                        statusCode: response.StatusCode
                    );
                }

                content = await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(
                    ex,
                    "Network connectivity issue while retrieving version data for {Package}",
                    packageEntity.packageName
                );
                throw new PackageVersionsFetchException(
                    packageEntity.packageName,
                    packageEntity.server,
                    $"Unable to establish a connection to the {packageEntity.server} registry server. Please check network connectivity and registry URLs.",
                    ex
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error fetching versions for {Package}",
                    packageEntity.packageName
                );
                throw new PackageVersionsFetchException(
                    packageEntity.packageName,
                    packageEntity.server,
                    $"An unexpected system error occurred while downloading metadata for package '{packageEntity.packageName}'.",
                    ex
                );
            }

            // Parse and safely extract available tags
            var versions = RegistryVersionsParser
                .ParseVersions(packageEntity.server, content)
                ?.ToList();

            if (versions == null || !versions.Any())
            {
                _logger.LogWarning(
                    "Registry returned data but no parseable versions were found for {Package}",
                    packageEntity.packageName
                );

                // If there are absolutely no versions available on the registry yet,
                // you might want to throw an exception, or pass an initial baseline version seed like "0.0.0"
                // depending on your business rules.
                throw new InvalidOperationException(
                    $"No valid historical version tags were found in the registry manifest for package '{packageEntity.packageName}'. Cannot calculate step."
                );
            }

            // Hand off calculation to your domain rule engine
            // Any custom exceptions thrown from VersionBumper (e.g., ReverseChannelTransitionException)
            // will naturally bubble up directly to the client with their clear messages intact.
            var next = VersionBumper.CalculateNextVersion(versions, packageEntity.channel, _logger);

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

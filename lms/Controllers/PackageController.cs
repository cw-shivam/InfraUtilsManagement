using lms.Dtos;
using lms.Interfaces;
using lms.Mappings;
using Microsoft.AspNetCore.Mvc;

namespace lms.Controllers
{
    [ApiController]
    [Route("lms/api/v1/package")]
    public class PackageController : ControllerBase
    {
        private readonly IPackageService _packageService;
        private readonly ILogger<PackageController> _logger;
        private readonly PackageMapper _mapper;

        public PackageController(
            IPackageService service,
            ILogger<PackageController> logger,
            PackageMapper mapper
        )
        {
            _packageService = service;
            _logger = logger;
            _mapper = mapper;
        }

        [HttpPost("calculate_next_tag")]
        public Task<string> CalculateNextTag([FromBody] PackageDto packageDto)
        {
            _logger.LogInformation(
                "Calculating next tag for package {PackageName} on {Server} ({Channel})",
                packageDto.packageName,
                packageDto.server,
                packageDto.channel
            );

            var entity = _mapper.Map(packageDto);
            return _packageService.CalculateNextTag(entity);
        }
    }
}

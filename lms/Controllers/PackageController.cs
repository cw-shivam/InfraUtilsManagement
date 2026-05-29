using lms.Dtos;
using lms.Interfaces;
using lms.Mappings;
using lms.Utility;
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
        public async Task<IActionResult> CalculateNextTag([FromBody] PackageDto packageDto)
        {
            if (packageDto == null)
            {
                return BadRequest(new { message = "Request body payload cannot be null." });
            }

            _logger.LogInformation(
                "Calculating next tag for package {PackageName} on {Server} ({Channel})",
                packageDto.packageName,
                packageDto.server,
                packageDto.channel
            );

            try
            {
                var entity = _mapper.Map(packageDto);
                var nextTag = await _packageService.CalculateNextTag(entity);

                return Ok(new { nextTag });
            }
            // 400 Bad Request: Input data parameters are missing or fundamentally flawed
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            // 400 Bad Request: The target tag string layout was completely unrecognizable
            catch (InvalidPreReleaseFormatException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            // 422 Unprocessable Entity: Valid formatting syntax, but the command requested is not allowed
            catch (InvalidReleaseTypeException ex)
            {
                return UnprocessableEntity(new { message = ex.Message });
            }
            // 409 Conflict: Trying to move backwards against semantic order rules (e.g., rc -> beta)
            catch (ReverseChannelTransitionException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            // 404 Not Found OR Dependency Issue: Handled dynamically based on remote response
            catch (PackageVersionsFetchException ex)
            {
                // If the remote server explicitly told us the package was missing, return 404.
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound(new { message = ex.Message });
                }

                // For registry connectivity faults or other 5xx issues on their side, return a Bad Gateway status
                return StatusCode(statusCode: 502, value: new { message = ex.Message });
            }
            // 400/404 Alternate Fallback: Version state missing in repository histories
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            // 500 Internal Server Error: Global catch-all shield for completely unexpected bugs
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An unhandled exception slipped through while calculating tag for {Package}",
                    packageDto.packageName
                );
                return StatusCode(
                    statusCode: 500,
                    value: new
                    {
                        message = "An internal server error occurred while finalizing your version calculation step.",
                    }
                );
            }
        }
    }
}

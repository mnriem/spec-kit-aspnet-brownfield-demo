using Carrotware.CMS.HeadlessApi.Models.Dto;
using Carrotware.CMS.HeadlessApi.Models.Request;
using Carrotware.CMS.HeadlessApi.Models.Response;
using Carrotware.CMS.HeadlessApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carrotware.CMS.HeadlessApi.Controllers {

	[Authorize]
	[ApiController]
	[Route("api/headless/navigation")]
	public class NavigationController : ControllerBase {

		private readonly IContentQueryService _contentQueryService;
		private readonly ILogger<NavigationController> _logger;

		public NavigationController(IContentQueryService contentQueryService, ILogger<NavigationController> logger) {
			_contentQueryService = contentQueryService;
			_logger = logger;
		}

		[HttpGet]
		public async Task<IActionResult> GetNavigation(
			[FromQuery] NavigationQueryParams queryParams,
			CancellationToken ct) {

			if (!ModelState.IsValid) {
				_logger.LogDebug("Navigation request rejected: model validation failed");
				return ValidationProblem(ModelState);
			}

			try {
				// Resolve siteId
				var siteId = queryParams.SiteId;
				if (siteId == null) {
					siteId = await _contentQueryService.GetDefaultSiteIdAsync(ct);
					if (siteId == null) {
						_logger.LogDebug("Navigation request rejected: multi-site install requires siteId");
						return Problem(
							detail: "siteId is required for multi-site installations",
							statusCode: StatusCodes.Status400BadRequest,
							title: "Bad Request");
					}
				}

				var siteExists = await _contentQueryService.SiteExistsAsync(siteId.Value, ct);
				if (!siteExists) {
					_logger.LogInformation("Navigation request: site not found {SiteId}", siteId);
					return Problem(
						detail: $"No site found with id '{siteId}'",
						statusCode: StatusCodes.Status404NotFound,
						title: "Not Found");
				}

				var tree = await _contentQueryService.GetNavigationAsync(siteId.Value, ct);

				return Ok(new ApiResponse<List<NavigationNodeDto>> {
					Data = tree,
					Meta = new ApiMeta(),
				});
			} catch (UnauthorizedAccessException ex) {
				_logger.LogInformation("Navigation request forbidden: {Message}", ex.Message);
				return Problem(
					detail: ex.Message,
					statusCode: StatusCodes.Status403Forbidden,
					title: "Forbidden");
			}
		}
	}
}

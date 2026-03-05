using Carrotware.CMS.HeadlessApi.Models.Dto;
using Carrotware.CMS.HeadlessApi.Models.Request;
using Carrotware.CMS.HeadlessApi.Models.Response;
using Carrotware.CMS.HeadlessApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carrotware.CMS.HeadlessApi.Controllers {

	[Authorize]
	[ApiController]
	[Route("api/headless/widgetzones")]
	public class WidgetZonesController : ControllerBase {

		private readonly IContentQueryService _contentQueryService;
		private readonly ILogger<WidgetZonesController> _logger;

		public WidgetZonesController(IContentQueryService contentQueryService, ILogger<WidgetZonesController> logger) {
			_contentQueryService = contentQueryService;
			_logger = logger;
		}

		[HttpGet]
		public async Task<IActionResult> GetWidgetZone(
			[FromQuery] WidgetZoneQueryParams queryParams,
			CancellationToken ct) {

			if (!ModelState.IsValid) {
				_logger.LogDebug("WidgetZones request rejected: model validation failed (pageSlug and zone are required)");
				return ValidationProblem(ModelState);
			}

			try {
				// Resolve siteId
				var siteId = queryParams.SiteId;
				if (siteId == null) {
					siteId = await _contentQueryService.GetDefaultSiteIdAsync(ct);
					if (siteId == null) {
						_logger.LogDebug("WidgetZones request rejected: multi-site install requires siteId");
						return Problem(
							detail: "siteId is required for multi-site installations",
							statusCode: StatusCodes.Status400BadRequest,
							title: "Bad Request");
					}
				}

				var siteExists = await _contentQueryService.SiteExistsAsync(siteId.Value, ct);
				if (!siteExists) {
					_logger.LogInformation("WidgetZones request: site not found {SiteId}", siteId);
					return Problem(
						detail: $"No site found with id '{siteId}'",
						statusCode: StatusCodes.Status404NotFound,
						title: "Not Found");
				}

				var widgets = await _contentQueryService.GetWidgetZoneAsync(
					siteId.Value, queryParams.PageSlug, queryParams.Zone, ct);

				if (!widgets.Any()) {
					_logger.LogInformation(
						"WidgetZones request: no active widgets found for page {PageSlug} zone {Zone}",
						queryParams.PageSlug, queryParams.Zone);
					return Problem(
						detail: $"No active widgets found for page '{queryParams.PageSlug}' zone '{queryParams.Zone}'",
						statusCode: StatusCodes.Status404NotFound,
						title: "Not Found");
				}

				return Ok(new ApiResponse<List<WidgetInstanceDto>> {
					Data = widgets,
					Meta = new ApiMeta(),
				});
			} catch (UnauthorizedAccessException ex) {
				_logger.LogInformation("WidgetZones request forbidden: {Message}", ex.Message);
				return Problem(
					detail: ex.Message,
					statusCode: StatusCodes.Status403Forbidden,
					title: "Forbidden");
			}
		}
	}
}

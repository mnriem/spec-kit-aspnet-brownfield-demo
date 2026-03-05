using Carrotware.CMS.HeadlessApi.Models.Dto;
using Carrotware.CMS.HeadlessApi.Models.Request;
using Carrotware.CMS.HeadlessApi.Models.Response;
using Carrotware.CMS.HeadlessApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carrotware.CMS.HeadlessApi.Controllers {

	[Authorize]
	[ApiController]
	[Route("api/headless/pages")]
	public class PagesController : ControllerBase {

		private readonly IContentQueryService _contentQueryService;
		private readonly ILogger<PagesController> _logger;

		public PagesController(IContentQueryService contentQueryService, ILogger<PagesController> logger) {
			_contentQueryService = contentQueryService;
			_logger = logger;
		}

		[HttpGet]
		public async Task<IActionResult> GetPages(
			[FromQuery] PageQueryParams queryParams,
			CancellationToken ct) {

			if (!ModelState.IsValid) {
				_logger.LogDebug("Pages request rejected: model validation failed");
				return ValidationProblem(ModelState);
			}

			try {
				// Resolve siteId
				var siteId = queryParams.SiteId;
				if (siteId == null) {
					siteId = await _contentQueryService.GetDefaultSiteIdAsync(ct);
					if (siteId == null) {
						_logger.LogDebug("Pages request rejected: multi-site install requires siteId");
						return Problem(
							detail: "siteId is required for multi-site installations",
							statusCode: StatusCodes.Status400BadRequest,
							title: "Bad Request");
					}
				}

				queryParams.SiteId = siteId;

				var siteExists = await _contentQueryService.SiteExistsAsync(siteId.Value, ct);
				if (!siteExists) {
					_logger.LogInformation("Pages request: site not found {SiteId}", siteId);
					return Problem(
						detail: $"No site found with id '{siteId}'",
						statusCode: StatusCodes.Status404NotFound,
						title: "Not Found");
				}

				// Single page by slug
				if (!string.IsNullOrEmpty(queryParams.Slug)) {
					var page = await _contentQueryService.GetPageBySlugAsync(siteId.Value, queryParams.Slug, ct);
					if (page == null) {
						_logger.LogInformation("Pages request: page not found for slug {Slug}", queryParams.Slug);
						return Problem(
							detail: $"No published page found with slug '{queryParams.Slug}'",
							statusCode: StatusCodes.Status404NotFound,
							title: "Not Found");
					}
					return Ok(new ApiResponse<PageDto> {
						Data = page,
						Meta = new ApiMeta(),
					});
				}

				// Paged list
				var (items, total) = await _contentQueryService.GetPagesAsync(queryParams, ct);
				int totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / queryParams.PageSize);

				return Ok(new PagedApiResponse<PageSummaryDto> {
					Data = items,
					Meta = new PagedApiMeta {
						Page = queryParams.Page,
						PageSize = queryParams.PageSize,
						Total = total,
						TotalPages = totalPages,
					},
				});
			} catch (UnauthorizedAccessException ex) {
				_logger.LogInformation("Pages request forbidden: {Message}", ex.Message);
				return Problem(
					detail: ex.Message,
					statusCode: StatusCodes.Status403Forbidden,
					title: "Forbidden");
			}
		}
	}
}

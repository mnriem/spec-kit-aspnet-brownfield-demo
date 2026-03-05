using Carrotware.CMS.HeadlessApi.Models.Dto;
using Carrotware.CMS.HeadlessApi.Models.Request;
using Carrotware.CMS.HeadlessApi.Models.Response;
using Carrotware.CMS.HeadlessApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carrotware.CMS.HeadlessApi.Controllers {

	[Authorize]
	[ApiController]
	[Route("api/headless/posts")]
	public class PostsController : ControllerBase {

		private readonly IContentQueryService _contentQueryService;
		private readonly ILogger<PostsController> _logger;

		public PostsController(IContentQueryService contentQueryService, ILogger<PostsController> logger) {
			_contentQueryService = contentQueryService;
			_logger = logger;
		}

		[HttpGet]
		public async Task<IActionResult> GetPosts(
			[FromQuery] PostQueryParams queryParams,
			CancellationToken ct) {

			if (!ModelState.IsValid) {
				_logger.LogDebug("Posts request rejected: model validation failed");
				return ValidationProblem(ModelState);
			}

			try {
				// Validate date parameters before resolving siteId to return early on bad input
				DateTime? dateFrom = null;
				DateTime? dateTo = null;

				if (!string.IsNullOrEmpty(queryParams.DateFrom)) {
					if (!DateTime.TryParse(queryParams.DateFrom, out var df)) {
						_logger.LogDebug("Posts request rejected: invalid dateFrom format");
						return Problem(
							detail: $"Invalid date format for dateFrom: '{queryParams.DateFrom}'. Use ISO 8601.",
							statusCode: StatusCodes.Status400BadRequest,
							title: "Bad Request");
					}
					dateFrom = df;
				}

				if (!string.IsNullOrEmpty(queryParams.DateTo)) {
					if (!DateTime.TryParse(queryParams.DateTo, out var dt)) {
						_logger.LogDebug("Posts request rejected: invalid dateTo format");
						return Problem(
							detail: $"Invalid date format for dateTo: '{queryParams.DateTo}'. Use ISO 8601.",
							statusCode: StatusCodes.Status400BadRequest,
							title: "Bad Request");
					}
					dateTo = dt;
				}

				if (dateFrom.HasValue && dateTo.HasValue && dateTo.Value < dateFrom.Value) {
					_logger.LogDebug("Posts request rejected: dateTo is before dateFrom");
					return Problem(
						detail: "dateTo must be on or after dateFrom",
						statusCode: StatusCodes.Status400BadRequest,
						title: "Bad Request");
				}

				// Resolve siteId
				var siteId = queryParams.SiteId;
				if (siteId == null) {
					siteId = await _contentQueryService.GetDefaultSiteIdAsync(ct);
					if (siteId == null) {
						_logger.LogDebug("Posts request rejected: multi-site install requires siteId");
						return Problem(
							detail: "siteId is required for multi-site installations",
							statusCode: StatusCodes.Status400BadRequest,
							title: "Bad Request");
					}
				}

				queryParams.SiteId = siteId;

				var siteExists = await _contentQueryService.SiteExistsAsync(siteId.Value, ct);
				if (!siteExists) {
					_logger.LogInformation("Posts request: site not found {SiteId}", siteId);
					return Problem(
						detail: $"No site found with id '{siteId}'",
						statusCode: StatusCodes.Status404NotFound,
						title: "Not Found");
				}

				// Single post by slug
				if (!string.IsNullOrEmpty(queryParams.Slug)) {
					var post = await _contentQueryService.GetPostBySlugAsync(siteId.Value, queryParams.Slug, ct);
					if (post == null) {
						_logger.LogInformation("Posts request: post not found for slug {Slug}", queryParams.Slug);
						return Problem(
							detail: $"No published post found with slug '{queryParams.Slug}'",
							statusCode: StatusCodes.Status404NotFound,
							title: "Not Found");
					}
					return Ok(new ApiResponse<PostDto> {
						Data = post,
						Meta = new ApiMeta(),
					});
				}

				// Paged list
				var (items, total) = await _contentQueryService.GetPostsAsync(queryParams, ct);
				int totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / queryParams.PageSize);

				return Ok(new PagedApiResponse<PostSummaryDto> {
					Data = items,
					Meta = new PagedApiMeta {
						Page = queryParams.Page,
						PageSize = queryParams.PageSize,
						Total = total,
						TotalPages = totalPages,
					},
				});
			} catch (UnauthorizedAccessException ex) {
				_logger.LogInformation("Posts request forbidden: {Message}", ex.Message);
				return Problem(
					detail: ex.Message,
					statusCode: StatusCodes.Status403Forbidden,
					title: "Forbidden");
			}
		}
	}
}

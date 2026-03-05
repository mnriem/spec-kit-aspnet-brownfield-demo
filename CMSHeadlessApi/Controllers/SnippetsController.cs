using Carrotware.CMS.HeadlessApi.Models.Dto;
using Carrotware.CMS.HeadlessApi.Models.Request;
using Carrotware.CMS.HeadlessApi.Models.Response;
using Carrotware.CMS.HeadlessApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carrotware.CMS.HeadlessApi.Controllers {

	[Authorize]
	[ApiController]
	[Route("api/headless/snippets")]
	public class SnippetsController : ControllerBase {

		private readonly IContentQueryService _contentQueryService;
		private readonly ILogger<SnippetsController> _logger;

		public SnippetsController(IContentQueryService contentQueryService, ILogger<SnippetsController> logger) {
			_contentQueryService = contentQueryService;
			_logger = logger;
		}

		[HttpGet]
		public async Task<IActionResult> GetSnippet(
			[FromQuery] SnippetQueryParams queryParams,
			CancellationToken ct) {

			if (!ModelState.IsValid) {
				_logger.LogDebug("Snippets request rejected: model validation failed (name is required)");
				return ValidationProblem(ModelState);
			}

			try {
				// Resolve siteId
				var siteId = queryParams.SiteId;
				if (siteId == null) {
					siteId = await _contentQueryService.GetDefaultSiteIdAsync(ct);
					if (siteId == null) {
						_logger.LogDebug("Snippets request rejected: multi-site install requires siteId");
						return Problem(
							detail: "siteId is required for multi-site installations",
							statusCode: StatusCodes.Status400BadRequest,
							title: "Bad Request");
					}
				}

				var siteExists = await _contentQueryService.SiteExistsAsync(siteId.Value, ct);
				if (!siteExists) {
					_logger.LogInformation("Snippets request: site not found {SiteId}", siteId);
					return Problem(
						detail: $"No site found with id '{siteId}'",
						statusCode: StatusCodes.Status404NotFound,
						title: "Not Found");
				}

				var snippet = await _contentQueryService.GetSnippetByNameAsync(siteId.Value, queryParams.Name, ct);
				if (snippet == null) {
					_logger.LogInformation("Snippets request: snippet not found {Name}", queryParams.Name);
					return Problem(
						detail: $"No active snippet found with name '{queryParams.Name}'",
						statusCode: StatusCodes.Status404NotFound,
						title: "Not Found");
				}

				return Ok(new ApiResponse<SnippetDto> {
					Data = snippet,
					Meta = new ApiMeta(),
				});
			} catch (UnauthorizedAccessException ex) {
				_logger.LogInformation("Snippets request forbidden: {Message}", ex.Message);
				return Problem(
					detail: ex.Message,
					statusCode: StatusCodes.Status403Forbidden,
					title: "Forbidden");
			}
		}
	}
}

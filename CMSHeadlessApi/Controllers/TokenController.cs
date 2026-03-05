using Carrotware.CMS.HeadlessApi.Models.Request;
using Carrotware.CMS.HeadlessApi.Models.Response;
using Carrotware.CMS.HeadlessApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carrotware.CMS.HeadlessApi.Controllers {

	[ApiController]
	[Route("api/headless/token")]
	public class TokenController : ControllerBase {

		private readonly ITokenService _tokenService;
		private readonly ILogger<TokenController> _logger;

		public TokenController(ITokenService tokenService, ILogger<TokenController> logger) {
			_tokenService = tokenService;
			_logger = logger;
		}

		// AllowAnonymous: this is the credential-exchange endpoint itself; no existing
		// token is available at this stage. Transport-layer security (HTTPS) is the guard.
		[AllowAnonymous]
		[HttpPost]
		public async Task<IActionResult> IssueToken(
			[FromBody] TokenRequest request,
			CancellationToken ct) {

			if (!ModelState.IsValid) {
				_logger.LogDebug("Token request rejected: model validation failed for ClientId={ClientId}", request?.ClientId);
				return ValidationProblem(ModelState);
			}

			var result = await _tokenService.IssueTokenAsync(request, ct);
			if (result == null) {
				_logger.LogDebug("Token request rejected: invalid credentials for ClientId={ClientId}", request.ClientId);
				return Problem(
					detail: "Invalid credentials",
					statusCode: StatusCodes.Status401Unauthorized,
					title: "Unauthorized");
			}

			return Ok(new ApiResponse<Models.Response.TokenResponse> {
				Data = result,
				Meta = new ApiMeta(),
			});
		}
	}
}

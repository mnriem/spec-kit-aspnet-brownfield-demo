using Carrotware.CMS.Data.Models;
using Carrotware.CMS.HeadlessApi.Models.Request;
using Carrotware.CMS.HeadlessApi.Models.Response;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Carrotware.CMS.HeadlessApi.Services {

	public class TokenService : ITokenService {

		private readonly CarrotCakeContext _db;
		private readonly IConfiguration _config;

		public TokenService(CarrotCakeContext db, IConfiguration config) {
			_db = db;
			_config = config;
		}

		// TODO(TEST): unit tests for IssueTokenAsync covering valid client, inactive client, expired client, wrong secret
		public async Task<TokenResponse?> IssueTokenAsync(TokenRequest request, CancellationToken ct) {
			var client = await _db.CarrotApiClients
				.AsNoTracking()
				.FirstOrDefaultAsync(c => c.ClientId == request.ClientId, ct);

			if (client == null || !client.IsActive) {
				return null;
			}

			if (client.ExpiresDateUtc.HasValue && client.ExpiresDateUtc.Value <= DateTime.UtcNow) {
				return null;
			}

			var hasher = new PasswordHasher<CarrotApiClient>();
			var result = hasher.VerifyHashedPassword(client, client.ClientSecretHash, request.ClientSecret);
			if (result == PasswordVerificationResult.Failed) {
				return null;
			}

			var jwtKey = Environment.GetEnvironmentVariable("CARROT_HEADLESS_JWT_KEY")
				?? _config["HeadlessApi:JwtKey"]
				?? throw new InvalidOperationException("JWT key is not configured.");

			byte[] keyBytes;
			try {
				keyBytes = Convert.FromBase64String(jwtKey);
			} catch (FormatException) {
				// key may be a plain string in dev; use UTF8 bytes
				keyBytes = Encoding.UTF8.GetBytes(jwtKey);
			}

			var signingKey = new SymmetricSecurityKey(keyBytes);
			var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

			var issuer = _config["HeadlessApi:Issuer"] ?? "CarrotCakeCMS";
			var audience = _config["HeadlessApi:Audience"] ?? "CarrotCakeHeadlessConsumers";
			int expiryMinutes = _config.GetValue("HeadlessApi:TokenExpiryMinutes", 60);
			var now = DateTime.UtcNow;
			var expires = now.AddMinutes(expiryMinutes);

			var siteScope = client.ScopeToSiteId.HasValue
				? client.ScopeToSiteId.Value.ToString()
				: "*";

			var claims = new List<Claim> {
				new Claim(JwtRegisteredClaimNames.Sub, client.ClientId),
				new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
				new Claim(JwtRegisteredClaimNames.Iat,
					new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
					ClaimValueTypes.Integer64),
				new Claim("site_scope", siteScope),
			};

			var token = new JwtSecurityToken(
				issuer: issuer,
				audience: audience,
				claims: claims,
				notBefore: now,
				expires: expires,
				signingCredentials: credentials
			);

			var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

			return new TokenResponse {
				Token = tokenString,
				TokenType = "Bearer",
				ExpiresInSeconds = expiryMinutes * 60,
				ExpiresAt = expires,
			};
		}
	}
}

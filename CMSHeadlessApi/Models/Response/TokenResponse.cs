namespace Carrotware.CMS.HeadlessApi.Models.Response {

	public class TokenResponse {
		public string Token { get; set; } = null!;
		public string TokenType { get; set; } = "Bearer";
		public int ExpiresInSeconds { get; set; }
		public DateTime ExpiresAt { get; set; }
	}
}

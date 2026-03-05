using System.ComponentModel.DataAnnotations;

namespace Carrotware.CMS.HeadlessApi.Models.Request {

	public class TokenRequest {
		[Required]
		[StringLength(128, MinimumLength = 3)]
		public string ClientId { get; set; } = null!;

		[Required]
		[StringLength(512, MinimumLength = 8)]
		public string ClientSecret { get; set; } = null!;
	}
}

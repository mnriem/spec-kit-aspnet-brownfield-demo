using System.ComponentModel.DataAnnotations;

namespace Carrotware.CMS.HeadlessApi.Models.Request {

	public class WidgetZoneQueryParams {
		public Guid? SiteId { get; set; }

		[Required]
		public string PageSlug { get; set; } = null!;

		[Required]
		public string Zone { get; set; } = null!;
	}
}

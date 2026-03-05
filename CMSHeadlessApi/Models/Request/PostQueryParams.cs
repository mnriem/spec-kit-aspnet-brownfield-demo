using System.ComponentModel.DataAnnotations;

namespace Carrotware.CMS.HeadlessApi.Models.Request {

	public class PostQueryParams {
		public Guid? SiteId { get; set; }
		public string? Slug { get; set; }
		public string? Category { get; set; }
		public string? Tag { get; set; }
		public string? DateFrom { get; set; }
		public string? DateTo { get; set; }

		[Range(1, int.MaxValue)]
		public int Page { get; set; } = 1;

		[Range(1, 100)]
		public int PageSize { get; set; } = 20;
	}
}

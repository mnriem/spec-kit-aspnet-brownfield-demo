using System.ComponentModel.DataAnnotations;

namespace Carrotware.CMS.HeadlessApi.Models.Request {

	public class PageQueryParams {
		public Guid? SiteId { get; set; }
		public string? Slug { get; set; }

		[Range(1, int.MaxValue)]
		public int Page { get; set; } = 1;

		[Range(1, 100)]
		public int PageSize { get; set; } = 20;
	}
}

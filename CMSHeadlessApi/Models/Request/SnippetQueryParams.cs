using System.ComponentModel.DataAnnotations;

namespace Carrotware.CMS.HeadlessApi.Models.Request {

	public class SnippetQueryParams {
		public Guid? SiteId { get; set; }

		[Required]
		public string Name { get; set; } = null!;
	}
}

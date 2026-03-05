namespace Carrotware.CMS.HeadlessApi.Models.Dto {

	public class PageSummaryDto {
		public Guid RootContentId { get; set; }
		public Guid SiteId { get; set; }
		public string Slug { get; set; } = null!;
		public string? Title { get; set; }
		public string? NavTitle { get; set; }
		public string? MetaDescription { get; set; }
		public DateTime PublishDate { get; set; }
		public string? Thumbnail { get; set; }
		public bool ShowInSiteNav { get; set; }
		public bool ShowInSiteMap { get; set; }
	}
}

namespace Carrotware.CMS.HeadlessApi.Models.Dto {

	public class PostSummaryDto {
		public Guid RootContentId { get; set; }
		public Guid SiteId { get; set; }
		public string Slug { get; set; } = null!;
		public string? Title { get; set; }
		public string Excerpt { get; set; } = string.Empty;
		public DateTime PublishDate { get; set; }
		public string? Thumbnail { get; set; }
		public List<CategoryDto> Categories { get; set; } = new List<CategoryDto>();
		public List<TagDto> Tags { get; set; } = new List<TagDto>();
	}
}

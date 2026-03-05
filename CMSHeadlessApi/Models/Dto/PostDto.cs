namespace Carrotware.CMS.HeadlessApi.Models.Dto {

	public class PostDto {
		public Guid RootContentId { get; set; }
		public Guid SiteId { get; set; }
		public string Slug { get; set; } = null!;
		public string? Title { get; set; }
		public string? NavTitle { get; set; }
		public string? PageHeading { get; set; }
		public string? Body { get; set; }
		public string? LeftColumnBody { get; set; }
		public string? RightColumnBody { get; set; }
		public string? MetaKeywords { get; set; }
		public string? MetaDescription { get; set; }
		public DateTime PublishDate { get; set; }
		public DateTime RetireDate { get; set; }
		public DateTime CreateDate { get; set; }
		public DateTime EditDate { get; set; }
		public bool IsActive { get; set; }
		public bool ShowInSiteNav { get; set; }
		public bool ShowInSiteMap { get; set; }
		public string? Thumbnail { get; set; }
		public string ContentType { get; set; } = null!;
		public string Excerpt { get; set; } = string.Empty;
		public List<CategoryDto> Categories { get; set; } = new List<CategoryDto>();
		public List<TagDto> Tags { get; set; } = new List<TagDto>();
	}
}

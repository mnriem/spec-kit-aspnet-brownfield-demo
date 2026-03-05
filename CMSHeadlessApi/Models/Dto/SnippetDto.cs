namespace Carrotware.CMS.HeadlessApi.Models.Dto {

	public class SnippetDto {
		public Guid RootContentSnippetId { get; set; }
		public Guid SiteId { get; set; }
		public string Name { get; set; } = null!;
		public string Slug { get; set; } = null!;
		public string Body { get; set; } = null!;
		public bool IsActive { get; set; }
		public DateTime PublishDate { get; set; }
		public DateTime RetireDate { get; set; }
	}
}

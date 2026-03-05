namespace Carrotware.CMS.HeadlessApi.Models.Dto {

	public class NavigationNodeDto {
		public Guid RootContentId { get; set; }
		public Guid? ParentContentId { get; set; }
		public string Title { get; set; } = null!;
		public string Href { get; set; } = null!;
		public int NavOrder { get; set; }
		public List<NavigationNodeDto> Children { get; set; } = new List<NavigationNodeDto>();
	}
}

namespace Carrotware.CMS.HeadlessApi.Models.Dto {

	public class WidgetInstanceDto {
		public Guid RootWidgetId { get; set; }
		public int WidgetOrder { get; set; }
		public string Zone { get; set; } = null!;
		public string ControlPath { get; set; } = null!;
		public string ControlProperties { get; set; } = null!;
		public bool IsActive { get; set; }
	}
}

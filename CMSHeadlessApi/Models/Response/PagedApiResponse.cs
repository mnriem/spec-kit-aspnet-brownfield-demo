namespace Carrotware.CMS.HeadlessApi.Models.Response {

	public class PagedApiResponse<T> {
		public List<T> Data { get; set; } = new List<T>();
		public PagedApiMeta Meta { get; set; } = new PagedApiMeta();
	}

	public class PagedApiMeta {
		public int Page { get; set; }
		public int PageSize { get; set; }
		public int Total { get; set; }
		public int TotalPages { get; set; }
		public Guid RequestId { get; set; } = Guid.NewGuid();
		public DateTime Timestamp { get; set; } = DateTime.UtcNow;
	}
}

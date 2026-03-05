namespace Carrotware.CMS.HeadlessApi.Models.Response {

	public class ApiResponse<T> {
		public T Data { get; set; } = default!;
		public ApiMeta Meta { get; set; } = new ApiMeta();
	}

	public class ApiMeta {
		public Guid RequestId { get; set; } = Guid.NewGuid();
		public DateTime Timestamp { get; set; } = DateTime.UtcNow;
	}
}

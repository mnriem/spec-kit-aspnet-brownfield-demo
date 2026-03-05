using Carrotware.CMS.HeadlessApi.Models.Dto;
using Carrotware.CMS.HeadlessApi.Models.Request;

namespace Carrotware.CMS.HeadlessApi.Services {

	public interface IContentQueryService {
		Task<(List<PageSummaryDto> Items, int Total)> GetPagesAsync(PageQueryParams p, CancellationToken ct);
		Task<PageDto?> GetPageBySlugAsync(Guid siteId, string slug, CancellationToken ct);
		Task<(List<PostSummaryDto> Items, int Total)> GetPostsAsync(PostQueryParams p, CancellationToken ct);
		Task<PostDto?> GetPostBySlugAsync(Guid siteId, string slug, CancellationToken ct);
		Task<List<NavigationNodeDto>> GetNavigationAsync(Guid siteId, CancellationToken ct);
		Task<SnippetDto?> GetSnippetByNameAsync(Guid siteId, string name, CancellationToken ct);
		Task<List<WidgetInstanceDto>> GetWidgetZoneAsync(Guid siteId, string pageSlug, string zone, CancellationToken ct);
		Task<bool> SiteExistsAsync(Guid siteId, CancellationToken ct);
		Task<Guid?> GetDefaultSiteIdAsync(CancellationToken ct);
	}
}

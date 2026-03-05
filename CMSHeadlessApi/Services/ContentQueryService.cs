using Carrotware.CMS.Core;
using Carrotware.CMS.Data.Models;
using Carrotware.CMS.HeadlessApi.Models.Dto;
using Carrotware.CMS.HeadlessApi.Models.Request;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace Carrotware.CMS.HeadlessApi.Services {

	public class ContentQueryService : IContentQueryService {

		private readonly CarrotCakeContext _db;
		private readonly IMemoryCache _cache;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IConfiguration _config;

		private static readonly TimeSpan _siteMetaCacheTtl = TimeSpan.FromMinutes(5);

		public ContentQueryService(
			CarrotCakeContext db,
			IMemoryCache cache,
			IHttpContextAccessor httpContextAccessor,
			IConfiguration config) {
			_db = db;
			_cache = cache;
			_httpContextAccessor = httpContextAccessor;
			_config = config;
		}

		// TODO(TEST): unit test SiteExistsAsync with valid guid, invalid guid, cache hit
		public async Task<bool> SiteExistsAsync(Guid siteId, CancellationToken ct) {
			var cacheKey = $"site_exists_{siteId}";
			if (_cache.TryGetValue(cacheKey, out bool exists)) {
				return exists;
			}
			exists = await _db.CarrotSites
				.AsNoTracking()
				.AnyAsync(s => s.SiteId == siteId, ct);
			_cache.Set(cacheKey, exists, _siteMetaCacheTtl);
			return exists;
		}

		// TODO(TEST): unit test GetDefaultSiteIdAsync for single-site, multi-site
		public async Task<Guid?> GetDefaultSiteIdAsync(CancellationToken ct) {
			const string cacheKey = "default_site_id";
			if (_cache.TryGetValue(cacheKey, out Guid? cachedId)) {
				return cachedId;
			}
			var sites = await _db.CarrotSites.AsNoTracking().Select(s => s.SiteId).ToListAsync(ct);
			Guid? result = sites.Count == 1 ? sites[0] : (Guid?)null;
			_cache.Set(cacheKey, result, _siteMetaCacheTtl);
			return result;
		}

		// Throws UnauthorizedAccessException if the token's site_scope claim does not cover siteId.
		private void CheckSiteScope(Guid siteId) {
			var user = _httpContextAccessor.HttpContext?.User;
			if (user == null) return;
			var siteScopeClaim = user.FindFirstValue("site_scope");
			if (siteScopeClaim != null && siteScopeClaim != "*") {
				if (!Guid.TryParse(siteScopeClaim, out var scopedSiteId)
					|| scopedSiteId != siteId) {
					throw new UnauthorizedAccessException($"Token is not authorized for site {siteId}");
				}
			}
		}

		// TODO(TEST): unit test GetPagesAsync pagination, site scope enforcement, content type filter
		public async Task<(List<PageSummaryDto> Items, int Total)> GetPagesAsync(
			PageQueryParams p, CancellationToken ct) {
			CheckSiteScope(p.SiteId!.Value);

			var contentTypeId = ContentPageType.GetIDByType(ContentPageType.PageType.ContentEntry);
			var now = DateTime.UtcNow;

			var query = _db.vwCarrotContents
				.AsNoTracking()
				.Where(ct2 => ct2.SiteId == p.SiteId!.Value
					&& ct2.IsLatestVersion == true
					&& ct2.PageActive == true
					&& ct2.GoLiveDate < now
					&& ct2.RetireDate > now
					&& ct2.ContentTypeId == contentTypeId)
				.OrderBy(ct2 => ct2.NavOrder).ThenBy(ct2 => ct2.NavMenuText);

			var total = await query.CountAsync(ct);
			var items = await query
				.Skip((p.Page - 1) * p.PageSize)
				.Take(p.PageSize)
				.Select(ct2 => new PageSummaryDto {
					RootContentId = ct2.RootContentId,
					SiteId = ct2.SiteId,
					Slug = ct2.FileName,
					Title = ct2.TitleBar,
					NavTitle = ct2.NavMenuText,
					MetaDescription = ct2.MetaDescription,
					PublishDate = ct2.GoLiveDate,
					Thumbnail = ct2.PageThumbnail,
					ShowInSiteNav = ct2.ShowInSiteNav,
					ShowInSiteMap = ct2.ShowInSiteMap,
				})
				.ToListAsync(ct);

			return (items, total);
		}

		// TODO(TEST): unit test GetPageBySlugAsync for valid slug, nonexistent slug, retired page
		public async Task<PageDto?> GetPageBySlugAsync(Guid siteId, string slug, CancellationToken ct) {
			CheckSiteScope(siteId);

			var contentTypeId = ContentPageType.GetIDByType(ContentPageType.PageType.ContentEntry);
			var now = DateTime.UtcNow;

			var item = await _db.vwCarrotContents
				.AsNoTracking()
				.Where(ct2 => ct2.SiteId == siteId
					&& ct2.IsLatestVersion == true
					&& ct2.PageActive == true
					&& ct2.GoLiveDate < now
					&& ct2.RetireDate > now
					&& ct2.ContentTypeId == contentTypeId
					&& ct2.FileName.ToLower() == slug.ToLower())
				.Select(ct2 => new PageDto {
					RootContentId = ct2.RootContentId,
					SiteId = ct2.SiteId,
					Slug = ct2.FileName,
					Title = ct2.TitleBar,
					NavTitle = ct2.NavMenuText,
					PageHeading = ct2.PageHead,
					Body = ct2.PageText,
					LeftColumnBody = ct2.LeftPageText,
					RightColumnBody = ct2.RightPageText,
					MetaKeywords = ct2.MetaKeyword,
					MetaDescription = ct2.MetaDescription,
					PublishDate = ct2.GoLiveDate,
					RetireDate = ct2.RetireDate,
					CreateDate = ct2.CreateDate,
					EditDate = ct2.EditDate,
					IsActive = ct2.PageActive,
					ShowInSiteNav = ct2.ShowInSiteNav,
					ShowInSiteMap = ct2.ShowInSiteMap,
					Thumbnail = ct2.PageThumbnail,
					ContentType = ct2.ContentTypeValue,
				})
				.FirstOrDefaultAsync(ct);

			return item;
		}

		// TODO(TEST): unit test GetPostsAsync with category filter, tag filter, date range, combined filters
		public async Task<(List<PostSummaryDto> Items, int Total)> GetPostsAsync(
			PostQueryParams p, CancellationToken ct) {
			CheckSiteScope(p.SiteId!.Value);

			var contentTypeId = ContentPageType.GetIDByType(ContentPageType.PageType.BlogEntry);
			var now = DateTime.UtcNow;

			var query = _db.vwCarrotContents
				.AsNoTracking()
				.Where(ct2 => ct2.SiteId == p.SiteId!.Value
					&& ct2.IsLatestVersion == true
					&& ct2.PageActive == true
					&& ct2.GoLiveDate < now
					&& ct2.RetireDate > now
					&& ct2.ContentTypeId == contentTypeId);

			// Category filter — join via vwCarrotCategoryUrls + CarrotCategoryContentMappings
			if (!string.IsNullOrEmpty(p.Category)) {
				var catUrl = p.Category;
				var matchingRootIds = (from c in _db.vwCarrotCategoryUrls
									  join m in _db.CarrotCategoryContentMappings
										on c.ContentCategoryId equals m.ContentCategoryId
									  where c.SiteId == p.SiteId!.Value
										&& c.CategoryUrl == catUrl
									  select m.RootContentId);
				query = query.Where(ct2 => matchingRootIds.Contains(ct2.RootContentId));
			}

			// Tag filter — join via vwCarrotTagUrls + CarrotTagContentMappings
			if (!string.IsNullOrEmpty(p.Tag)) {
				var tagUrl = p.Tag;
				var matchingRootIds = (from t in _db.vwCarrotTagUrls
									  join m in _db.CarrotTagContentMappings
										on t.ContentTagId equals m.ContentTagId
									  where t.SiteId == p.SiteId!.Value
										&& t.TagUrl == tagUrl
									  select m.RootContentId);
				query = query.Where(ct2 => matchingRootIds.Contains(ct2.RootContentId));
			}

			// Date range filters
			if (!string.IsNullOrEmpty(p.DateFrom) && DateTime.TryParse(p.DateFrom, out var dateFrom)) {
				query = query.Where(ct2 => ct2.GoLiveDate >= dateFrom);
			}
			if (!string.IsNullOrEmpty(p.DateTo) && DateTime.TryParse(p.DateTo, out var dateTo)) {
				query = query.Where(ct2 => ct2.GoLiveDate <= dateTo);
			}

			query = query.OrderByDescending(ct2 => ct2.GoLiveDate).ThenBy(ct2 => ct2.TitleBar);

			var total = await query.CountAsync(ct);
			var pageItems = await query
				.Skip((p.Page - 1) * p.PageSize)
				.Take(p.PageSize)
				.Select(ct2 => new {
					ct2.RootContentId,
					ct2.SiteId,
					Slug = ct2.FileName,
					Title = ct2.TitleBar,
					Body = ct2.PageText,
					PublishDate = ct2.GoLiveDate,
					Thumbnail = ct2.PageThumbnail,
				})
				.ToListAsync(ct);

			if (!pageItems.Any()) {
				return (new List<PostSummaryDto>(), total);
			}

			var rootIds = pageItems.Select(x => x.RootContentId).ToList();

			// Batch-fetch categories for all posts in the page — zero N+1
			var catMap = await (from m in _db.CarrotCategoryContentMappings
								join cc in _db.CarrotContentCategories
									on m.ContentCategoryId equals cc.ContentCategoryId
								where rootIds.Contains(m.RootContentId)
								select new { m.RootContentId, cc.CategoryText, cc.CategorySlug })
				.ToListAsync(ct);

			var categoryDict = catMap
				.GroupBy(x => x.RootContentId)
				.ToDictionary(g => g.Key, g => g.Select(x => new CategoryDto {
					Text = x.CategoryText,
					Slug = x.CategorySlug,
				}).ToList());

			// Batch-fetch tags for all posts in the page — zero N+1
			var tagMap = await (from m in _db.CarrotTagContentMappings
								join ct3 in _db.CarrotContentTags
									on m.ContentTagId equals ct3.ContentTagId
								where rootIds.Contains(m.RootContentId)
								select new { m.RootContentId, ct3.TagText, ct3.TagSlug })
				.ToListAsync(ct);

			var tagDict = tagMap
				.GroupBy(x => x.RootContentId)
				.ToDictionary(g => g.Key, g => g.Select(x => new TagDto {
					Text = x.TagText,
					Slug = x.TagSlug,
				}).ToList());

			var dtos = pageItems.Select(x => new PostSummaryDto {
				RootContentId = x.RootContentId,
				SiteId = x.SiteId,
				Slug = x.Slug,
				Title = x.Title,
				Excerpt = x.Body != null && x.Body.Length > 500
					? x.Body.Substring(0, 500)
					: (x.Body ?? string.Empty),
				PublishDate = x.PublishDate,
				Thumbnail = x.Thumbnail,
				Categories = categoryDict.TryGetValue(x.RootContentId, out var cats)
					? cats : new List<CategoryDto>(),
				Tags = tagDict.TryGetValue(x.RootContentId, out var tags)
					? tags : new List<TagDto>(),
			}).ToList();

			return (dtos, total);
		}

		// TODO(TEST): unit test GetPostBySlugAsync for valid, nonexistent, category/tag population
		public async Task<PostDto?> GetPostBySlugAsync(Guid siteId, string slug, CancellationToken ct) {
			CheckSiteScope(siteId);

			var contentTypeId = ContentPageType.GetIDByType(ContentPageType.PageType.BlogEntry);
			var now = DateTime.UtcNow;

			var item = await _db.vwCarrotContents
				.AsNoTracking()
				.Where(ct2 => ct2.SiteId == siteId
					&& ct2.IsLatestVersion == true
					&& ct2.PageActive == true
					&& ct2.GoLiveDate < now
					&& ct2.RetireDate > now
					&& ct2.ContentTypeId == contentTypeId
					&& ct2.FileName.ToLower() == slug.ToLower())
				.Select(ct2 => new {
					ct2.RootContentId,
					ct2.SiteId,
					Slug = ct2.FileName,
					Title = ct2.TitleBar,
					NavTitle = ct2.NavMenuText,
					PageHeading = ct2.PageHead,
					Body = ct2.PageText,
					LeftColumnBody = ct2.LeftPageText,
					RightColumnBody = ct2.RightPageText,
					MetaKeywords = ct2.MetaKeyword,
					MetaDescription = ct2.MetaDescription,
					PublishDate = ct2.GoLiveDate,
					RetireDate = ct2.RetireDate,
					CreateDate = ct2.CreateDate,
					EditDate = ct2.EditDate,
					IsActive = ct2.PageActive,
					ShowInSiteNav = ct2.ShowInSiteNav,
					ShowInSiteMap = ct2.ShowInSiteMap,
					Thumbnail = ct2.PageThumbnail,
					ContentType = ct2.ContentTypeValue,
				})
				.FirstOrDefaultAsync(ct);

			if (item == null) return null;

			// Fetch categories for this post
			var categories = await (from m in _db.CarrotCategoryContentMappings
									join cc in _db.CarrotContentCategories
										on m.ContentCategoryId equals cc.ContentCategoryId
									where m.RootContentId == item.RootContentId
									select new CategoryDto {
										Text = cc.CategoryText,
										Slug = cc.CategorySlug,
									})
				.ToListAsync(ct);

			// Fetch tags for this post
			var tags = await (from m in _db.CarrotTagContentMappings
							  join t in _db.CarrotContentTags
								on m.ContentTagId equals t.ContentTagId
							  where m.RootContentId == item.RootContentId
							  select new TagDto {
								  Text = t.TagText,
								  Slug = t.TagSlug,
							  })
				.ToListAsync(ct);

			return new PostDto {
				RootContentId = item.RootContentId,
				SiteId = item.SiteId,
				Slug = item.Slug,
				Title = item.Title,
				NavTitle = item.NavTitle,
				PageHeading = item.PageHeading,
				Body = item.Body,
				LeftColumnBody = item.LeftColumnBody,
				RightColumnBody = item.RightColumnBody,
				MetaKeywords = item.MetaKeywords,
				MetaDescription = item.MetaDescription,
				PublishDate = item.PublishDate,
				RetireDate = item.RetireDate,
				CreateDate = item.CreateDate,
				EditDate = item.EditDate,
				IsActive = item.IsActive,
				ShowInSiteNav = item.ShowInSiteNav,
				ShowInSiteMap = item.ShowInSiteMap,
				Thumbnail = item.Thumbnail,
				ContentType = item.ContentType,
				Excerpt = item.Body != null && item.Body.Length > 500
					? item.Body.Substring(0, 500)
					: (item.Body ?? string.Empty),
				Categories = categories,
				Tags = tags,
			};
		}

		// TODO(TEST): unit test GetNavigationAsync for tree building, ShowInSiteNav filtering
		public Task<List<NavigationNodeDto>> GetNavigationAsync(Guid siteId, CancellationToken ct) {
			CheckSiteScope(siteId);

			// Use SiteNavHelperReal to get the flat navigation list
			using var navHelper = new SiteNavHelperReal();
			var flatNav = navHelper.GetMasterNavigation(siteId, bActiveOnly: true)
				.Where(n => n.ShowInSiteNav)
				.ToList();

			// Build tree in O(n) single pass
			var nodeMap = new Dictionary<Guid, NavigationNodeDto>();
			foreach (var n in flatNav) {
				nodeMap[n.Root_ContentID] = new NavigationNodeDto {
					RootContentId = n.Root_ContentID,
					ParentContentId = n.Parent_ContentID,
					Title = n.NavMenuText,
					Href = n.FileName,
					NavOrder = n.NavOrder,
				};
			}

			var roots = new List<NavigationNodeDto>();
			foreach (var node in nodeMap.Values) {
				if (node.ParentContentId.HasValue && nodeMap.TryGetValue(node.ParentContentId.Value, out var parent)) {
					parent.Children.Add(node);
				} else {
					roots.Add(node);
				}
			}

			// Sort each level by NavOrder
			SortNavigationChildren(roots);

			return Task.FromResult(roots);
		}

		private static void SortNavigationChildren(List<NavigationNodeDto> nodes) {
			nodes.Sort((a, b) => a.NavOrder.CompareTo(b.NavOrder));
			foreach (var node in nodes) {
				SortNavigationChildren(node.Children);
			}
		}

		// TODO(TEST): unit test GetSnippetByNameAsync for name match, slug match, inactive snippet, not found
		public async Task<SnippetDto?> GetSnippetByNameAsync(Guid siteId, string name, CancellationToken ct) {
			CheckSiteScope(siteId);

			var now = DateTime.UtcNow;
			var nameLower = name.ToLower();

			var item = await _db.vwCarrotContentSnippets
				.AsNoTracking()
				.Where(s => s.SiteId == siteId
					&& s.IsLatestVersion == true
					&& s.ContentSnippetActive == true
					&& s.GoLiveDate < now
					&& s.RetireDate > now
					&& (s.ContentSnippetName.ToLower() == nameLower
						|| s.ContentSnippetSlug.ToLower() == nameLower))
				.Select(s => new SnippetDto {
					RootContentSnippetId = s.RootContentSnippetId,
					SiteId = s.SiteId,
					Name = s.ContentSnippetName,
					Slug = s.ContentSnippetSlug,
					Body = s.ContentBody ?? string.Empty,
					IsActive = s.ContentSnippetActive,
					PublishDate = s.GoLiveDate,
					RetireDate = s.RetireDate,
				})
				.FirstOrDefaultAsync(ct);

			return item;
		}

		// TODO(TEST): unit test GetWidgetZoneAsync for valid page+zone, invalid page, empty zone, returns ordered list
		public async Task<List<WidgetInstanceDto>> GetWidgetZoneAsync(
			Guid siteId, string pageSlug, string zone, CancellationToken ct) {
			CheckSiteScope(siteId);

			var now = DateTime.UtcNow;

			// Resolve RootContentId from page slug (single query)
			var rootContentId = await _db.vwCarrotContents
				.AsNoTracking()
				.Where(c => c.SiteId == siteId
					&& c.IsLatestVersion == true
					&& c.FileName.ToLower() == pageSlug.ToLower())
				.Select(c => (Guid?)c.RootContentId)
				.FirstOrDefaultAsync(ct);

			if (rootContentId == null) {
				return new List<WidgetInstanceDto>();
			}

			var widgets = await _db.vwCarrotWidgets
				.AsNoTracking()
				.Where(w => w.RootContentId == rootContentId.Value
					&& w.PlaceholderName == zone
					&& w.IsLatestVersion == true
					&& w.WidgetActive == true
					&& w.GoLiveDate < now
					&& w.RetireDate > now)
				.OrderBy(w => w.WidgetOrder)
				.Select(w => new WidgetInstanceDto {
					RootWidgetId = w.RootWidgetId,
					WidgetOrder = w.WidgetOrder,
					Zone = w.PlaceholderName,
					ControlPath = w.ControlPath,
					ControlProperties = w.ControlProperties ?? string.Empty,
					IsActive = w.WidgetActive,
				})
				.ToListAsync(ct);

			return widgets;
		}
	}
}

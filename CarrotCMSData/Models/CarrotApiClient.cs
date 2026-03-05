/*
* CarrotCake CMS (MVC Core)
* http://www.carrotware.com/
*
* Copyright 2015, 2023, Samantha Copeland
* Dual licensed under the MIT or GPL Version 3 licenses.
*/

namespace Carrotware.CMS.Data.Models {
	public partial class CarrotApiClient {
		public Guid ApiClientId { get; set; }
		public string ClientId { get; set; } = null!;
		public string ClientSecretHash { get; set; } = null!;
		public Guid? ScopeToSiteId { get; set; }
		public bool IsActive { get; set; }
		public DateTime CreatedDateUtc { get; set; }
		public DateTime? ExpiresDateUtc { get; set; }
		public string? Description { get; set; }

		public virtual CarrotSite? ScopeToSite { get; set; }
	}
}

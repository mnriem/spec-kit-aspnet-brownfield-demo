using Carrotware.CMS.HeadlessApi.Models.Request;
using Carrotware.CMS.HeadlessApi.Models.Response;

namespace Carrotware.CMS.HeadlessApi.Services {

	public interface ITokenService {
		Task<TokenResponse?> IssueTokenAsync(TokenRequest request, CancellationToken ct);
	}
}

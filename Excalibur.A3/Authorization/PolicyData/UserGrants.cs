using Excalibur.A3.Authorization.Grants.Domain.RequestProviders;
using Excalibur.Domain;

namespace Excalibur.A3.Authorization.PolicyData;

/// <summary>
///     Manages grants in the database.
/// </summary>
internal sealed class UserGrants(IDomainDb domainDb, IGrantRequestProvider grantRequestProvider)
{
	/// <summary>
	///     Asynchronously retrieves a dictionary of grants for a specific user.
	/// </summary>
	/// <param name="userId"> The ID of the user. </param>
	/// <returns> A dictionary of grants. </returns>
	public async Task<IDictionary<string, object>> Value(string userId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		return await grantRequestProvider
			.FindUserGrants(userId, CancellationToken.None).ResolveAsync(domainDb.Connection)
			.ConfigureAwait(false);
	}
}

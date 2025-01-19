using Excalibur.A3.Authorization.Grants.Domain.QueryProviders;
using Excalibur.Domain;

namespace Excalibur.A3.Authorization.PolicyData;

/// <summary>
///     Provides functionality for managing activity groups in a domain database.
/// </summary>
/// <param name="domainDb"> The domain database connection. </param>
internal sealed class ActivityGroups(IDomainDb domainDb, IActivityGroupQueryProvider activityGroupQueryProvider)
{
	/// <summary>
	///     Asynchronously retrieves a dictionary of activity groups and their data.
	/// </summary>
	/// <returns> A dictionary of activity group data. </returns>
	public async Task<IDictionary<string, object>> Value()
	{
		var query = activityGroupQueryProvider.FindActivityGroups();
		return await query.Resolve(domainDb.Connection).ConfigureAwait(false);
	}
}

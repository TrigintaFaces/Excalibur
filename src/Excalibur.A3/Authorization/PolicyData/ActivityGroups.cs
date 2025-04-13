using Excalibur.A3.Authorization.Grants.Domain.RequestProviders;
using Excalibur.Domain;

namespace Excalibur.A3.Authorization.PolicyData;

/// <summary>
///     Provides functionality for managing activity groups in a domain database.
/// </summary>
internal sealed class ActivityGroups
{
	private readonly IDomainDb _domainDb;
	private readonly IActivityGroupRequestProvider _activityGroupRequestProvider;

	/// <summary>
	///     Provides functionality for managing activity groups in a domain database.
	/// </summary>
	/// <param name="domainDb"> The domain database connection. </param>
	public ActivityGroups(IDomainDb domainDb, IActivityGroupRequestProvider activityGroupRequestProvider)
	{
		_domainDb = domainDb;
		_activityGroupRequestProvider = activityGroupRequestProvider;
	}

	/// <summary>
	///     Asynchronously retrieves a dictionary of activity groups and their data.
	/// </summary>
	/// <returns> A dictionary of activity group data. </returns>
	public async Task<IDictionary<string, object>> Value() =>
		await _activityGroupRequestProvider.FindActivityGroups().ResolveAsync(_domainDb.Connection).ConfigureAwait(false);
}

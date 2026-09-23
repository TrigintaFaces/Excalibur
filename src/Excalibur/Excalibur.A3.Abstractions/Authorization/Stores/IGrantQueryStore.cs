// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Extended query operations for authorization grants.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Microsoft ASP.NET Core Identity <c>IQueryableUserStore&lt;TUser&gt;</c> pattern:
/// an ISP sub-interface accessed via <see cref="IServiceProvider.GetService(Type)"/>.
/// </para>
/// <para>
/// Replaces <c>IGrantQueryProvider</c> from ISP split.
/// </para>
/// </remarks>
public interface IGrantQueryStore
{
	/// <summary>
	/// Retrieves the grants of ONE tenant that match every supplied filter.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Returns exactly the non-revoked grants whose tenant equals <paramref name="tenantId"/> and, for every
	/// filter that is not <see langword="null"/>, whose field equals that filter under ORDINAL comparison --
	/// case-sensitive, with no wildcards and no collation folding. Expired grants are included; the order is
	/// unspecified.
	/// </para>
	/// <para>
	/// <see langword="null"/>, and only <see langword="null"/>, means "do not filter on this field". An empty
	/// or whitespace filter is refused rather than read as "all", because a value that means "match
	/// everything" is exactly what a caller passes by accident.
	/// </para>
	/// <para>
	/// A single-tenant host stores its grants under the untenanted sentinel and passes that value here.
	/// To read across tenants, call <see cref="GetMatchingGrantsAcrossTenantsAsync"/>, which says so in its name.
	/// </para>
	/// </remarks>
	/// <param name="tenantId">The tenant whose grants are read. Required.</param>
	/// <param name="userId">The user to match, or <see langword="null"/> for every user.</param>
	/// <param name="grantType">The grant type to match, or <see langword="null"/> for every type.</param>
	/// <param name="qualifier">The qualifier to match, or <see langword="null"/> for every qualifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The matching grants.</returns>
	/// <exception cref="ArgumentException">
	/// <paramref name="tenantId"/> is null, empty or whitespace, or a non-null filter is empty or whitespace.
	/// </exception>
	Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(string tenantId, string? userId,
		string? grantType, string? qualifier, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves the grants of EVERY tenant that match every supplied filter.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Deliberately estate-wide, for operator reads such as cross-tenant entitlement reports and orphan
	/// detection. It is a read: nothing that revokes or changes a grant should be driven from its result,
	/// because one call spans every tenant. Tenant-scoped work belongs on
	/// <see cref="GetMatchingGrantsAsync"/>.
	/// </para>
	/// <para>
	/// Filters follow the same rules as <see cref="GetMatchingGrantsAsync"/>: ordinal equality,
	/// <see langword="null"/> means unfiltered, and an empty or whitespace filter is refused.
	/// </para>
	/// <para>
	/// Its cost is the cost of reading across partitions: a partitioned store cannot serve it from one
	/// partition, so on DynamoDB it is a paginated scan and on Cosmos DB a cross-partition query.
	/// </para>
	/// </remarks>
	/// <param name="userId">The user to match, or <see langword="null"/> for every user.</param>
	/// <param name="grantType">The grant type to match, or <see langword="null"/> for every type.</param>
	/// <param name="qualifier">The qualifier to match, or <see langword="null"/> for every qualifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The matching grants, across every tenant.</returns>
	/// <exception cref="ArgumentException">A non-null filter is empty or whitespace.</exception>
	Task<IReadOnlyList<Grant>> GetMatchingGrantsAcrossTenantsAsync(string? userId, string? grantType,
		string? qualifier, CancellationToken cancellationToken);

	/// <summary>
	/// Finds grants keyed by a provider-specific shape.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Provider-specific projection of grants.</returns>
	Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId,
		CancellationToken cancellationToken);
}

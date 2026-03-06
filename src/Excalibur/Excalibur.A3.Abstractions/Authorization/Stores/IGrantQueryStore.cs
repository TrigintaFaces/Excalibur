// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Abstractions.Authorization;

/// <summary>
/// Extended query operations for authorization grants.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Microsoft ASP.NET Core Identity <c>IQueryableUserStore&lt;TUser&gt;</c> pattern:
/// an ISP sub-interface accessed via <see cref="IGrantStore.GetService(Type)"/>.
/// </para>
/// <para>
/// Replaces <c>IGrantQueryProvider</c> from Sprint 551 ISP split.
/// </para>
/// </remarks>
public interface IGrantQueryStore
{
	/// <summary>
	/// Retrieves grants matching the provided filter.
	/// </summary>
	/// <param name="userId">Optional user/subject identifier filter.</param>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier/scope.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Matching grants.</returns>
	Task<IReadOnlyList<Grant>> GetMatchingGrantsAsync(string? userId, string tenantId,
		string grantType, string qualifier, CancellationToken cancellationToken);

	/// <summary>
	/// Finds grants keyed by a provider-specific shape.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Provider-specific projection of grants.</returns>
	Task<IReadOnlyDictionary<string, object>> FindUserGrantsAsync(string userId,
		CancellationToken cancellationToken);
}

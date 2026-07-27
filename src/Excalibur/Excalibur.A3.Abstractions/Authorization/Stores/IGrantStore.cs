// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.A3.Authorization;

/// <summary>
/// Provider-neutral store for authorization grant persistence.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Microsoft ASP.NET Core Identity <c>IUserStore&lt;TUser&gt;</c> pattern:
/// minimal CRUD surface (5 methods) with <see cref="IServiceProvider.GetService(Type)"/> for ISP extensions.
/// </para>
/// <para>
/// Replaces both the internal <c>IGrantRequestProvider</c> (11-method, SQL-coupled) and the
/// abstractions-level <c>IGrantRequestProvider</c> (5 methods + GetService).
/// </para>
/// <para>
/// Optional capabilities — including durability, discovered via <see cref="IDurableGrantStore"/> —
/// are resolved through <see cref="IServiceProvider.GetService(Type)"/>, never by casting the store.
/// A store answers for the capabilities it provides; decorators MUST forward <c>GetService</c> to the
/// wrapped store.
/// </para>
/// </remarks>
public interface IGrantStore : IServiceProvider
{
	/// <summary>
	/// Resolves an optional grant-store capability, or <see langword="null"/> when it is unavailable.
	/// </summary>
	/// <param name="serviceType"> The capability interface to resolve, for example <see cref="IDurableGrantStore"/>. </param>
	/// <returns>
	/// An instance assignable to <paramref name="serviceType"/> when this store provides the capability;
	/// otherwise <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// The default implementation answers for any capability this instance itself implements. Leaf stores
	/// need not override it. Decorators MUST override it to defer unknown capabilities to the store they
	/// wrap; a decorator that does not forward silently disables the capability beneath it.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : null;
	}

	/// <summary>
	/// Retrieves a specific grant.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier/scope.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The grant, or <see langword="null"/> if not found.</returns>
	Task<Grant?> GetGrantAsync(string userId, string tenantId, string grantType,
		string qualifier, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all <b>active (non-expired)</b> grants for a user.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>All non-expired grants for the specified user.</returns>
	/// <remarks>
	/// This overload is <b>default-secure</b>: expired grants are excluded so they can never
	/// influence an authorization or separation-of-duties decision (Microsoft soft-delete-filter
	/// pattern). Callers that legitimately need expired grants (reporting, orphaned-access
	/// detection, reconciliation) MUST use the
	/// <see cref="GetAllGrantsAsync(string, bool, CancellationToken)"/> overload with
	/// <c>includeExpired: true</c>.
	/// </remarks>
	Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves all grants for a user, optionally including expired grants.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="includeExpired">
	/// <see langword="true"/> to include expired grants; <see langword="false"/> to return only
	/// active (non-expired) grants. Authorization and SoD decision paths MUST pass
	/// <see langword="false"/>.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>All grants for the specified user, filtered per <paramref name="includeExpired"/>.</returns>
	Task<IReadOnlyList<Grant>> GetAllGrantsAsync(string userId, bool includeExpired, CancellationToken cancellationToken);

	/// <summary>
	/// Saves or updates a grant (upsert).
	/// </summary>
	/// <param name="grant">The grant to persist.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> SaveGrantAsync(Grant grant, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a grant with optional revocation metadata.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier/scope.</param>
	/// <param name="revokedBy">Optional actor identifier who revoked the grant.</param>
	/// <param name="revokedOn">Optional revocation timestamp (UTC).</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Number of rows affected.</returns>
	Task<int> DeleteGrantAsync(string userId, string tenantId, string grantType,
		string qualifier, string? revokedBy, DateTimeOffset? revokedOn,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks whether a specific grant exists.
	/// </summary>
	/// <param name="userId">The user/subject identifier.</param>
	/// <param name="tenantId">The tenant identifier.</param>
	/// <param name="grantType">The grant type.</param>
	/// <param name="qualifier">The qualifier/scope.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> if the grant exists; otherwise <see langword="false"/>.</returns>
	Task<bool> GrantExistsAsync(string userId, string tenantId, string grantType,
		string qualifier, CancellationToken cancellationToken);

}

/// <summary>
/// Marks an <see cref="IGrantStore"/> whose grant persistence is durable — grants survive a process
/// restart. A store advertises this capability by answering for it from
/// <see cref="IServiceProvider.GetService(Type)"/>; consumers query rather than cast, so the capability
/// is discoverable through decorators.
/// </summary>
public interface IDurableGrantStore
{
}

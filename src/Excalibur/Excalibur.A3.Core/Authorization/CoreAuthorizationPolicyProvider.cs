// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authentication;
using Excalibur.A3.Authorization.PolicyData;
using Excalibur.Dispatch;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Lightweight <see cref="IAuthorizationPolicyProvider"/> for the <c>AddExcaliburA3Core</c> composition:
/// reads grants and activity groups straight from the configured stores on every call, with no
/// distributed-cache layer.
/// </summary>
/// <remarks>
/// The full-stack <c>AddExcaliburA3()</c> composition registers its own provider that adds an
/// <c>IDistributedCache</c> memoization layer -- that layer needs
/// <c>Excalibur.Dispatch.Serialization.DispatchJsonSerializer</c>,
/// which lives in the full <c>Excalibur.Dispatch</c> package, so it cannot live here without pulling that
/// package into the "no full stack" lightweight tier. This type carries every dependency
/// <c>AddExcaliburA3Core()</c> already registers (<see cref="IGrantStore"/>, <see cref="IActivityGroupStore"/>)
/// and nothing else, so the lightweight path can evaluate a grant without it.
/// </remarks>
/// <param name="activityGroups"> A collection of activity groups. </param>
/// <param name="userGrants"> A collection of grants for authorization purposes. </param>
/// <param name="currentUser"> The current authenticated user token. </param>
/// <param name="tenantContext">
/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
/// receives the framework default context and operates as the one canonical tenant.
/// </param>
/// <param name="timeProvider">Clock used to reject expired grants during acquisition.</param>
internal sealed class CoreAuthorizationPolicyProvider(
	ActivityGroups activityGroups,
	UserGrants userGrants,
	IAuthenticationToken currentUser,
	ITenantContext tenantContext,
	TimeProvider? timeProvider = null
) : IAuthorizationPolicyProvider
{
	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

	/// <inheritdoc />
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="IAuthenticationToken.UserId"/> is null or
	/// <see cref="ITenantContext.TenantId"/> is null or empty.
	/// </exception>
	/// <remarks>
	/// Kept <c>async</c> deliberately, even though the body has no <c>await</c> of its own: a validation
	/// failure must reach the caller as a FAULTED TASK, the normal TAP contract for a Task-returning
	/// method, not as a synchronous throw out of the method call itself.
	/// </remarks>
	public async Task<IAuthorizationPolicy> GetPolicyAsync()
	{
		if (currentUser.UserId is null)
		{
			throw new InvalidOperationException("User ID is required for authorization policy.");
		}

		if (string.IsNullOrEmpty(tenantContext.TenantId))
		{
			throw new InvalidOperationException(
				"Tenant ID is required for authorization policy. " +
				"Establish the ambient tenant (TenantContextHolder.BeginScope / tenant middleware) before evaluating authorization.");
		}

		var userId = currentUser.UserId;
		var tenantId = tenantContext.TenantId;
		for (var attempt = 0; attempt < 3; attempt++)
		{
			var revision = await userGrants.RevisionAsync(userId, CancellationToken.None).ConfigureAwait(false);
			IDictionary<string, object> grants;
			if (revision is null)
			{
				grants = await userGrants.ValueAsync(userId, CancellationToken.None).ConfigureAwait(false);
			}
			else
			{
				var snapshot = await userGrants.SnapshotAsync(userId, CancellationToken.None).ConfigureAwait(false)
					?? throw new InvalidOperationException("The grant store did not return an authoritative snapshot.");
				if (!string.Equals(snapshot.UserId, userId, StringComparison.Ordinal))
				{
					throw new InvalidOperationException("The grant snapshot belongs to a different subject.");
				}
				if (snapshot.Revision != revision)
				{
					continue;
				}
				var now = _timeProvider.GetUtcNow();
				grants = snapshot.Grants.Where(g => g.IsActive(now)).ToDictionary(
					g => SegmentedKey.Compose(g.TenantId, g.GrantType, g.Qualifier),
					g => (object)g,
					StringComparer.Ordinal);
			}

			// A relational store can share the scoped connection with the catalogue store.
			// Finish the coherent grant read before issuing the catalogue query.
			var groups = await activityGroups.ValueAsync(tenantId, CancellationToken.None).ConfigureAwait(false);
			if (revision is not null && revision != await userGrants.RevisionAsync(userId, CancellationToken.None).ConfigureAwait(false))
			{
				continue;
			}
			if (!string.Equals(currentUser.UserId, userId, StringComparison.Ordinal)
				|| !string.Equals(tenantContext.TenantId, tenantId, StringComparison.Ordinal))
			{
				throw new InvalidOperationException("Authorization identity changed while acquiring a policy.");
			}
			return new AuthorizationPolicy(grants, groups, tenantContext, userId);
		}
		throw new InvalidOperationException("Authorization grants changed repeatedly while acquiring a policy. Retry authorization.");
	}
}

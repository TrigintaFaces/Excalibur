// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
internal sealed class CoreAuthorizationPolicyProvider(
	ActivityGroups activityGroups,
	UserGrants userGrants,
	IAuthenticationToken currentUser,
	ITenantContext tenantContext
) : IAuthorizationPolicyProvider
{
	/// <summary>
	/// The policy built for this scope, memoized after the first call. This type is registered scoped, so
	/// one instance already lives for exactly one request/dispatch -- caching here is per-request
	/// memoization without introducing any new lifetime or shared state, and it is free: it costs nothing
	/// beyond what the scope already pays for evaluating more than one activity or resource.
	/// </summary>
	private Task<IAuthorizationPolicy>? _cachedPolicy;

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

		return await (_cachedPolicy ??= BuildPolicyAsync(currentUser.UserId)).ConfigureAwait(false);
	}

	private async Task<IAuthorizationPolicy> BuildPolicyAsync(string userId)
	{
		var grantsTask = userGrants.ValueAsync(userId, CancellationToken.None);
		var activityGroupsTask = activityGroups.ValueAsync(CancellationToken.None);

		await Task.WhenAll(grantsTask, activityGroupsTask).ConfigureAwait(false);

		return new AuthorizationPolicy(
			await grantsTask.ConfigureAwait(false),
			await activityGroupsTask.ConfigureAwait(false),
			tenantContext,
			userId);
	}
}

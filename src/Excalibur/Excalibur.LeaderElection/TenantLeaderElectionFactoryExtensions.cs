// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.LeaderElection;

/// <summary>
/// Opt-in tenant-scoping helpers for <see cref="ILeaderElectionFactory"/>.
/// </summary>
/// <remarks>
/// Per-tenant leadership is expressed by qualifying the lease resource name with the
/// resolved tenant identifier (<c>{resourceName}:{tenantId}</c>) — a caller-supplied
/// composition. The core leader-election abstraction stays tenant-agnostic: it never
/// generates an unbounded per-tenant lease keyspace of its own, and consumers that do
/// not use these helpers are unaffected. Each call scopes exactly one tenant (bounded),
/// and fails closed when no tenant is resolved, so a missing tenant can never silently
/// collapse into an unscoped, cross-tenant lease.
/// </remarks>
public static class TenantLeaderElectionFactoryExtensions
{
	/// <summary>
	/// Composes a tenant-qualified lease resource name (<c>{resourceName}:{tenantId}</c>)
	/// from the current ambient tenant, failing closed when no tenant is resolved.
	/// </summary>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="resourceName"> The base resource name to qualify. </param>
	/// <returns> The tenant-qualified resource name. </returns>
	/// <exception cref="System.ArgumentNullException"> <paramref name="tenantContext"/> is <see langword="null"/>. </exception>
	/// <exception cref="System.ArgumentException"> <paramref name="resourceName"/> is <see langword="null"/> or whitespace. </exception>
	/// <exception cref="TenantRequiredException"> No ambient tenant is resolved for the current execution flow. </exception>
	public static string TenantScopedResourceName(this ITenantContext tenantContext, string resourceName)
	{
		System.ArgumentNullException.ThrowIfNull(tenantContext);
		System.ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

		var tenantId = tenantContext.TenantId
			?? throw new TenantRequiredException(
				"Tenant-scoped leader election requires a resolved ambient tenant, but none is present in the current execution flow.");

		return string.Concat(resourceName, ":", tenantId);
	}

	/// <summary>
	/// Creates a leader election whose lease is scoped to the current ambient tenant.
	/// </summary>
	/// <param name="factory"> The leader-election factory. </param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="resourceName"> The base resource to elect a leader for; qualified with the tenant. </param>
	/// <param name="candidateId"> Optional candidate ID (defaults to the instance ID). </param>
	/// <returns> A leader election instance scoped to the current tenant. </returns>
	/// <exception cref="System.ArgumentNullException"> <paramref name="factory"/> or <paramref name="tenantContext"/> is <see langword="null"/>. </exception>
	/// <exception cref="TenantRequiredException"> No ambient tenant is resolved for the current execution flow. </exception>
	public static ILeaderElection CreateTenantScopedElection(
		this ILeaderElectionFactory factory,
		ITenantContext tenantContext,
		string resourceName,
		string? candidateId = null)
	{
		System.ArgumentNullException.ThrowIfNull(factory);
		return factory.CreateElection(tenantContext.TenantScopedResourceName(resourceName), candidateId);
	}

	/// <summary>
	/// Creates a health-based leader election whose lease is scoped to the current ambient tenant.
	/// </summary>
	/// <param name="factory"> The leader-election factory. </param>
	/// <param name="tenantContext">
	/// The ambient tenant context. Required: this store partitions rows by tenant, and it resolves that
	/// partition from here, so there is no state in which the partition is undecided. A single-tenant host
	/// receives the framework default context and operates as the one canonical tenant.
	/// </param>
	/// <param name="resourceName"> The base resource to elect a leader for; qualified with the tenant. </param>
	/// <param name="candidateId"> Optional candidate ID (defaults to the instance ID). </param>
	/// <returns> A health-based leader election instance scoped to the current tenant. </returns>
	/// <exception cref="System.ArgumentNullException"> <paramref name="factory"/> or <paramref name="tenantContext"/> is <see langword="null"/>. </exception>
	/// <exception cref="TenantRequiredException"> No ambient tenant is resolved for the current execution flow. </exception>
	public static IHealthBasedLeaderElection CreateTenantScopedHealthBasedElection(
		this ILeaderElectionFactory factory,
		ITenantContext tenantContext,
		string resourceName,
		string? candidateId = null)
	{
		System.ArgumentNullException.ThrowIfNull(factory);
		return factory.CreateHealthBasedElection(tenantContext.TenantScopedResourceName(resourceName), candidateId);
	}
}

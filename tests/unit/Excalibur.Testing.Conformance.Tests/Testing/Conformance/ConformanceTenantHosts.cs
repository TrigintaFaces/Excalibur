// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Ambient tenant contexts standing in for the host shapes these fixtures used to express by supplying no
/// context at all.
/// </summary>
/// <remarks>
/// <para>
/// The in-memory stores now take <see cref="ITenantContext"/> as a required dependency, so "this fixture
/// registers no tenancy" is no longer expressible by omission and has to be named. The conformance kits
/// still model that host as <see langword="null"/>, and their arms assert the partition such a caller
/// resolves to — so the substitute has to resolve the SAME partition the store folded a missing context
/// onto, or the arms silently move to a partition nothing was written to.
/// </para>
/// <para>
/// These implement <see cref="ITenantContext"/> DIRECTLY, inheriting no first-party base, so arms built on
/// them bind the interface's own requirement rather than re-testing an inherited convenience.
/// </para>
/// </remarks>
internal static class ConformanceTenantHosts
{
	/// <summary>
	/// A host with no tenant established, resolving the framework's reserved untenanted marker.
	/// </summary>
	/// <remarks>
	/// Stores that partition through <c>KeyedTenantPartition.FromContext</c> fold this onto
	/// <c>KeyedTenantPartition.Untenanted</c> — the exact partition they previously folded a missing
	/// context onto — so the partition each arm addresses is unchanged.
	/// </remarks>
	/// <returns>A context resolving the reserved untenanted marker.</returns>
	internal static ITenantContext Untenanted() => new FixedTenantContext(TenantScope.UntenantedSentinel);

	/// <summary>
	/// A host with no tenant established, for arms exercising <c>InMemoryAuditStore</c>'s untenanted
	/// partition specifically.
	/// </summary>
	/// <remarks>
	/// <c>InMemoryAuditStore</c> now partitions through the same reserved marker as every other store —
	/// <c>TenantScope.UntenantedSentinel</c> — so this resolves the identical partition
	/// <see cref="Untenanted"/> does. It previously used a store-private label of its own, which meant a
	/// caller had to name that exact private string to read back events written under no tenant; this
	/// method existed to carry it. Kept as a distinct, named entry point rather than collapsed into
	/// <see cref="Untenanted"/> because a future store-specific divergence should be visible at its own
	/// call sites again, not silently reunified by deleting the seam that would show it.
	/// </remarks>
	/// <returns>A context resolving the reserved untenanted marker.</returns>
	internal static ITenantContext UntenantedAuditHost() => new FixedTenantContext(TenantScope.UntenantedSentinel);

	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
}

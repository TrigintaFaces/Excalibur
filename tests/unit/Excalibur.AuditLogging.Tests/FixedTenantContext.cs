// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.AuditLogging.Tests;

/// <summary>
/// An ambient tenant scope fixed for the lifetime of a store, standing in for the per-request context a
/// host would supply.
/// </summary>
/// <remarks>
/// <para>
/// It takes its tenant in the constructor and has no null state. A sibling implementation elsewhere yields
/// a null tenant until an explicit switch is called, which fails closed on the first read if that call is
/// ever missed -- a present-but-unresolved ambient context throws rather than degrading. Requiring the
/// tenant up front makes that mistake inexpressible here rather than merely absent today.
/// </para>
/// <para>
/// Tests use this instead of setting <c>AuditQuery.TenantId</c>. The stores resolve their partition from
/// the ambient scope and never read that field; a test that supplied it and asserted the result narrowed
/// would only pass if a store trusted a caller-supplied tenant, which is the cross-tenant impersonation
/// defect rather than the contract.
/// </para>
/// </remarks>
internal sealed class FixedTenantContext(string tenantId) : ITenantContext
{
	public string TenantId { get; } = !string.IsNullOrWhiteSpace(tenantId)
		? tenantId
		: throw new ArgumentException("An ambient tenant is required.", nameof(tenantId));

	string? ITenantContext.TenantId => TenantId;

	public bool HasTenant => true;
}

/// <summary>
/// Ambient contexts standing in for the host shapes these tests used to express by omitting the context
/// entirely.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ITenantContext"/> is now a required dependency of the stores, so "no context registered" is
/// no longer expressible. Each construction site therefore has to name the host it always meant, and the
/// two are not interchangeable: they select different partitions.
/// </para>
/// </remarks>
internal static class TestTenantHosts
{
	/// <summary>
	/// The partition an audit event carrying no tenant is written to by
	/// <c>InMemoryAuditStore</c>.
	/// </summary>
	/// <remarks>
	/// The store keys writes off the event's own tenant (<c>auditEvent.TenantId ?? UntenantedPartitionKey</c>)
	/// but keys reads off the ambient scope. A test that stores tenant-less events therefore has to run under
	/// an ambient scope naming that same partition, or its reads resolve a key nothing was ever written to.
	/// This is now <see cref="TenantScope.UntenantedSentinel"/>, the same reserved marker every other store
	/// uses — the store no longer carries a private label of its own for a test to mirror.
	/// </remarks>
	internal static readonly string UntenantedAuditPartition = TenantScope.UntenantedSentinel;

	/// <summary>
	/// A host with no tenancy, operating entirely in the audit store's untenanted partition.
	/// </summary>
	internal static ITenantContext UntenantedAuditHost() => new FixedTenantContext(UntenantedAuditPartition);

	/// <summary>
	/// A single-tenant host, operating as the one canonical tenant the framework names for that shape.
	/// </summary>
	internal static ITenantContext SingleTenantHost() => new FixedTenantContext(TenantDefaults.DefaultTenantId);
}

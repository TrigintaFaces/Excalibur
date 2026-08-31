// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

/// <summary>
/// Builds <see cref="InMemoryAuditStore"/> instances whose reads are confined to a tenant by AMBIENT
/// context, which is the only way the store scopes a read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why every tenant-aware arm needs this.</b> The store partitions <i>writes</i> by
/// <c>auditEvent.TenantId</c> but resolves <i>reads</i> from ambient tenant context, deliberately never
/// consulting <c>AuditQuery.TenantId</c>. A caller must not be able to widen a read by omitting that field,
/// nor redirect it by naming another tenant — both were live cross-tenant disclosures, and both are closed
/// by ignoring the field entirely. So a store constructed without a context reads only the untenanted
/// partition, and an arm that writes tenanted events through one will find nothing.
/// </para>
/// <para>
/// <b>Why this helper is shared rather than copied.</b> Four test classes exercise the same contract. A
/// private fake in each is four places to drift when the contract moves again, and drift in a tenancy
/// fixture does not fail loudly — it produces arms that pass while testing a partition nobody uses.
/// </para>
/// </remarks>
internal static class AuditStoreTenantScope
{
	/// <summary>
	/// Creates a store whose reads are confined to <paramref name="tenantId"/>.
	/// </summary>
	/// <param name="tenantId">The ambient tenant the returned store reads as.</param>
	/// <returns>A store scoped to <paramref name="tenantId"/>.</returns>
	public static InMemoryAuditStore ScopedTo(string tenantId) =>
		new(AuditIntegrityTestStrategy.Create(), new FixedTenantContext(tenantId));

	/// <summary>
	/// The partition the store writes an audit event carrying no tenant to.
	/// </summary>
	/// <remarks>
	/// The store keys writes off <c>auditEvent.TenantId ?? UntenantedPartitionKey</c> and keys reads off the
	/// ambient scope, so a store built for tenant-less events has to read as this exact partition or its
	/// reads resolve a key nothing was written to. This is <see cref="TenantScope.UntenantedSentinel"/>, the
	/// same reserved marker every store uses for "no tenant" — the store no longer carries a private label
	/// of its own. It is deliberately NOT <c>TenantDefaults.DefaultTenantId</c>: that is a real single-tenant
	/// identity and selects a different partition, so substituting it makes tenant-less events unreadable.
	/// </remarks>
	private static readonly string UntenantedPartition = TenantScope.UntenantedSentinel;

	/// <summary>
	/// Creates a store for a host with no tenancy, reading and writing the untenanted partition.
	/// </summary>
	/// <remarks>
	/// This is the shape these arms previously expressed by omitting the tenant context entirely. The
	/// context is now a required dependency, so the untenanted host has to be named rather than implied;
	/// the partition each arm reads is unchanged.
	/// </remarks>
	/// <returns>A store confined to the untenanted partition.</returns>
	public static InMemoryAuditStore Untenanted() => ScopedTo(UntenantedPartition);

	/// <summary>
	/// Implements <see cref="ITenantContext"/> DIRECTLY, inheriting no first-party base, so arms built on it
	/// bind the interface's own requirement rather than re-testing an inherited convenience.
	/// </summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
	}
}

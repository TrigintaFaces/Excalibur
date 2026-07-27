// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.Compliance;

/// <summary>
/// Provides storage and retrieval services for audit events.
/// </summary>
/// <remarks>
/// <para>
/// The audit store is responsible for:
/// - Durable, tamper-evident storage of audit events
/// - Efficient querying for compliance reports
/// - Long-term retention according to policy
/// </para>
/// <para> Implementations may use SQL Server, Postgres, append-only blob storage, or specialized audit platforms (e.g., Splunk, Datadog). </para>
/// <para>
/// This is a composite of the focused <see cref="IAuditWriter"/> (storage) and
/// <see cref="IAuditQuery"/> (retrieval, querying, and integrity verification) contracts.
/// New code should depend on the narrowest interface that meets its needs.
/// </para>
/// <para>
/// Optional capabilities are discovered through <see cref="IServiceProvider.GetService(Type)"/>,
/// never by casting the store. A durable implementation answers for <see cref="IDurableAuditStore"/>;
/// a volatile one answers <see langword="null"/>. Decorators MUST forward <c>GetService</c> to the
/// wrapped store so the capability chain stays transparent.
/// </para>
/// </remarks>
public interface IAuditStore : IAuditWriter, IAuditQuery, IServiceProvider
{
	/// <summary>
	/// Resolves an optional audit-store capability, or <see langword="null"/> when it is unavailable.
	/// </summary>
	/// <param name="serviceType"> The capability interface to resolve, for example <see cref="IDurableAuditStore"/>. </param>
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
}

/// <summary>
/// Marks an <see cref="IAuditStore"/> whose storage is durable — audit events survive a process
/// restart. A store advertises this capability by answering for it from
/// <see cref="IServiceProvider.GetService(Type)"/>; consumers query rather than cast, so the
/// capability is discoverable through decorators.
/// </summary>
public interface IDurableAuditStore
{
}

/// <summary>
/// Exposes destructive purge of stored audit events on an <see cref="IAuditStore"/> that supports it,
/// so a consumer can satisfy a retention or erasure obligation without depending on a concrete store type.
/// </summary>
/// <remarks>
/// <para>
/// This is a <em>capability</em>, not a member of <see cref="IAuditStore"/>, and the distinction is
/// deliberate. A store backed by append-only or write-once media cannot delete, and a mandatory delete
/// member would oblige it to supply one — in practice a throwing stub or, worse, a silent no-op that
/// reports success. Discovery answers honestly instead: a store that cannot purge does not offer the
/// capability, and <see cref="IServiceProvider.GetService(Type)"/> returns <see langword="null"/>.
/// </para>
/// <para>
/// A <see langword="null"/> result MUST fail loudly at the caller. Retention that cannot find this
/// capability has not "completed with nothing to do" — it has failed to run, and reporting it as success
/// is how a retention policy comes to be believed enforced while no data is ever removed.
/// </para>
/// <para>
/// Resolve it, never cast to it, so the capability survives decoration:
/// <code>
/// var purge = auditStore.GetService(typeof(IAuditPurgeCapability)) as IAuditPurgeCapability
///     ?? throw new NotSupportedException("The configured audit store cannot purge events.");
/// var removed = await purge.PurgeExpiredAsync(cutoff, tenant, cancellationToken);
/// </code>
/// </para>
/// </remarks>
public interface IAuditPurgeCapability
{
	/// <summary>
	/// Permanently removes every audit event older than <paramref name="cutoff"/>, across all tenants,
	/// together with every record that exists only to describe them.
	/// </summary>
	/// <param name="cutoff">Events with an earlier timestamp are removed.</param>
	/// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
	/// <returns>The number of audit events removed.</returns>
	/// <remarks>
	/// <para>
	/// <strong>This operation is estate-wide by contract, and the absence of a tenant parameter is the
	/// contract rather than an omission.</strong> Retention answers "how long may anything be kept",
	/// which is a property of the data and its policy — not of who may read it. A retention pass that
	/// swept only one tenant would leave every other tenant's expired data in place while reporting a
	/// completed pass, so restricting this member to a partition would defeat the obligation it exists
	/// to discharge.
	/// </para>
	/// <para>
	/// It is deliberately a <em>separate, differently-named</em> member from
	/// <see cref="PurgeTenantAsync"/> rather than an overload with the tenant omitted. A caller cannot
	/// reach an estate-wide delete by leaving an argument out, forgetting one, or passing a value that
	/// happens to widen — they must name this operation. The unscoped sweep is expressible, and only
	/// by asking for it.
	/// </para>
	/// <para>
	/// Implementations MUST remove an event and its dependent records atomically. A record whose owning
	/// event has been deleted is not merely orphaned: where its tenant is derived by joining to that
	/// event, the survivor's tenant becomes underivable, so it is simultaneously unreadable by its true
	/// owner and at risk of being read as un-tenanted. Partial application is not a degraded outcome
	/// but a defect.
	/// </para>
	/// </remarks>
	Task<int> PurgeExpiredAsync(
		DateTimeOffset cutoff,
		CancellationToken cancellationToken);

	/// <summary>
	/// Permanently removes audit events older than <paramref name="cutoff"/> within a single tenant
	/// partition, together with every record that exists only to describe them.
	/// </summary>
	/// <param name="cutoff">Events with an earlier timestamp are removed.</param>
	/// <param name="tenant">
	/// The partition to purge. Required, and has no empty inhabitant: purging the un-tenanted partition is
	/// expressed by passing the reserved un-tenanted partition explicitly, never by omitting a tenant.
	/// </param>
	/// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
	/// <returns>The number of audit events removed.</returns>
	/// <remarks>
	/// This serves a targeted erasure — a subject request, a tenant offboarding — where the caller means
	/// exactly one partition. For scheduled retention use <see cref="PurgeExpiredAsync"/>; using this
	/// member on a timer would purge one tenant and silently abandon the rest.
	/// The atomicity requirement stated on <see cref="PurgeExpiredAsync"/> applies here unchanged.
	/// </remarks>
	Task<int> PurgeTenantAsync(
		DateTimeOffset cutoff,
		KeyedTenantPartition tenant,
		CancellationToken cancellationToken);
}

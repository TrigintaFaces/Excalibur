// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>
/// Optional event-store capability for observing current committed event state independently of an earlier read.
/// </summary>
/// <remarks>
/// <para>
/// Resolve this capability through the final decorated store's <see cref="IServiceProvider.GetService(Type)"/>.
/// A provider must not advertise it unless an owned, authoritative read path is configured. Decorators
/// imposing tenant, routing, or representation restrictions must mediate each call or deny the capability.
/// </para>
/// <para>
/// Each result observes committed state at a point between invocation and completion on the authoritative
/// database for this event. It must observe an erasure committed before invocation. Ambient transactions,
/// earlier snapshots, caches, and lagging replicas must not determine its visibility. A new connection alone
/// does not establish these properties. This is an observation point, not a fence against subsequent erasure
/// or an atomic snapshot of multiple calls.
/// </para>
/// </remarks>
public interface IEventStoreAuthoritativeReader
{
	/// <summary>Reads the current state using an identity that survives archival and erasure.</summary>
	/// <param name="tenant">The verified tenant partition, never inferred from later ambient context.</param>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The case-sensitive aggregate type.</param>
	/// <param name="eventId">The event identifier.</param>
	/// <param name="version">The aggregate version.</param>
	/// <param name="cancellationToken">Token to observe for cancellation.</param>
	/// <returns>
	/// Current state, or null only after a successful authoritative lookup finds no matching row.
	/// Unavailability, unsafe connection ownership, authorization failure, cancellation and database errors
	/// must fail the operation rather than appear as absence. Payload and arbitrary metadata are not returned.
	/// </returns>
	/// <remarks>
	/// Do not filter by the original event type: erasure changes it. Consumers must verify stable identity
	/// and any expected global position before accepting the result.
	/// The tenant must be non-null and verified; locator strings must be nonblank and version nonnegative.
	/// A returned state must carry its recorded tenant without normalization and nonnegative version and
	/// position. Requests outside the reader's authorized scope must fail, not return null.
	/// Event identity and non-erased payload/metadata are immutable across archival; this state-only read
	/// does not detect independent rewrites of live metadata.
	/// </remarks>
	ValueTask<EventStoreEventState?> ReadCurrentAsync(
		KeyedTenantPartition tenant,
		string aggregateId,
		string aggregateType,
		string eventId,
		long version,
		CancellationToken cancellationToken);
}

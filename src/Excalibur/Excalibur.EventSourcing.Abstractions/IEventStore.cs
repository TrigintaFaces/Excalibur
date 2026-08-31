// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;

namespace Excalibur.EventSourcing;

/// <summary>
/// Defines the contract for event store operations supporting event sourcing patterns.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides the core operations for event-sourced aggregates:
/// <list type="bullet">
/// <item>Loading events for aggregate hydration</item>
/// <item>Appending events with optimistic concurrency control</item>
/// <item>For reliable event publishing, use the transactional outbox pattern</item>
/// </list>
/// </para>
/// <para>
/// For snapshot operations, use <see cref="ISnapshotStore"/>.
/// </para>
/// <para>
/// <b>Tenant confinement.</b> Every operation is confined to the ambient tenant established for this
/// store instance: a confined read returns none of another tenant's rows and every one of the caller's
/// own, and a confined append can neither be observed by, nor collide with, another tenant's stream.
/// Which mechanism a given provider uses to hold that boundary is declared by its capability marker —
/// <see cref="ITenantScopingCapability{TContract}"/> for a store that reads an ambient tenant,
/// <see cref="ITenantPartitionedCapability{TContract}"/> for one that carries the tenant on the row and
/// re-establishes it on read — and the package's own <c>ARCHITECTURE.md</c> states the falsifiable
/// guarantee and how it is verified. A store presenting neither marker is not confined by the framework.
/// </para>
/// <para>
/// <b>Performance Note:</b> Methods return <see cref="ValueTask{TResult}"/> to avoid heap allocations
/// for synchronous completions (e.g., in-memory stores, cache hits). Callers should await the result
/// immediately and not store the ValueTask for later use.
/// </para>
/// </remarks>
[TenantOwned]
public interface IEventStore : IServiceProvider
{
	/// <summary>
	/// Resolves an optional event-store capability, or <see langword="null"/> when it is unavailable.
	/// </summary>
	/// <param name="serviceType">
	/// The capability interface to resolve, for example <see cref="ITransactionalEventStore"/>,
	/// <see cref="IEventStoreErasure"/> or <see cref="IEventStoreArchive"/>.
	/// </param>
	/// <returns>
	/// An instance assignable to <paramref name="serviceType"/> when this store provides the capability;
	/// otherwise <see langword="null"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Resolve capabilities through this method rather than testing the store's type. A store is commonly
	/// reached through a decorator -- telemetry, tenant scoping, encryption, tiered storage -- and a
	/// decorator's interface list is fixed when it is compiled, while the capabilities of the store it
	/// wraps are known only at run time. A type test therefore reports the decorator's own list and hides
	/// every capability the store beneath it provides.
	/// </para>
	/// <para>
	/// The default implementation answers for any capability this instance itself implements, so a store
	/// that implements a capability directly need not override it. Decorators override it to answer for
	/// the store they wrap.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : null;
	}

	/// <summary>
	/// Loads all events for an aggregate.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="aggregateType">The aggregate type name. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events for the aggregate in version order.</returns>
	/// <remarks>
	/// Confined to the ambient tenant established for this store instance: returns none of another
	/// tenant's events for this <paramref name="aggregateId"/>, and every one of the caller's own — a
	/// store that returns fewer than the caller's own full history satisfies neither this contract nor
	/// the aggregate that must replay from it.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is empty or white space.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is <see langword="null"/>.
	/// </exception>
	ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Loads events for an aggregate from a specific version.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="aggregateType">The aggregate type name. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="fromVersion">
	/// The version to start loading from, exclusive: the result begins at the next version after this one.
	/// Versions are zero-based, so <c>-1</c> loads the whole stream. See the version base stated on
	/// <see cref="AppendAsync"/>.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The events for the aggregate from the specified version in order.</returns>
	/// <remarks>
	/// Confined identically to the zero-argument overload above: none of another tenant's events for
	/// this <paramref name="aggregateId"/>, and every one of the caller's own from
	/// <paramref name="fromVersion"/> onward.
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is empty or white space.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is <see langword="null"/>.
	/// </exception>
	ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken);

	/// <summary>
	/// Appends events to the store with optimistic concurrency control.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="aggregateType">The aggregate type name. Must not be <see langword="null"/>, empty, or white space.</param>
	/// <param name="events">The events to append. Must not be <see langword="null"/>.</param>
	/// <param name="expectedVersion">
	/// The version the stream is expected to currently be at, or <c>-1</c> when the stream does not yet
	/// exist. See the version base stated in the remarks below.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>
	/// The result of the append operation, carrying the stream's version after the append in
	/// <see cref="AppendResult.NextExpectedVersion"/>.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>Version base.</b> Stream versions are zero-based and contiguous. The first event appended to a
	/// new stream is version <c>0</c>, and each subsequent event takes the next integer. A stream that
	/// does not yet exist has no current version, which <paramref name="expectedVersion"/> expresses as
	/// <c>-1</c>. Appending <c>N</c> events to a new stream therefore leaves it at version <c>N-1</c>:
	/// that is the value <see cref="AppendResult.NextExpectedVersion"/> reports, and the value the next
	/// append must pass as <paramref name="expectedVersion"/> — it is the version the stream is now at,
	/// not the version the next event will receive. Appending no events leaves the version unchanged and
	/// reports back the version it was given.
	/// </para>
	/// <para>
	/// An identifier that is <see langword="null"/>, empty, or white space is a usage error, never a legitimate
	/// stream: accepting one would fabricate a stream key and write events where no reader will ever look.
	/// Every implementation therefore rejects such an argument by throwing, rather than reporting a failed
	/// <see cref="AppendResult"/> — a returned result models a domain or infrastructure outcome, not a caller
	/// defect. Implementations validate their arguments before any I/O or concurrency check is attempted.
	/// </para>
	/// <para>
	/// <b>Outcome or defect.</b> The line between a returned <see cref="AppendResult"/> and a thrown
	/// exception is whether the identical call could ever succeed without a change to the calling program.
	/// A version clash could, once the caller reloads, so it is reported as a result carrying
	/// <see cref="AppendResult.IsConcurrencyConflict"/>; a transient store fault could, on retry, so it is
	/// reported as a failed result. An event type the configured resolver does not declare could not — no
	/// retry, reload or reconfiguration reaches it — so it throws, as do a blank identifier, a batch above
	/// the provider's atomic limit, and cancellation. Every implementation of this interface answers the
	/// same way; a caller may rely on that rather than on which provider is configured.
	/// </para>
	/// <para>
	/// <b>Cancellation propagates.</b> A cancelled append raises
	/// <see cref="OperationCanceledException"/> rather than reporting a failed result: reporting it as a
	/// store failure would invite a caller to retry inside a scope that has already been cancelled.
	/// </para>
	/// <para>
	/// Confined to the ambient tenant established for this store instance: the appended events join only
	/// this tenant's stream for <paramref name="aggregateId"/>, and the optimistic-concurrency check
	/// against <paramref name="expectedVersion"/> is evaluated within that same partition — another
	/// tenant holding a stream under the same <paramref name="aggregateId"/> can neither cause nor observe
	/// a conflict here.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">
	/// <paramref name="aggregateId"/> or <paramref name="aggregateType"/> is empty or white space.
	/// </exception>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="aggregateId"/>, <paramref name="aggregateType"/>, or <paramref name="events"/> is
	/// <see langword="null"/>.
	/// </exception>
	/// <exception cref="EventBatchTooLargeException">
	/// <paramref name="events"/> contains more events than the underlying store can append atomically. The
	/// exception carries the offending count and the limit, so a caller may split the batch and retry.
	/// </exception>
	/// <exception cref="EventTypeNotDeclaredException">
	/// An event's runtime type is not declared by the type-info resolver the host configured, so it cannot
	/// be serialized. Nothing is appended. The identical call cannot succeed until the type is declared,
	/// which is why this is thrown rather than reported as a failed <see cref="AppendResult"/>.
	/// </exception>
	ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken);

}

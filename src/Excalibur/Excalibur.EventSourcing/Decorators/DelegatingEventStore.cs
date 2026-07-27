// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Decorators;

/// <summary>
/// Abstract base class for <see cref="IEventStore"/> decorators.
/// All methods are virtual and forward to the inner store by default.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <c>DelegatingHandler</c> / <c>DelegatingChatClient</c> pattern from Microsoft.
/// Subclasses override only the methods they need to intercept.
/// </para>
/// <para>
/// The base forwards the <see cref="IEventStoreErasure"/> capability to the inner store by default, so a
/// decorator subclass cannot silently strip GDPR erasure merely by not re-implementing it — the recurring
/// hazard the <c>is IEventStoreErasure</c> probe is subject to (a capability-stripping decorator answers the
/// probe <see langword="false"/> for a store that actually supports erasure). The forward recurses through
/// nested delegating decorators and lands on the terminal provider store, which performs the erase. If the
/// inner store does not support erasure, the forward surfaces a clear <see cref="NotSupportedException"/> at
/// erase-time rather than at composition-time (the store still resolves and its other operations work).
/// </para>
/// </remarks>
public abstract class DelegatingEventStore : IEventStore, IEventStoreErasure
{
	/// <summary>
	/// Gets the inner event store being decorated.
	/// </summary>
	protected IEventStore Inner { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DelegatingEventStore"/> class.
	/// </summary>
	/// <param name="inner">The inner event store to delegate to.</param>
	protected DelegatingEventStore(IEventStore inner)
	{
		Inner = inner ?? throw new ArgumentNullException(nameof(inner));
	}

	/// <inheritdoc />
	public virtual ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		=> Inner.LoadAsync(aggregateId, aggregateType, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
		string aggregateId,
		string aggregateType,
		long fromVersion,
		CancellationToken cancellationToken)
		=> Inner.LoadAsync(aggregateId, aggregateType, fromVersion, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask<AppendResult> AppendAsync(
		string aggregateId,
		string aggregateType,
		IEnumerable<IDomainEvent> events,
		long expectedVersion,
		CancellationToken cancellationToken)
		=> Inner.AppendAsync(aggregateId, aggregateType, events, expectedVersion, cancellationToken);

	/// <inheritdoc />
	public virtual Task<int> EraseEventsAsync(
		string aggregateId,
		string aggregateType,
		Guid erasureRequestId,
		CancellationToken cancellationToken)
		=> RequireInnerErasure().EraseEventsAsync(aggregateId, aggregateType, erasureRequestId, cancellationToken);

	/// <inheritdoc />
	public virtual Task<bool> IsErasedAsync(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
		=> RequireInnerErasure().IsErasedAsync(aggregateId, aggregateType, cancellationToken);

	/// <summary>
	/// Resolves the inner store's <see cref="IEventStoreErasure"/> capability, forwarding the erase down the
	/// decoration chain. A decorator can only forward the capability the store it wraps actually supports; if the
	/// inner store does not implement <see cref="IEventStoreErasure"/>, this surfaces that explicitly rather than
	/// silently stripping erasure.
	/// </summary>
	/// <returns>The inner store viewed as <see cref="IEventStoreErasure"/>.</returns>
	/// <exception cref="NotSupportedException">The inner store does not support GDPR erasure.</exception>
	private protected IEventStoreErasure RequireInnerErasure()
		=> Inner as IEventStoreErasure
			?? throw new NotSupportedException(
				$"The inner event store ({Inner.GetType().Name}) does not support GDPR erasure (IEventStoreErasure).");
}

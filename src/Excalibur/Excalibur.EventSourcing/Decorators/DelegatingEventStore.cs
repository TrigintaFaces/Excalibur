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
/// nested delegating decorators and lands on the terminal provider store, which performs the erase. The
/// capability is FORWARDED, never manufactured: a decorator over a store that cannot erase does not
/// advertise erasure, because <see cref="GetService"/> resolves that probe against the inner chain rather
/// than against this type's declaration. Probe with <see cref="GetService"/> rather than testing
/// <c>is IEventStoreErasure</c> — the type test reads the unconditional declaration and is true for every
/// decorator regardless of what it wraps.
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

	/// <summary>
	/// Resolves a capability, answering for capabilities this decorator itself implements and deferring
	/// all others to <see cref="Inner"/>.
	/// </summary>
	/// <param name="serviceType">The capability interface to resolve.</param>
	/// <returns>
	/// This decorator when it implements <paramref name="serviceType"/>; otherwise whatever the decorated
	/// store resolves, which is <see langword="null"/> when no participant provides the capability.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Forwarding unconditionally is correct for an <em>observational</em> decorator -- one that measures
	/// or records but imposes no invariant on the events passing through it. Such a decorator has nothing
	/// to protect, so passing a capability through preserves it losslessly, and a subclass cannot strip a
	/// capability merely by not re-declaring it.
	/// </para>
	/// <para>
	/// A decorator that <em>does</em> impose an invariant -- encryption, tenant scoping, tiered routing --
	/// must not inherit this behaviour: handing a caller a capability that derives from
	/// <see cref="IEventStore"/> would hand them the store beneath the decorator, unmediated. Those derive
	/// from <see cref="IsolatingEventStoreDecorator"/> instead, which denies by default.
	/// </para>
	/// <para>
	/// <see cref="IEventStoreErasure"/> is answered CONDITIONALLY, and this is the one capability that
	/// cannot follow the rule above. The base declares that interface unconditionally -- C# has no
	/// conditional interface declaration -- but it implements no erasure of its own: both members forward
	/// to the inner store and throw when the inner store has none. Answering the probe with
	/// <see langword="this"/> on the strength of the declaration alone would therefore report the
	/// capability as present over EVERY inner store, including one that cannot erase, which is a probe
	/// that always says yes. The probe is resolved against the inner chain instead: the decorator is
	/// returned only when something beneath it actually provides erasure, so a caller receives
	/// <see langword="null"/> for a chain that has none -- the contract for an absent capability -- and
	/// still reaches the erasure THROUGH this decorator when the chain does, so a decorator cannot be
	/// bypassed. A subclass that implements erasure itself rather than forwarding it overrides this
	/// method.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	public virtual object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IEventStoreErasure))
		{
			return Inner.GetService(serviceType) is null ? null : this;
		}

		return serviceType.IsInstanceOfType(this) ? this : Inner.GetService(serviceType);
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
		=> Inner.GetService(typeof(IEventStoreErasure)) as IEventStoreErasure
			?? throw new NotSupportedException(
				$"The inner event store ({Inner.GetType().Name}) does not support GDPR erasure (IEventStoreErasure).");
}

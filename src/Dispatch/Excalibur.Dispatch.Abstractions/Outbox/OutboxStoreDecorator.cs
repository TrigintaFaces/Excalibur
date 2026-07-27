// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// Base class for outbox store decorators. Forwards capability resolution to the decorated store so that
/// decoration can never drop a capability the underlying store provides.
/// </summary>
/// <remarks>
/// <para>
/// A C# type's interface list is fixed at compile time, but the capability set of the store a decorator
/// wraps is only known at run time. A decorator that re-declares capability interfaces therefore has to
/// guess, and every guess is wrong for some inner store: declare too few and a real capability becomes
/// invisible to consumers; declare too many and the decorator advertises behaviour its inner store cannot
/// perform. Resolving capabilities through <see cref="IServiceProvider.GetService(Type)"/> removes the
/// guess -- the decorator answers for what it adds and asks the store it wraps about everything else.
/// </para>
/// <para>
/// Consequently a decorator derived from this type <em>cannot</em> strip a capability: there is no
/// declaration to omit. The failure is not detected by a test; it is unwritable.
/// </para>
/// <para>
/// Decorators compose. Given <c>Telemetry(Encrypting(store))</c>, a request for an administrative
/// capability walks outward-in until it reaches the first participant that provides it.
/// </para>
/// </remarks>
/// <param name="inner"> The store being decorated. </param>
public abstract class OutboxStoreDecorator(IOutboxStore inner) : IOutboxStore
{
	/// <summary>
	/// Gets the store being decorated.
	/// </summary>
	/// <value> The inner store; never <see langword="null"/>. </value>
	protected IOutboxStore Inner { get; } = inner ?? throw new ArgumentNullException(nameof(inner));

	/// <summary>
	/// Resolves an optional outbox capability, answering for capabilities this decorator itself provides and
	/// deferring all others to <see cref="Inner"/>.
	/// </summary>
	/// <param name="serviceType"> The capability interface to resolve. </param>
	/// <returns>
	/// This decorator when it implements <paramref name="serviceType"/>; otherwise whatever the decorated
	/// store resolves, which is <see langword="null"/> when no participant provides the capability.
	/// </returns>
	/// <remarks>
	/// Override only to intercept a capability the decorator wishes to wrap rather than pass through, and
	/// call <c>base.GetService</c> for every other type. Never return a value for a capability the inner
	/// store lacks: a caller reads a non-null result as a promise that the operation will be performed.
	/// </remarks>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	public virtual object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : Inner.GetService(serviceType);
	}

	/// <inheritdoc />
	public virtual ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken) =>
		Inner.StageMessageAsync(message, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken) =>
		Inner.EnqueueAsync(message, context, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(int batchSize, CancellationToken cancellationToken) =>
		Inner.GetUnsentMessagesAsync(batchSize, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken) =>
		Inner.MarkSentAsync(messageId, cancellationToken);

	/// <inheritdoc />
	public virtual ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken) =>
		Inner.MarkFailedAsync(messageId, errorMessage, retryCount, cancellationToken);
}

/// <summary>
/// Base class for outbox store decorators that impose an invariant on the messages passing through them --
/// encryption, redaction, tenant scoping -- and must therefore never hand a caller unmediated access to the
/// store they wrap.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OutboxStoreDecorator"/> forwards every capability it does not itself implement to the decorated
/// store. For an <em>observational</em> decorator, one whose invariant is empty, that is correct and safe: a
/// telemetry decorator has nothing to protect, so passing a capability through preserves it losslessly. For a
/// <em>transforming</em> decorator the same forwarding is a bypass. A caller that asks a decorated store for a
/// capability and receives the undecorated store beneath it has been handed the plaintext the decorator exists
/// to prevent, and has been handed it by the very object that promised to prevent it.
/// </para>
/// <para>
/// Capability resolution here is therefore <strong>deny-by-default</strong>. A capability is reachable through
/// this decorator only when the decorator has explicitly said how: either it <em>wraps</em> the capability, by
/// returning a view that upholds the invariant (see <see cref="WrapCapability"/>), or it <em>forwards</em> the
/// capability, by naming it in <see cref="ForwardableCapabilities"/> as one whose surface cannot carry a
/// protected payload. Everything else resolves to <see langword="null"/>.
/// </para>
/// <para>
/// The default is empty, and the failure mode is loud. A capability that is neither wrapped nor forwarded
/// becomes unobtainable rather than unprotected: the caller sees <see langword="null"/> and cannot proceed,
/// instead of seeing a working object that quietly returns ciphertext or accepts plaintext. A confidentiality
/// boundary is not a place to trade a silent leak for a silent absence of one.
/// </para>
/// <para>
/// <see cref="GetService"/> is sealed. A derived decorator cannot reopen unconditional forwarding by overriding
/// it, so "I forgot to wrap the ninth capability" is expressible only as a <see langword="null"/>, never as a
/// leak. The next capability added to this codebase is denied by this type before anyone has read it.
/// </para>
/// </remarks>
/// <param name="inner"> The store being decorated. </param>
public abstract class IsolatingOutboxStoreDecorator(IOutboxStore inner) : OutboxStoreDecorator(inner)
{
	/// <summary>
	/// Gets the capabilities this decorator permits to be resolved directly from the decorated store, without
	/// mediation.
	/// </summary>
	/// <value>
	/// The set of capability interfaces whose every member is free of protected payload. Empty by default: a
	/// capability is forwarded only when a derived decorator has affirmatively established that passing it
	/// through cannot breach the decorator's invariant.
	/// </value>
	/// <remarks>
	/// Establish this per member, not per name. An interface qualifies only when no member of it -- including
	/// members it inherits from base interfaces, which <see cref="Type.GetMethods()"/> does not report -- accepts
	/// or returns a protected payload.
	/// </remarks>
	protected virtual IReadOnlySet<Type> ForwardableCapabilities => EmptyCapabilities;

	private static readonly IReadOnlySet<Type> EmptyCapabilities = new HashSet<Type>();

	/// <summary>
	/// Returns a view over the decorated store's capability that upholds this decorator's invariant, when the
	/// decorator knows how to build one.
	/// </summary>
	/// <param name="serviceType"> The capability interface being resolved. </param>
	/// <returns>
	/// A mediating view over the decorated store's capability; or <see langword="null"/> when this decorator does
	/// not wrap <paramref name="serviceType"/>, or when the decorated store does not provide it.
	/// </returns>
	/// <remarks>
	/// Return <see langword="null"/> when the decorated store lacks the capability -- never a view over nothing. A
	/// caller reads a non-null result as a promise that the operation will be performed.
	/// </remarks>
	protected virtual object? WrapCapability(Type serviceType) => null;

	/// <summary>
	/// Resolves a capability, denying by default anything this decorator neither wraps nor has declared forwardable.
	/// </summary>
	/// <param name="serviceType"> The capability interface to resolve. </param>
	/// <returns>
	/// This decorator when it implements <paramref name="serviceType"/>; a mediating view when this decorator wraps
	/// it; the decorated store's capability when it is forwardable; otherwise <see langword="null"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	public sealed override object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType.IsInstanceOfType(this))
		{
			return this;
		}

		return WrapCapability(serviceType)
			?? (ForwardableCapabilities.Contains(serviceType) ? Inner.GetService(serviceType) : null);
	}
}

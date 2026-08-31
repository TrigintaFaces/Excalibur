// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Decorators;

/// <summary>
/// Base class for event store decorators that impose an invariant on the events passing through them --
/// encryption, tenant scoping, tiered routing -- and must therefore never hand a caller unmediated access
/// to the store they wrap.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DelegatingEventStore"/> forwards every capability it does not itself implement. For an
/// observational decorator that is correct. For a decorator that imposes an invariant it is a bypass: a
/// caller that asks a decorated store for a capability and receives the undecorated store beneath it has
/// been handed the plaintext, the unscoped rows, or the hot tier alone -- and has been handed it by the
/// very object that promised to prevent that.
/// </para>
/// <para>
/// Capability resolution here is therefore <strong>deny-by-default</strong>. A capability is reachable
/// through this decorator only when the decorator has said how: either it <em>wraps</em> the capability by
/// returning a view that upholds the invariant (see <see cref="WrapCapability"/>), or it <em>forwards</em>
/// the capability by naming it in <see cref="ForwardableCapabilities"/> as one whose surface cannot carry
/// a protected payload or reach the inner store's own event operations.
/// </para>
/// <para>
/// The default is empty and the failure mode is loud. A capability that is neither wrapped nor forwarded
/// becomes unobtainable rather than unprotected: the caller sees <see langword="null"/> and takes the
/// decorated path, instead of seeing a working object that quietly returns ciphertext, another tenant's
/// rows, or a partial history.
/// </para>
/// <para>
/// <see cref="GetService"/> is sealed, so a derived decorator cannot reopen unconditional forwarding.
/// </para>
/// </remarks>
/// <param name="inner">The event store being decorated.</param>
public abstract class IsolatingEventStoreDecorator(IEventStore inner) : DelegatingEventStore(inner)
{
	private static readonly IReadOnlySet<Type> EmptyCapabilities = new HashSet<Type>();

	/// <summary>
	/// Gets the capabilities this decorator permits to be resolved directly from the decorated store,
	/// without mediation.
	/// </summary>
	/// <value>
	/// The set of capability interfaces whose every member is free of protected payload and which do not
	/// derive from <see cref="IEventStore"/>. Empty by default: a capability is forwarded only when a
	/// derived decorator has established that passing it through cannot breach the decorator's invariant.
	/// </value>
	/// <remarks>
	/// Establish this per member, not per name. A capability deriving from <see cref="IEventStore"/> never
	/// qualifies, because forwarding it hands the caller a fully functional undecorated event store.
	/// </remarks>
	protected virtual IReadOnlySet<Type> ForwardableCapabilities => EmptyCapabilities;

	/// <summary>
	/// Returns a view over the decorated store's capability that upholds this decorator's invariant, when
	/// the decorator knows how to build one.
	/// </summary>
	/// <param name="serviceType">The capability interface being resolved.</param>
	/// <returns>
	/// A mediating view over the decorated store's capability; or <see langword="null"/> when this
	/// decorator does not wrap <paramref name="serviceType"/>, or when the decorated store does not
	/// provide it.
	/// </returns>
	/// <remarks>
	/// Return <see langword="null"/> when the decorated store lacks the capability -- never a view over
	/// nothing. A caller reads a non-null result as a promise that the operation will be performed.
	/// </remarks>
	protected virtual object? WrapCapability(Type serviceType) => null;

	/// <summary>
	/// Resolves a capability, denying by default anything this decorator neither wraps nor has declared
	/// forwardable.
	/// </summary>
	/// <param name="serviceType">The capability interface to resolve.</param>
	/// <returns>
	/// This decorator when it implements <paramref name="serviceType"/>; a mediating view when this
	/// decorator wraps it; the decorated store's capability when it is forwardable; otherwise
	/// <see langword="null"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceType"/> is null. </exception>
	public sealed override object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IEventStoreErasure))
		{
			// The base declares IEventStoreErasure unconditionally and implements none of its own: both
			// members forward to the inner store. A plain type test therefore reads that declaration and
			// answers the probe with this decorator over EVERY inner store, including one that cannot
			// erase - a probe that always says yes, and one that would slip past deny-by-default because
			// the type test runs before it. Resolve against the inner chain instead, so a caller gets null
			// for a chain with no erasure and still reaches the erase THROUGH this decorator when there is
			// one. An isolating subclass that erases itself rather than forwarding must revisit this.
			return Inner.GetService(serviceType) is null ? null : this;
		}

		if (serviceType.IsInstanceOfType(this))
		{
			return this;
		}

		return WrapCapability(serviceType)
			?? (ForwardableCapabilities.Contains(serviceType) ? Inner.GetService(serviceType) : null);
	}
}

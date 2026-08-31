// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing.Decorators;

/// <summary>
/// Base class for projection store decorators that impose an invariant on the projections passing through
/// them -- encryption, tenant scoping -- and must therefore never hand a caller unmediated access to the
/// store they wrap.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
/// <remarks>
/// <para>
/// A decorator's interface list is fixed when it is compiled; the capability set of the store it wraps is
/// known only at run time. A decorator that re-declares capability interfaces therefore has to guess, and
/// every guess is wrong for some inner store: declare too few and a real capability becomes invisible to
/// consumers, declare too many and the decorator advertises behaviour its inner store cannot perform.
/// Resolving capabilities through <see cref="IServiceProvider.GetService(Type)"/> removes the guess.
/// </para>
/// <para>
/// Every projection capability interface derives from <see cref="IProjectionStore{TProjection}"/>. Handing a
/// caller the decorated store's capability object would therefore hand them a fully functional, undecorated
/// projection store -- the plaintext, or the unscoped rows, that this decorator exists to prevent -- and hand
/// it to them through the very object that promised to prevent it. No projection capability is forwardable;
/// a capability is reachable through this decorator only when the decorator says how, by returning a
/// mediating view from <see cref="WrapCapability"/>.
/// </para>
/// <para>
/// Resolution is therefore <strong>deny-by-default</strong>, and the failure mode is a missing optimisation
/// rather than a breach. A capability that is not wrapped resolves to <see langword="null"/>, and the caller
/// falls back to the decorated <see cref="IProjectionStore{TProjection}"/> surface, which upholds the
/// invariant. "I forgot to wrap the next capability" is expressible only as a slower correct answer, never
/// as a leak.
/// </para>
/// <para>
/// <see cref="GetService"/> is sealed so that a derived decorator cannot reopen unconditional forwarding.
/// </para>
/// </remarks>
public abstract class IsolatingProjectionStoreDecorator<TProjection> : IProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Initializes a new instance of the <see cref="IsolatingProjectionStoreDecorator{TProjection}"/> class.
	/// </summary>
	/// <param name="inner">The projection store being decorated.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
	protected IsolatingProjectionStoreDecorator(IProjectionStore<TProjection> inner)
	{
		ArgumentNullException.ThrowIfNull(inner);

		Inner = inner;
	}

	/// <summary>
	/// Gets the projection store being decorated.
	/// </summary>
	/// <value>The inner store; never <see langword="null"/>.</value>
	protected IProjectionStore<TProjection> Inner { get; }

	/// <summary>
	/// Resolves a capability, denying by default anything this decorator does not wrap.
	/// </summary>
	/// <param name="serviceType">The capability interface to resolve.</param>
	/// <returns>
	/// This decorator when it implements <paramref name="serviceType"/>; a mediating view when this decorator
	/// wraps it; otherwise <see langword="null"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceType"/> is null.</exception>
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : WrapCapability(serviceType);
	}

	/// <summary>
	/// Returns a view over the decorated store's capability that upholds this decorator's invariant, when the
	/// decorator knows how to build one.
	/// </summary>
	/// <param name="serviceType">The capability interface being resolved.</param>
	/// <returns>
	/// A mediating view over the decorated store's capability; or <see langword="null"/> when this decorator
	/// does not wrap <paramref name="serviceType"/>, or when the decorated store does not provide it.
	/// </returns>
	/// <remarks>
	/// Return <see langword="null"/> when the decorated store lacks the capability -- never a view over
	/// nothing. A caller reads a non-null result as a promise that the operation will be performed.
	/// </remarks>
	protected virtual object? WrapCapability(Type serviceType) => null;

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public abstract Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public abstract Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken);

	/// <inheritdoc />
	public abstract Task DeleteAsync(string id, CancellationToken cancellationToken);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public abstract Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken);

	/// <inheritdoc />
	public abstract Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken);
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

namespace Excalibur.EventSourcing.Decorators;

/// <summary>
/// Base for the mediating views an <see cref="IsolatingProjectionStoreDecorator{TProjection}"/> returns from
/// <c>WrapCapability</c>.
/// </summary>
/// <typeparam name="TProjection">The projection type.</typeparam>
/// <remarks>
/// Every projection capability interface derives from <see cref="IProjectionStore{TProjection}"/>, so a view
/// over one is also a projection store and would breach the decorator's invariant if it reached past it. This
/// base routes the whole inherited surface back through the decorator, leaving a derived view to mediate only
/// the one member its capability adds.
/// </remarks>
/// <param name="outer">The decorator whose invariant the view must uphold.</param>
public abstract class ProjectionStoreCapabilityView<TProjection>(IProjectionStore<TProjection> outer)
	: IProjectionStore<TProjection>
	where TProjection : class
{
	/// <summary>
	/// Gets the decorator this view defers to for every operation it does not itself mediate.
	/// </summary>
	/// <value>The decorating store; never <see langword="null"/>.</value>
	protected IProjectionStore<TProjection> Outer { get; } = outer ?? throw new ArgumentNullException(nameof(outer));

	/// <summary>
	/// Resolves a capability by answering for this view when it provides it, and otherwise through the
	/// decorator.
	/// </summary>
	/// <param name="serviceType">The capability interface to resolve.</param>
	/// <returns>
	/// This view when it is assignable to <paramref name="serviceType"/>; otherwise whatever the
	/// decorator resolves, which is <see langword="null"/> when the capability is unavailable.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>Why this is here at all.</b> A capability view is also an <see cref="IProjectionStore{TProjection}"/>,
	/// so it inherits that interface's capability lookup -- which answers for the instance ITSELF and
	/// knows nothing of the store beneath. A view left with that answer reports that the store it
	/// mediates has no capabilities at all. Supplying the answer ONCE here is what makes the summary
	/// above ("routes the whole inherited surface back through the decorator") true of every view rather
	/// than of the views whose authors remembered.
	/// </para>
	/// <para>
	/// <b>Why it answers for itself first rather than delegating unconditionally.</b> A view exists
	/// because handing out the inner capability directly would bypass the decorator -- skipping the
	/// encryption, the tenant predicate, or the shard routing the decorator exists to apply. The view IS
	/// the mediated form of its own capability, so a probe for that capability must yield the view. Were
	/// this to delegate unconditionally, a capability the decorator does not itself mediate would be
	/// resolved from the store beneath it and returned raw, reinstating exactly the breach the view was
	/// built to prevent. Every other capability falls through to the decorator, which mediates it or
	/// reports it absent.
	/// </para>
	/// <para>
	/// Supplied as an explicit interface implementation: it is the capability-resolution seam, not part
	/// of a view's own surface, and a derived view has no reason to call it.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceType"/> is null.</exception>
	object? IServiceProvider.GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		return serviceType.IsInstanceOfType(this) ? this : Outer.GetService(serviceType);
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<TProjection?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
		Outer.GetByIdAsync(id, cancellationToken);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task UpsertAsync(string id, TProjection projection, CancellationToken cancellationToken) =>
		Outer.UpsertAsync(id, projection, cancellationToken);

	/// <inheritdoc />
	public Task DeleteAsync(string id, CancellationToken cancellationToken) =>
		Outer.DeleteAsync(id, cancellationToken);

	/// <inheritdoc />
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public Task<IReadOnlyList<TProjection>> QueryAsync(
		IDictionary<string, object>? filters,
		QueryOptions? options,
		CancellationToken cancellationToken) =>
		Outer.QueryAsync(filters, options, cancellationToken);

	/// <inheritdoc />
	public Task<long> CountAsync(IDictionary<string, object>? filters, CancellationToken cancellationToken) =>
		Outer.CountAsync(filters, cancellationToken);
}

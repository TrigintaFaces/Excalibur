// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;

namespace Excalibur.EventSourcing.Views;

/// <summary>
/// Strongly-typed accessor over the store's generic <c>GetAsync&lt;TView&gt;</c>/<c>SaveAsync&lt;TView&gt;</c>.
/// One instance is created per view type at DI registration (where <c>TView</c> is statically known), so
/// per-event access is a plain <c>await</c> over a <see cref="System.Threading.Tasks.ValueTask"/> that is
/// consumed exactly once — with no runtime reflection (<c>MakeGenericType</c>/<c>Activator</c>) and no
/// double-consume.
/// </summary>
internal abstract class ViewStoreAccessor
{
	/// <summary>Gets the declared view name through the builder interface.</summary>
	public abstract string GetViewName(object builder);
	/// <summary>Gets handled event types through the builder interface.</summary>
	public abstract IReadOnlyList<Type> GetHandledEventTypes(object builder);
	/// <summary>Gets an event's target view identifier through the builder interface.</summary>
	public abstract string? GetViewId(object builder, IDomainEvent domainEvent);
	/// <summary>Creates a view, honoring default and explicit interface implementations.</summary>
	public abstract object CreateNew(object builder);
	/// <summary>Applies an event through the builder interface.</summary>
	public abstract object Apply(object builder, object view, IDomainEvent domainEvent);

	/// <summary>Loads a view from the store (returns <see langword="null"/> if not found).</summary>
	[RequiresUnreferencedCode("The store serializes TView reflectively.")]
	[RequiresDynamicCode("The store serializes TView reflectively.")]
	public abstract ValueTask<object?> GetAsync(
		IMaterializedViewStore store, string viewName, string viewId, CancellationToken cancellationToken);

	/// <summary>Saves a view to the store.</summary>
	[RequiresUnreferencedCode("The store serializes TView reflectively.")]
	[RequiresDynamicCode("The store serializes TView reflectively.")]
	public abstract ValueTask SaveAsync(
		IMaterializedViewStore store, string viewName, string viewId, object view, CancellationToken cancellationToken);

	/// <summary>Atomically saves a view and advances its processed position in one durable operation.</summary>
	/// <remarks>
	/// Takes <see cref="IAtomicMaterializedViewStore"/>, not <see cref="IMaterializedViewStore"/>: only a store
	/// that declares the atomic capability can reach this path, so a non-atomic store cannot be handed to it.
	/// </remarks>
	[RequiresUnreferencedCode("The store serializes TView reflectively.")]
	[RequiresDynamicCode("The store serializes TView reflectively.")]
	public abstract ValueTask SaveWithPositionAsync(
		IAtomicMaterializedViewStore store, string viewName, string viewId, object view, long position, CancellationToken cancellationToken);
}

/// <summary>Generic <see cref="ViewStoreAccessor"/> bound to a concrete <typeparamref name="TView"/>.</summary>
/// <typeparam name="TView">The materialized view type.</typeparam>
internal sealed class ViewStoreAccessor<TView> : ViewStoreAccessor
	where TView : class, new()
{
	/// <inheritdoc />
	public override string GetViewName(object builder) => ((IMaterializedViewBuilder<TView>)builder).ViewName;
	/// <inheritdoc />
	public override IReadOnlyList<Type> GetHandledEventTypes(object builder) => ((IMaterializedViewBuilder<TView>)builder).HandledEventTypes;
	/// <inheritdoc />
	public override string? GetViewId(object builder, IDomainEvent domainEvent) => ((IMaterializedViewBuilder<TView>)builder).GetViewId(domainEvent);
	/// <inheritdoc />
	public override object CreateNew(object builder) => ((IMaterializedViewBuilder<TView>)builder).CreateNew();
	/// <inheritdoc />
	public override object Apply(object builder, object view, IDomainEvent domainEvent) =>
		((IMaterializedViewBuilder<TView>)builder).Apply((TView)view, domainEvent);

	/// <inheritdoc />
	[RequiresUnreferencedCode("The store serializes TView reflectively.")]
	[RequiresDynamicCode("The store serializes TView reflectively.")]
	public override async ValueTask<object?> GetAsync(
		IMaterializedViewStore store, string viewName, string viewId, CancellationToken cancellationToken)
		=> await store.GetAsync<TView>(viewName, viewId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	[RequiresUnreferencedCode("The store serializes TView reflectively.")]
	[RequiresDynamicCode("The store serializes TView reflectively.")]
	public override async ValueTask SaveAsync(
		IMaterializedViewStore store, string viewName, string viewId, object view, CancellationToken cancellationToken)
		=> await store.SaveAsync<TView>(viewName, viewId, (TView)view, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	[RequiresUnreferencedCode("The store serializes TView reflectively.")]
	[RequiresDynamicCode("The store serializes TView reflectively.")]
	public override async ValueTask SaveWithPositionAsync(
		IAtomicMaterializedViewStore store, string viewName, string viewId, object view, long position, CancellationToken cancellationToken)
		=> await store.SaveViewAndPositionAsync<TView>(viewName, viewId, (TView)view, position, cancellationToken).ConfigureAwait(false);
}

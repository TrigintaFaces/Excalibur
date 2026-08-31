// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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

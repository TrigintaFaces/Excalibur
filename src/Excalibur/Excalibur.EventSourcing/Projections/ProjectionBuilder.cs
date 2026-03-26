// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Internal implementation of <see cref="IProjectionBuilder{TProjection}"/>.
/// Builds a <see cref="ProjectionRegistration"/> and registers it in the
/// <see cref="IProjectionRegistry"/>.
/// </summary>
internal sealed class ProjectionBuilder<TProjection> : IProjectionBuilder<TProjection>
	where TProjection : class, new()
{
	private readonly IProjectionRegistry _registry;
	private readonly MultiStreamProjection<TProjection> _projection = new();
	private ProjectionMode _mode = ProjectionMode.Async;
	private TimeSpan? _cacheTtl;

	internal ProjectionBuilder(IProjectionRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);
		_registry = registry;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> Inline()
	{
		_mode = ProjectionMode.Inline;
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> Async()
	{
		_mode = ProjectionMode.Async;
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> When<TEvent>(Action<TProjection, TEvent> handler)
		where TEvent : IDomainEvent
	{
		ArgumentNullException.ThrowIfNull(handler);
		_projection.AddHandler(handler);
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WithCacheTtl(TimeSpan ttl)
	{
		_cacheTtl = ttl;
		return this;
	}

	/// <summary>
	/// Gets the optional cache TTL configured via <see cref="WithCacheTtl"/>.
	/// </summary>
	internal TimeSpan? CacheTtl => _cacheTtl;

	/// <summary>
	/// Builds and registers the projection in the registry.
	/// A second call for the same projection type replaces the first (R27.37).
	/// </summary>
	internal void Build()
	{
		// Capture the generic type in a delegate at registration time (AOT-safe, no MakeGenericMethod)
		var inlineApply = _mode == ProjectionMode.Inline
			? CreateInlineApplyDelegate()
			: null;

		var registration = new ProjectionRegistration(
			typeof(TProjection),
			_mode,
			_projection,
			inlineApply,
			_cacheTtl);

		_registry.Register(registration);
	}

	private ProjectionRegistration.InlineApplyDelegate CreateInlineApplyDelegate()
	{
		var projection = _projection;

		return async (events, context, serviceProvider, cancellationToken) =>
		{
			var store = serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();

			// Load existing projection state or create new
			var state = await store.GetByIdAsync(context.AggregateId, cancellationToken)
				.ConfigureAwait(false)
				?? new TProjection();

			// Apply events sequentially in commit order
			foreach (var @event in events)
			{
				projection.Apply(state, @event);
			}

			// Persist the updated projection
			await store.UpsertAsync(context.AggregateId, state, cancellationToken)
				.ConfigureAwait(false);
		};
	}
}

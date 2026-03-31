// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Internal implementation of <see cref="IProjectionBuilder{TProjection}"/>.
/// Builds a <see cref="ProjectionRegistration"/> and registers it in the
/// <see cref="IProjectionRegistry"/>.
/// </summary>
internal sealed class ProjectionBuilder<TProjection> : IProjectionBuilder<TProjection>
	where TProjection : class, new()
{
	private readonly IProjectionRegistry? _registry;
	private readonly IServiceCollection? _services;
	private readonly MultiStreamProjection<TProjection> _projection = new();
	private ProjectionMode _mode = ProjectionMode.Async;
	private TimeSpan? _cacheTtl;
	private DirtyCheckingMode _dirtyCheckingMode;

	/// <summary>
	/// Creates a builder with a registry for direct build (used by tests).
	/// </summary>
	internal ProjectionBuilder(IProjectionRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);
		_registry = registry;
	}

	/// <summary>
	/// Creates a builder with DI service collection access for handler registration.
	/// The projection is registered in the registry later via <see cref="Build(IProjectionRegistry)"/>.
	/// </summary>
	internal ProjectionBuilder(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		_services = services;
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
	public IProjectionBuilder<TProjection> WhenHandledBy<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
		where TEvent : IDomainEvent
		where THandler : IProjectionEventHandler<TProjection, TEvent>
	{
		// Pre-compile a delegate that resolves the handler from DI and invokes it.
		// All generics are closed at registration time -- AOT-safe, no reflection on hot path.
		_projection.AddAsyncHandler<TEvent>(
			async (projection, domainEvent, context, serviceProvider, cancellationToken) =>
			{
				var handler = (IProjectionEventHandler<TProjection, TEvent>)
					serviceProvider.GetRequiredService(typeof(THandler));
				await handler.HandleAsync(projection, (TEvent)domainEvent, context, cancellationToken)
					.ConfigureAwait(false);
			});

		// Register the handler type in DI if IServiceCollection is available (T.6)
		_services?.TryAddTransient(typeof(THandler));

		return this;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover IProjectionEventHandler<T, TEvent> implementations.")]
	public IProjectionBuilder<TProjection> AddProjectionHandlersFromAssembly(Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(assembly);

		var handlerInterfaceType = typeof(IProjectionEventHandler<,>);
		var projectionType = typeof(TProjection);

		// Track which event types have handlers for duplicate detection (D3)
		var discoveredHandlers = new Dictionary<Type, Type>();

		foreach (var type in assembly.GetTypes())
		{
			if (type.IsAbstract || type.IsInterface || !type.IsClass || type.IsGenericTypeDefinition)
			{
				continue;
			}

			foreach (var iface in type.GetInterfaces())
			{
				if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != handlerInterfaceType)
				{
					continue;
				}

				var genericArgs = iface.GetGenericArguments();
				var handlerProjectionType = genericArgs[0];
				var eventType = genericArgs[1];

				// Only register handlers for this projection type
				if (handlerProjectionType != projectionType)
				{
					continue;
				}

				// Duplicate detection (D3): InvalidOperationException on same (TProjection, TEvent)
				if (discoveredHandlers.TryGetValue(eventType, out var existingHandler))
				{
					throw new InvalidOperationException(
						$"Duplicate handler for ({projectionType.Name}, {eventType.Name}): " +
						$"both {existingHandler.Name} and {type.Name} handle the same event type. " +
						$"Only one handler per (TProjection, TEvent) pair is allowed.");
				}

				discoveredHandlers[eventType] = type;

				// Register via reflection: call the private generic method with closed types
				var registerMethod = typeof(ProjectionBuilder<TProjection>)
					.GetMethod(nameof(RegisterScannedHandler), BindingFlags.NonPublic | BindingFlags.Instance)!
					.MakeGenericMethod(eventType, type);

				registerMethod.Invoke(this, null);
			}
		}

		return this;
	}

	/// <summary>
	/// Registers a scanned handler type via the typed WhenHandledBy path.
	/// Called via reflection during assembly scanning with closed generic types.
	/// </summary>
	[RequiresUnreferencedCode("Called via reflection during assembly scanning.")]
	private void RegisterScannedHandler<TEvent, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
		where TEvent : IDomainEvent
		where THandler : IProjectionEventHandler<TProjection, TEvent>
	{
		WhenHandledBy<TEvent, THandler>();
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WithCacheTtl(TimeSpan ttl)
	{
		_cacheTtl = ttl;
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WithDirtyChecking(DirtyCheckingMode mode = DirtyCheckingMode.Equality)
	{
		_dirtyCheckingMode = mode;
		return this;
	}

	/// <summary>
	/// Gets the optional cache TTL configured via <see cref="WithCacheTtl"/>.
	/// </summary>
	internal TimeSpan? CacheTtl => _cacheTtl;

	/// <summary>
	/// Gets the dirty checking mode configured via <see cref="WithDirtyChecking"/>.
	/// </summary>
	internal DirtyCheckingMode DirtyChecking => _dirtyCheckingMode;

	/// <summary>
	/// Builds and registers the projection using the registry provided at construction.
	/// A second call for the same projection type replaces the first (R27.37).
	/// </summary>
	internal void Build()
	{
		if (_registry is null)
		{
			throw new InvalidOperationException(
				"Build() requires a registry. Use Build(IProjectionRegistry) or construct with a registry.");
		}

		Build(_registry);
	}

	/// <summary>
	/// Builds and registers the projection in the specified registry.
	/// A second call for the same projection type replaces the first (R27.37).
	/// </summary>
	/// <param name="registry">The projection registry to register in.</param>
	internal void Build(IProjectionRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

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

		registry.Register(registration);
	}

	private ProjectionRegistration.InlineApplyDelegate CreateInlineApplyDelegate()
	{
		var projection = _projection;

		// Fast path: when all handlers are synchronous, use the simpler single-ID code path
		// that avoids Dictionary allocation and async overhead.
		if (!projection.HasAsyncHandlers)
		{
			return CreateSyncOnlyApplyDelegate(projection);
		}

		// Full path: supports both sync and async handlers, multi-ID via OverrideProjectionId (D1)
		return async (events, context, serviceProvider, cancellationToken) =>
		{
			var store = serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();
			var handlerContext = new ProjectionHandlerContext(
				context.AggregateId,
				context.AggregateType,
				context.CommittedVersion,
				context.Timestamp);

			// Multi-ID: lazily loaded projection instances keyed by projection ID (D1)
			var projections = new Dictionary<string, TProjection>(StringComparer.Ordinal);
			var defaultId = context.AggregateId;

			foreach (var @event in events)
			{
				var entry = projection.GetHandler(@event.GetType());
				if (entry is null)
				{
					continue;
				}

				var handlerEntry = entry.Value;

				// Reset OverrideProjectionId per event
				handlerContext.OverrideProjectionId = null;

				if (handlerEntry.SyncAction is not null)
				{
					// Sync handler always targets the default aggregate ID
					var state = await GetOrLoadAsync(projections, store, defaultId, cancellationToken)
						.ConfigureAwait(false);
					handlerEntry.SyncAction(state, @event);
				}
				else if (handlerEntry.AsyncHandler is not null)
				{
					// Load default projection first (handler may override ID)
					var state = await GetOrLoadAsync(projections, store, defaultId, cancellationToken)
						.ConfigureAwait(false);
					await handlerEntry.AsyncHandler(state, @event, handlerContext, serviceProvider, cancellationToken)
						.ConfigureAwait(false);

					// If handler set a custom ID, load that projection and re-invoke (D1)
					if (handlerContext.OverrideProjectionId is not null
						&& !string.Equals(handlerContext.OverrideProjectionId, defaultId, StringComparison.Ordinal))
					{
						var customId = handlerContext.OverrideProjectionId;
						var customState = await GetOrLoadAsync(projections, store, customId, cancellationToken)
							.ConfigureAwait(false);
						await handlerEntry.AsyncHandler(customState, @event, handlerContext, serviceProvider, cancellationToken)
							.ConfigureAwait(false);
					}
				}
			}

			// Upsert all projection instances that were loaded/modified (D1)
			foreach (var (id, state) in projections)
			{
				await store.UpsertAsync(id, state, cancellationToken)
					.ConfigureAwait(false);
			}
		};

		static async Task<TProjection> GetOrLoadAsync(
			Dictionary<string, TProjection> cache,
			IProjectionStore<TProjection> store,
			string id,
			CancellationToken cancellationToken)
		{
			if (!cache.TryGetValue(id, out var state))
			{
				state = await store.GetByIdAsync(id, cancellationToken)
					.ConfigureAwait(false) ?? new TProjection();
				cache[id] = state;
			}

			return state;
		}
	}

	/// <summary>
	/// Creates a simplified delegate for projections that only have sync handlers.
	/// Avoids Dictionary allocation and async overhead.
	/// </summary>
	private static ProjectionRegistration.InlineApplyDelegate CreateSyncOnlyApplyDelegate(
		MultiStreamProjection<TProjection> projection)
	{
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

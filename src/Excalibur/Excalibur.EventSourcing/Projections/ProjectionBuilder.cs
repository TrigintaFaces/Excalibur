// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch;

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
	private Func<string, CancellationToken, Task>? _deleteAction;
	private Type? _storeType;
	private ProjectionOptions? _options;
	private Func<TProjection, string>? _searchTextComputer;
	private Action<TProjection, string>? _searchTextSetter;

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
	public IProjectionBuilder<TProjection> Ephemeral()
	{
		_mode = ProjectionMode.Ephemeral;
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
	public IProjectionBuilder<TProjection> When<TEvent>(Action<TProjection, TEvent, ProjectionContext> handler)
		where TEvent : IDomainEvent
	{
		ArgumentNullException.ThrowIfNull(handler);
		_projection.AddContextHandler(handler);
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
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Assembly scanning uses MakeGenericMethod; consumers should use explicit registration for AOT scenarios.")]
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
	public IProjectionBuilder<TProjection> KeyedBy<TEvent>(Func<TEvent, string> keySelector)
		where TEvent : IDomainEvent
	{
		ArgumentNullException.ThrowIfNull(keySelector);
		_projection.AddKeySelector(keySelector);
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WithCacheTtl(TimeSpan ttl)
	{
		_cacheTtl = ttl;
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WhenDeleted(Func<string, CancellationToken, Task> deleteAction)
	{
		ArgumentNullException.ThrowIfNull(deleteAction);
		_deleteAction = deleteAction;
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WithStore<TStore>()
		where TStore : class, IProjectionStore<TProjection>
	{
		_storeType = typeof(TStore);
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WithOptions(Action<ProjectionOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);
		_options ??= new ProjectionOptions();
		configure(_options);
		return this;
	}

	/// <inheritdoc />
	public IProjectionBuilder<TProjection> WithSearchText(
		Func<TProjection, string> computeSearchText,
		Action<TProjection, string> setSearchText)
	{
		ArgumentNullException.ThrowIfNull(computeSearchText);
		ArgumentNullException.ThrowIfNull(setSearchText);
		_searchTextComputer = computeSearchText;
		_searchTextSetter = setSearchText;
		return this;
	}

	/// <summary>
	/// Gets the optional cache TTL configured via <see cref="WithCacheTtl"/>.
	/// </summary>
	internal TimeSpan? CacheTtl => _cacheTtl;

	/// <summary>
	/// Gets the delete action configured via <see cref="WhenDeleted"/>.
	/// </summary>
	internal Func<string, CancellationToken, Task>? DeleteAction => _deleteAction;

	/// <summary>
	/// Gets the store type override configured via <see cref="WithStore{TStore}"/>.
	/// </summary>
	internal Type? StoreType => _storeType;

	/// <summary>
	/// Gets the projection options configured via <see cref="WithOptions"/>.
	/// </summary>
	internal ProjectionOptions? Options => _options;

	/// <summary>
	/// Gets the search text computation function configured via <see cref="WithSearchText"/>.
	/// </summary>
	internal Func<TProjection, string>? SearchTextComputer => _searchTextComputer;

	/// <summary>
	/// Gets the search text setter configured via <see cref="WithSearchText"/>.
	/// </summary>
	internal Action<TProjection, string>? SearchTextSetter => _searchTextSetter;

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

		// Capture the generic type in a delegate at registration time (AOT-safe, no MakeGenericMethod).
		// Both Inline and Async modes need apply delegates: Inline runs during SaveAsync,
		// Async runs via the background AsyncProjectionProcessingHost.
		var inlineApply = _mode is ProjectionMode.Inline or ProjectionMode.Async
			? CreateInlineApplyDelegate()
			: null;

		// Type-erase the search text delegates for storage in the non-generic ProjectionRegistration.
		// The cast from object back to TProjection is safe because the projection engine only
		// invokes these with instances of TProjection.
		Func<object, string>? searchTextComputer = _searchTextComputer is not null
			? obj => _searchTextComputer((TProjection)obj)
			: null;
		Action<object, string>? searchTextSetter = _searchTextSetter is not null
			? (obj, text) => _searchTextSetter((TProjection)obj, text)
			: null;

		var registration = new ProjectionRegistration(
			typeof(TProjection),
			_mode,
			_projection,
			inlineApply,
			_cacheTtl,
			_deleteAction,
			_storeType,
			_options,
			searchTextComputer,
			searchTextSetter);

		registry.Register(registration);
	}

	private ProjectionRegistration.InlineApplyDelegate CreateInlineApplyDelegate()
	{
		var projection = _projection;
		var searchTextComputer = _searchTextComputer;
		var searchTextSetter = _searchTextSetter;

		// Fast path: when all handlers are synchronous (with or without context) and no
		// key selectors exist, use the simpler single-ID code path that avoids Dictionary
		// allocation and async overhead.
		if (!projection.HasAsyncHandlers)
		{
			return CreateSyncOnlyApplyDelegate(projection, searchTextComputer, searchTextSetter);
		}

		// Full path: supports both sync and async handlers, multi-ID via KeyedBy and OverrideProjectionId (D1)
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

			foreach (var @event in events)
			{
				var entry = projection.GetHandler(@event.GetType());
				if (entry is null)
				{
					continue;
				}

				var handlerEntry = entry.Value;

				// Derive projection ID: KeyedBy selector if registered, else aggregate ID
				var keySelector = projection.GetKeySelector(@event.GetType());
				var projectionId = keySelector is not null ? keySelector(@event) : context.AggregateId;

				if (string.IsNullOrEmpty(projectionId))
				{
					throw new InvalidOperationException(
						$"KeyedBy selector for event type {@event.GetType().Name} returned a null or empty projection ID.");
				}

				// Reset OverrideProjectionId per event
				handlerContext.OverrideProjectionId = null;

				if (handlerEntry.SyncAction is not null)
				{
					var state = await GetOrLoadAsync(projections, store, projectionId, cancellationToken)
						.ConfigureAwait(false);
					handlerEntry.SyncAction(state, @event);
				}
				else if (handlerEntry.SyncContextAction is not null)
				{
					var state = await GetOrLoadAsync(projections, store, projectionId, cancellationToken)
						.ConfigureAwait(false);
					handlerEntry.SyncContextAction(state, @event, ProjectionContext.Live);
				}
				else if (handlerEntry.AsyncHandler is not null)
				{
					var state = await GetOrLoadAsync(projections, store, projectionId, cancellationToken)
						.ConfigureAwait(false);
					await handlerEntry.AsyncHandler(state, @event, handlerContext, serviceProvider, cancellationToken)
						.ConfigureAwait(false);

					// OverrideProjectionId escape hatch: if handler set a DIFFERENT key, re-invoke there
					if (handlerContext.OverrideProjectionId is not null
						&& !string.Equals(handlerContext.OverrideProjectionId, projectionId, StringComparison.Ordinal))
					{
						var customId = handlerContext.OverrideProjectionId;
						var customState = await GetOrLoadAsync(projections, store, customId, cancellationToken)
							.ConfigureAwait(false);
						await handlerEntry.AsyncHandler(customState, @event, handlerContext, serviceProvider, cancellationToken)
							.ConfigureAwait(false);
					}
				}
			}

			// Compute search text once per projection instance (after all events applied)
			if (searchTextComputer is not null && searchTextSetter is not null)
			{
				foreach (var (_, state) in projections)
				{
					searchTextSetter(state, searchTextComputer(state));
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
	/// Avoids Dictionary allocation and async overhead when no key selectors are present.
	/// </summary>
	private static ProjectionRegistration.InlineApplyDelegate CreateSyncOnlyApplyDelegate(
		MultiStreamProjection<TProjection> projection,
		Func<TProjection, string>? searchTextComputer,
		Action<TProjection, string>? searchTextSetter)
	{
		// When key selectors exist, different events may target different projection IDs
		if (projection.HasKeySelectors)
		{
			return async (events, context, serviceProvider, cancellationToken) =>
			{
				var store = serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();
				var projections = new Dictionary<string, TProjection>(StringComparer.Ordinal);

				foreach (var @event in events)
				{
					if (projection.GetHandler(@event.GetType()) is null)
					{
						continue;
					}

					var keySelector = projection.GetKeySelector(@event.GetType());
					var id = keySelector is not null ? keySelector(@event) : context.AggregateId;

					if (string.IsNullOrEmpty(id))
					{
						throw new InvalidOperationException(
							$"KeyedBy selector for event type {@event.GetType().Name} returned a null or empty projection ID.");
					}

					if (!projections.TryGetValue(id, out var state))
					{
						state = await store.GetByIdAsync(id, cancellationToken)
							.ConfigureAwait(false) ?? new TProjection();
						projections[id] = state;
					}

					projection.Apply(state, @event);
				}

				// Compute search text once per projection instance (after all events applied)
				if (searchTextComputer is not null && searchTextSetter is not null)
				{
					foreach (var (_, state) in projections)
					{
						searchTextSetter(state, searchTextComputer(state));
					}
				}

				foreach (var (id, state) in projections)
				{
					await store.UpsertAsync(id, state, cancellationToken)
						.ConfigureAwait(false);
				}
			};
		}

		// Fast path: no key selectors, single projection by aggregate ID.
		// Lazy-load pattern: defer the store round-trip until the first relevant
		// event is found, then apply remaining events and upsert once.
		// This avoids ghost projections from unrelated aggregate events where
		// Apply() no-ops but the upsert would still fire with default state.
		return async (events, context, serviceProvider, cancellationToken) =>
		{
			var store = serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();
			TProjection? state = null;

			foreach (var @event in events)
			{
				if (projection.GetHandler(@event.GetType()) is null)
				{
					continue;
				}

				// Lazy-load: only hit the store when we know at least one handler matches
				state ??= await store.GetByIdAsync(context.AggregateId, cancellationToken)
					.ConfigureAwait(false) ?? new TProjection();

				projection.Apply(state, @event);
			}

			if (state is not null)
			{
				// Compute search text once after all events applied
				if (searchTextComputer is not null && searchTextSetter is not null)
				{
					searchTextSetter(state, searchTextComputer(state));
				}

				await store.UpsertAsync(context.AggregateId, state, cancellationToken)
					.ConfigureAwait(false);
			}
		};
	}
}

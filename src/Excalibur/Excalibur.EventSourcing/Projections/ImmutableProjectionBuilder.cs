// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Internal implementation of <see cref="IImmutableProjectionBuilder{TProjection}"/>.
/// Builds an immutable projection registration with factory, transform, and DI handler entries.
/// </summary>
internal sealed class ImmutableProjectionBuilder<TProjection> : IImmutableProjectionBuilder<TProjection>
	where TProjection : class
{
	private readonly IServiceCollection? _services;
	private readonly ImmutableMultiStreamProjection<TProjection> _projection = new();
	private ProjectionMode _mode = ProjectionMode.Async;
	private TimeSpan? _cacheTtl;

	internal ImmutableProjectionBuilder(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		_services = services;
	}

	/// <inheritdoc />
	public IImmutableProjectionBuilder<TProjection> Inline()
	{
		_mode = ProjectionMode.Inline;
		return this;
	}

	/// <inheritdoc />
	public IImmutableProjectionBuilder<TProjection> Async()
	{
		_mode = ProjectionMode.Async;
		return this;
	}

	/// <inheritdoc />
	public IImmutableProjectionBuilder<TProjection> WhenCreating<TEvent>(Func<TEvent, TProjection> factory)
		where TEvent : IDomainEvent
	{
		ArgumentNullException.ThrowIfNull(factory);
		_projection.AddCreatingHandler(factory);
		return this;
	}

	/// <inheritdoc />
	public IImmutableProjectionBuilder<TProjection> WhenTransforming<TEvent>(
		Func<TProjection, TEvent, TProjection> transform)
		where TEvent : IDomainEvent
	{
		ArgumentNullException.ThrowIfNull(transform);
		_projection.AddTransformingHandler(transform);
		return this;
	}

	/// <inheritdoc />
	public IImmutableProjectionBuilder<TProjection> WhenHandledBy<TEvent,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
		where TEvent : IDomainEvent
		where THandler : IImmutableProjectionHandler<TProjection, TEvent>
	{
		_projection.AddAsyncHandler<TEvent>(
			async (current, domainEvent, context, serviceProvider, cancellationToken) =>
			{
				var handler = (IImmutableProjectionHandler<TProjection, TEvent>)
					serviceProvider.GetRequiredService(typeof(THandler));
				return await handler.TransformAsync(current, (TEvent)domainEvent, context, cancellationToken)
					.ConfigureAwait(false);
			});

		_services?.TryAddTransient(typeof(THandler));
		return this;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover IImmutableProjectionHandler<T, TEvent> implementations.")]
	[UnconditionalSuppressMessage("AOT", "IL3050",
		Justification = "Assembly scanning uses MakeGenericMethod; consumers should use explicit registration for AOT scenarios.")]
	public IImmutableProjectionBuilder<TProjection> AddImmutableProjectionHandlersFromAssembly(Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(assembly);

		var handlerInterfaceType = typeof(IImmutableProjectionHandler<,>);
		var projectionType = typeof(TProjection);
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
				if (genericArgs[0] != projectionType)
				{
					continue;
				}

				var eventType = genericArgs[1];

				if (discoveredHandlers.TryGetValue(eventType, out var existing))
				{
					throw new InvalidOperationException(
						$"Duplicate immutable handler for ({projectionType.Name}, {eventType.Name}): " +
						$"both {existing.Name} and {type.Name}.");
				}

				discoveredHandlers[eventType] = type;

				var registerMethod = typeof(ImmutableProjectionBuilder<TProjection>)
					.GetMethod(nameof(RegisterScannedHandler), BindingFlags.NonPublic | BindingFlags.Instance)!
					.MakeGenericMethod(eventType, type);

				registerMethod.Invoke(this, null);
			}
		}

		return this;
	}

	[RequiresUnreferencedCode("Called via reflection during assembly scanning.")]
	private void RegisterScannedHandler<TEvent,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>()
		where TEvent : IDomainEvent
		where THandler : IImmutableProjectionHandler<TProjection, TEvent>
	{
		WhenHandledBy<TEvent, THandler>();
	}

	/// <inheritdoc />
	public IImmutableProjectionBuilder<TProjection> WithCacheTtl(TimeSpan ttl)
	{
		_cacheTtl = ttl;
		return this;
	}

	/// <summary>
	/// Builds and registers the immutable projection in the specified registry.
	/// </summary>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	internal void Build(IProjectionRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

		// Both Inline and Async modes need apply delegates: Inline runs during SaveAsync,
		// Async runs via the background AsyncProjectionProcessingHost.
		var inlineApply = _mode is ProjectionMode.Inline or ProjectionMode.Async
			? CreateImmutableInlineApplyDelegate()
			: null;

		var registration = new ProjectionRegistration(
			typeof(TProjection),
			_mode,
			_projection,
			inlineApply,
			_cacheTtl);

		registry.Register(registration);
	}

	/// <summary>
	/// Creates the inline apply delegate for immutable projections.
	/// Handles factory (WhenCreating), transform (WhenTransforming), and DI handlers.
	/// </summary>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private ProjectionRegistration.InlineApplyDelegate CreateImmutableInlineApplyDelegate()
	{
		var projection = _projection;

		return async (events, context, serviceProvider, cancellationToken) =>
		{
			var store = serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();
			var positioned = PositionedProjectionWriter<TProjection>.Resolve(store);
			var readPositions = new Dictionary<string, long?>(StringComparer.Ordinal);
			var folded = new Dictionary<string, List<ProjectionEvent>>(StringComparer.Ordinal);

			// Keyed PER AGGREGATE rather than once per call. A batch can span aggregates, and loading a
			// single state up front would fold unrelated aggregates into one projection the moment the
			// caller stops dispatching one aggregate at a time. Keying here makes the apply path
			// independent of how the caller batches, which is what lets a batch be delivered in stream
			// order instead of grouped by aggregate.
			//
			// Loaded lazily per id so an event with no matching handler cannot create a ghost projection.
			var loaded = new Dictionary<string, TProjection?>(StringComparer.Ordinal);
			var touched = new HashSet<string>(StringComparer.Ordinal);

			foreach (var projectionEvent in events)
			{
				var @event = projectionEvent.Domain;
				var entry = projection.GetHandler(@event.GetType());
				if (entry is null)
				{
					continue;
				}

				var id = projectionEvent.AggregateId;
				if (!loaded.TryGetValue(id, out var current))
				{
					// State and position as ONE observation when the store records a position.
					long? readAt = null;
					if (positioned is not null)
					{
						(current, var readAtPos) = await positioned.GetWithPositionAsync(id, cancellationToken)
							.ConfigureAwait(false);
						readAt = readAtPos.ExpectedPositionOrNull;
					}
					else
					{
						current = await store.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
					}

					loaded[id] = current;
					readPositions[id] = readAt;
					folded[id] = [];
				}

				// Skip what is already folded in, or a redelivery recomputes a state the store then
				// refuses as non-advancing and the reader never gets past the overlap.
				if (positioned is not null
					&& readPositions[id] is { } storedAt
					&& projectionEvent.GlobalPosition is { } eventPos
					&& eventPos <= storedAt)
				{
					continue;
				}

				folded[id].Add(projectionEvent);

				var handlerEntry = entry.Value;

				if (handlerEntry.CreatingFactory is not null)
				{
					// Factory: create new projection from event (replaces current if exists)
					current = handlerEntry.CreatingFactory(@event);
				}
				else if (handlerEntry.TransformingFunc is not null)
				{
					// Transform: produce new state from (current + event)
					if (current is null)
					{
						throw new InvalidOperationException(
							$"Cannot transform projection '{typeof(TProjection).Name}' for aggregate '{id}': " +
							$"no existing projection state. Use WhenCreating for the first event.");
					}

					current = handlerEntry.TransformingFunc(current, @event);
				}
				else if (handlerEntry.AsyncHandler is not null)
				{
					// Built PER EVENT: the aggregate, version and timestamp describe the event being
					// applied, and a batch spanning aggregates has no single answer for them.
					var handlerContext = new ProjectionHandlerContext(
						id,
						context.AggregateType,
						context.CommittedVersion,
						context.Timestamp,
						context.IsReplay);

					current = await handlerEntry.AsyncHandler(
						current, @event, handlerContext, serviceProvider, cancellationToken)
						.ConfigureAwait(false);
				}

				loaded[id] = current;
				_ = touched.Add(id);
			}

			foreach (var id in touched)
			{
				if (loaded[id] is not { } finalState)
				{
					continue;
				}

				// Nothing folded means nothing to record. An UNPOSITIONED fold is a different case and
				// is still written -- unconditionally, by the writer. Conflating the two silently
				// discarded every inline projection on a store that records positions.
				if (folded[id].Count == 0)
				{
					continue;
				}

				_ = await PositionedProjectionWriter<TProjection>.WriteAsync(
						store,
						positioned,
						id,
						finalState,
						readPositions[id],
						PositionedProjectionWriter<TProjection>.HighestPosition(folded[id]),
						cancellationToken)
					.ConfigureAwait(false);
			}
		};
	}
}

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
	internal void Build(IProjectionRegistry registry)
	{
		ArgumentNullException.ThrowIfNull(registry);

		var inlineApply = _mode == ProjectionMode.Inline
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
	private ProjectionRegistration.InlineApplyDelegate CreateImmutableInlineApplyDelegate()
	{
		var projection = _projection;

		return async (events, context, serviceProvider, cancellationToken) =>
		{
			var store = serviceProvider.GetRequiredService<IProjectionStore<TProjection>>();
			var defaultId = context.AggregateId;

			// Load current state (may be null for new projections -- immutable projections don't require new())
			var current = await store.GetByIdAsync(defaultId, cancellationToken)
				.ConfigureAwait(false);

			ProjectionHandlerContext? handlerContext = null;

			foreach (var @event in events)
			{
				var entry = projection.GetHandler(@event.GetType());
				if (entry is null)
				{
					continue;
				}

				var handlerEntry = entry.Value;

				if (handlerEntry.CreatingFactory is not null)
				{
					// Factory: create new projection from event (replaces current if exists)
					current = handlerEntry.CreatingFactory(@event);
				}
				else if (handlerEntry.TransformingFunc is not null)
				{
					// Transform: produce new state from (current + event)
					// Q1: null current + Transforming = throw
					if (current is null)
					{
						throw new InvalidOperationException(
							$"Cannot transform projection '{typeof(TProjection).Name}' for aggregate '{defaultId}': " +
							$"no existing projection state. Use WhenCreating for the first event.");
					}

					current = handlerEntry.TransformingFunc(current, @event);
				}
				else if (handlerEntry.AsyncHandler is not null)
				{
					// DI handler: receives nullable current, returns new state
					handlerContext ??= new ProjectionHandlerContext(
						context.AggregateId,
						context.AggregateType,
						context.CommittedVersion,
						context.Timestamp);

					current = await handlerEntry.AsyncHandler(
						current, @event, handlerContext, serviceProvider, cancellationToken)
						.ConfigureAwait(false);
				}
			}

			// Upsert the final state (only if we processed at least one event)
			if (current is not null)
			{
				await store.UpsertAsync(defaultId, current, cancellationToken)
					.ConfigureAwait(false);
			}
		};
	}
}

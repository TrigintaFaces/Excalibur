// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering inline projections and event notification
/// services on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class EventNotificationServiceCollectionExtensions
{
	/// <summary>
	/// Registers an inline projection with the event notification system.
	/// </summary>
	/// <typeparam name="TProjection">The projection state type.</typeparam>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Action to configure the projection mode, event handlers, and options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// A second call for the same projection type replaces the first (R27.37).
	/// </para>
	/// <para>
	/// If <see cref="UseEventNotification"/> has not been called, this method automatically
	/// registers the event notification infrastructure.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(builder =>
	/// {
	///     builder.AddProjection&lt;OrderSummary&gt;(p => p
	///         .Inline()
	///         .When&lt;OrderPlaced&gt;((proj, e) => { proj.Total = e.Amount; })
	///         .When&lt;OrderShipped&gt;((proj, e) => { proj.ShippedAt = e.ShippedAt; }));
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder AddProjection<TProjection>(
		this IEventSourcingBuilder builder,
		Action<IProjectionBuilder<TProjection>> configure)
		where TProjection : class, new()
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure event notification infrastructure is registered
		builder.UseEventNotification();

		// Resolve the registry (registered as singleton by UseEventNotification)
		// Build the projection and register it in the registry at startup
		builder.Services.AddSingleton<IConfigureProjection>(sp =>
		{
			return new ConfigureProjection<TProjection>(
				sp.GetRequiredService<IProjectionRegistry>(), configure);
		});

		return builder;
	}

	/// <summary>
	/// Scans the specified assembly for types implementing
	/// <see cref="IProjectionConfiguration{TProjection}"/> and registers each
	/// discovered projection via <see cref="AddProjection{TProjection}"/>.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="assembly">The assembly to scan for projection configurations.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Assemblies with no <see cref="IProjectionConfiguration{TProjection}"/> implementations
	/// are handled gracefully (no-op). This is consistent with the existing
	/// <see cref="ProjectionHandlerServiceCollectionExtensions.AddProjectionHandlersFromAssembly"/>
	/// pattern.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(builder =&gt;
	/// {
	///     builder.AddProjectionsFromAssembly(typeof(OrderSummary).Assembly);
	/// });
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover IProjectionConfiguration<T> implementations.")]
	public static IEventSourcingBuilder AddProjectionsFromAssembly(
		this IEventSourcingBuilder builder,
		Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(assembly);

		var configurationType = typeof(IProjectionConfiguration<>);

		foreach (var type in assembly.GetTypes())
		{
			if (type.IsAbstract || type.IsInterface || !type.IsClass || type.IsGenericTypeDefinition)
			{
				continue;
			}

			foreach (var iface in type.GetInterfaces())
			{
				if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != configurationType)
				{
					continue;
				}

				// Extract TProjection from IProjectionConfiguration<TProjection>
				var projectionType = iface.GetGenericArguments()[0];

				// Create an instance of the configuration class.
				// Activator.CreateInstance(Type) is banned globally but assembly scanning
				// is inherently reflection-based (already marked [RequiresUnreferencedCode]).
#pragma warning disable RS0030 // Assembly scanning requires dynamic instantiation
				var config = Activator.CreateInstance(type)
#pragma warning restore RS0030
					?? throw new InvalidOperationException(
						$"Failed to create instance of {type.Name}. " +
						$"IProjectionConfiguration<T> implementations must have a parameterless constructor.");

				// Call AddProjection<TProjection> via reflection to register
				// the projection with the correct generic type
				var addProjectionMethod = typeof(EventNotificationServiceCollectionExtensions)
					.GetMethod(nameof(AddProjectionFromConfiguration),
						BindingFlags.NonPublic | BindingFlags.Static)!
					.MakeGenericMethod(projectionType);

				addProjectionMethod.Invoke(null, [builder, config]);
			}
		}

		return builder;
	}

	/// <summary>
	/// Helper to register a discovered <see cref="IProjectionConfiguration{TProjection}"/>
	/// via the existing <see cref="AddProjection{TProjection}"/> path.
	/// </summary>
	private static void AddProjectionFromConfiguration<TProjection>(
		IEventSourcingBuilder builder,
		object config)
		where TProjection : class, new()
	{
		var typedConfig = (IProjectionConfiguration<TProjection>)config;
		builder.AddProjection<TProjection>(typedConfig.Configure);
	}

	/// <summary>
	/// Registers the <see cref="IProjectionRecovery"/> service for recovering
	/// inline projections that failed after events were committed.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Called automatically by <see cref="UseEventNotification"/>. Consumers who
	/// only want recovery without full notification can call this directly.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(builder =&gt;
	/// {
	///     builder.UseProjectionRecovery();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseProjectionRecovery(
		this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<IProjectionRecovery>(sp =>
			new ProjectionRecoveryService(
				sp.GetRequiredService<IProjectionRegistry>(),
				sp.GetRequiredKeyedService<IEventStore>("default"),
				sp.GetRequiredService<Excalibur.Dispatch.Abstractions.IEventSerializer>(),
				sp,
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ProjectionRecoveryService>>()));

		return builder;
	}

	/// <summary>
	/// Registers the event notification broker, inline projection processor,
	/// and projection registry. Called automatically by <see cref="AddProjection{TProjection}"/>.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IEventSourcingBuilder UseEventNotification(
		this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Idempotent: only register once
		builder.Services.TryAddSingleton<IProjectionRegistry, InMemoryProjectionRegistry>();
		builder.Services.TryAddSingleton<InlineProjectionProcessor>();
		builder.Services.TryAddSingleton<IEventNotificationBroker, EventNotificationBroker>();
		builder.Services.TryAddSingleton<ICursorMapStore, InMemoryCursorMapStore>();
		builder.UseProjectionRecovery();

		// Observability: metrics, health state, health check
		builder.Services.TryAddSingleton<Excalibur.EventSourcing.Projections.ProjectionHealthState>();
		builder.Services.TryAddSingleton<Excalibur.EventSourcing.Diagnostics.ProjectionObservability>();
		builder.Services.AddHealthChecks()
			.AddCheck<Excalibur.EventSourcing.Health.ProjectionHealthCheck>("projections");

		builder.Services.TryAddSingleton<IEphemeralProjectionEngine>(sp =>
			new EphemeralProjectionEngine(
				sp.GetRequiredKeyedService<IEventStore>("default"),
				sp.GetRequiredService<Excalibur.Dispatch.Abstractions.IEventSerializer>(),
				sp.GetRequiredService<IProjectionRegistry>(),
				sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EphemeralProjectionEngine>>(),
				sp.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>()));

		return builder;
	}

	/// <summary>
	/// Internal interface for deferred projection registration at startup.
	/// </summary>
	internal interface IConfigureProjection
	{
		void Configure();
	}

	/// <summary>
	/// Captures the projection configuration to apply when the DI container resolves it.
	/// </summary>
	internal sealed class ConfigureProjection<TProjection> : IConfigureProjection
		where TProjection : class, new()
	{
		private readonly IProjectionRegistry _registry;
		private readonly Action<IProjectionBuilder<TProjection>> _configure;
		private bool _configured;

		internal ConfigureProjection(
			IProjectionRegistry registry,
			Action<IProjectionBuilder<TProjection>> configure)
		{
			_registry = registry;
			_configure = configure;
		}

		public void Configure()
		{
			if (_configured)
			{
				return;
			}

			_configured = true;
			var projectionBuilder = new ProjectionBuilder<TProjection>(_registry);
			_configure(projectionBuilder);
			projectionBuilder.Build();
		}
	}
}

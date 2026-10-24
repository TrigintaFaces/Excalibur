// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering projections and event notification
/// services on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class EventNotificationServiceCollectionExtensions
{
	/// <summary>
	/// Registers a projection with the event notification system.
	/// </summary>
	/// <typeparam name="TProjection">The projection state type.</typeparam>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Action to configure the projection mode, event handlers, and options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// A second call for the same projection type replaces the first.
	/// </para>
	/// <para>
	/// If <see cref="UseEventNotification"/> has not been called, this method automatically
	/// registers the event notification infrastructure.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(builder =>
	/// {
	///     builder.AddProjection&lt;OrderSummary&gt;(p => p
	///         .Async()
	///         .When&lt;OrderPlaced&gt;((proj, e) => { proj.Total = e.Amount; })
	///         .When&lt;OrderShipped&gt;((proj, e) => { proj.ShippedAt = e.ShippedAt; }));
	/// }));
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public static IEventSourcingBuilder AddProjection<TProjection>(
		this IEventSourcingBuilder builder,
		Action<IProjectionBuilder<TProjection>> configure)
		where TProjection : class, new()
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure event notification infrastructure is registered
		builder.UseEventNotification();

		// Run configure eagerly: WhenHandledBy registers handler types in DI via
		// TryAddTransient, which must happen before the container is built.
		// The projection registration itself is deferred to startup.
		var projectionBuilder = new ProjectionBuilder<TProjection>(builder.Services);
		configure(projectionBuilder);
		if (projectionBuilder.Mode == ProjectionMode.Inline)
		{
			InlineProjectionCoordinator.Register(builder.Services);
		}

		builder.Services.AddSingleton<IConfigureProjection>(sp =>
		{
			return new ConfigureProjection<TProjection>(
				sp.GetRequiredService<IProjectionRegistry>(), projectionBuilder);
		});

		// Closed over TProjection here, where the type is known, so the startup check needs no
		// reflection and stays AOT-safe. It costs nothing unless the host requires positioned writes.
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IPositionedProjectionProbe, PositionedProjectionProbe<TProjection>>());

		// The same shape for the store itself, and NOT opt-in: a projection registered with nowhere to
		// store it folds into nothing, which is wrong on every host. TryAddEnumerable de-duplicates on
		// the implementation type, so the validator lands once however many projections are registered.
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IProjectionStoreProbe>(
				new ProjectionStoreProbe<TProjection>(projectionBuilder.Mode)));
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, ProjectionStoreValidator>());
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, ProjectionStoreValidator>());

		return builder;
	}

    /// <summary>Registers a projection using generated metadata and an AOT-safe store capability.</summary>
    /// <typeparam name="TProjection">Projection state type.</typeparam>
    /// <param name="builder">Event sourcing builder.</param>
    /// <param name="typeInfo">The same generated metadata instance supplied by the store's JSON context.</param>
    /// <param name="configure">Projection handlers and processing mode.</param>
    /// <returns>The builder.</returns>
    /// <remarks>Unsupported stores or decorators are rejected; this overload never falls back to reflective persistence.</remarks>
	public static IEventSourcingBuilder AddProjection<TProjection>(
		this IEventSourcingBuilder builder,
		JsonTypeInfo<TProjection> typeInfo,
        Action<IProjectionBuilder<TProjection>> configure)
		where TProjection : class, new()
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(typeInfo);

		// Ensure event notification infrastructure is registered
		builder.UseEventNotification();

		// Run configure eagerly: WhenHandledBy registers handler types in DI via
		// TryAddTransient, which must happen before the container is built.
		// The projection registration itself is deferred to startup.
		var projectionBuilder = new ProjectionBuilder<TProjection>(builder.Services);
		configure(projectionBuilder);
        if (projectionBuilder.Mode == ProjectionMode.Ephemeral)
        {
            throw new NotSupportedException("Generated projection registration supports Inline and Async persistence. Ephemeral caching does not yet consume generated metadata.");
        }

		if (projectionBuilder.Mode == ProjectionMode.Inline)
		{
			InlineProjectionCoordinator.Register(builder.Services);
		}

		builder.Services.AddSingleton<IConfigureProjection>(sp =>
		{
			return new ConfigureProjection<TProjection>(
				() => projectionBuilder.BuildGenerated(sp.GetRequiredService<IProjectionRegistry>(), typeInfo));
		});

		// Closed over TProjection here, where the type is known, so the startup check needs no
		// reflection and stays AOT-safe. It costs nothing unless the host requires positioned writes.
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IPositionedProjectionProbe, PositionedProjectionProbe<TProjection>>());

		// The same shape for the store itself, and NOT opt-in: a projection registered with nowhere to
		// store it folds into nothing, which is wrong on every host. TryAddEnumerable de-duplicates on
		// the implementation type, so the validator lands once however many projections are registered.
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IProjectionStoreProbe>(
				new ProjectionStoreProbe<TProjection>(projectionBuilder.Mode)));
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, ProjectionStoreValidator>());
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, ProjectionStoreValidator>());

        if (projectionBuilder.Mode != ProjectionMode.Ephemeral)
        {
            builder.Services.AddSingleton<IGeneratedProjectionValidator>(sp => new GeneratedProjectionValidator<TProjection>(sp, typeInfo));
        }
        return builder;
	}

	/// <summary>
	/// Registers a projection using a DI-resolved <see cref="IProjectionConfiguration{TProjection}"/>
	/// implementation. The configuration class is resolved from the service provider at startup
	/// to configure the projection's mode, event handlers, and options.
	/// </summary>
	/// <typeparam name="TProjection">The projection state type.</typeparam>
	/// <typeparam name="TConfig">
	/// The configuration type implementing <see cref="IProjectionConfiguration{TProjection}"/>.
	/// Must have a public parameterless constructor or be pre-registered in DI.
	/// </typeparam>
	/// <param name="builder">The event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the preferred registration path for projections when using the class-based
	/// configuration pattern. It avoids reflection-based assembly scanning while supporting
	/// DI-injected configuration classes.
	/// </para>
	/// <para>
	/// A second call for the same projection type replaces the first.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(builder =>
	/// {
	///     builder.AddProjection&lt;OrderSummary, OrderSummaryProjectionConfig&gt;();
	/// }));
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public static IEventSourcingBuilder AddProjection<TProjection, TConfig>(
		this IEventSourcingBuilder builder)
		where TProjection : class, new()
		where TConfig : class, IProjectionConfiguration<TProjection>, new()
	{
		ArgumentNullException.ThrowIfNull(builder);

		var config = new TConfig();
		return builder.AddProjection<TProjection>(b => config.Configure(b));
	}

	/// <summary>
	/// Scans the specified assembly for types implementing
	/// <see cref="IProjectionConfiguration{TProjection}"/> and registers each
	/// discovered projection via <see cref="AddProjection{TProjection}(IEventSourcingBuilder, Action{IProjectionBuilder{TProjection}})"/>.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="assembly">The assembly to scan for projection configurations.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Assemblies with no <see cref="IProjectionConfiguration{TProjection}"/> implementations
	/// are handled gracefully (no-op).
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(builder =&gt;
	/// {
	///     builder.AddProjectionsFromAssembly(typeof(OrderSummary).Assembly);
	/// }));
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover IProjectionConfiguration<T> implementations.")]
	[RequiresDynamicCode("Uses Activator.CreateInstance and MethodInfo.MakeGenericMethod for dynamic projection registration.")]
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

				// Create an instance of the configuration class. ActivatorUtilities.CreateInstance cannot
				// substitute here: this runs against an IServiceCollection during composition, so there is no
				// IServiceProvider to resolve from, and the type is discovered by scanning rather than named
				// by the caller - which is the untraceable case the ban exists for. The API is annotated
				// RequiresUnreferencedCode/RequiresDynamicCode so the consumer sees the requirement.
#pragma warning disable RS0030
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
	/// via the existing <see cref="AddProjection{TProjection}(IEventSourcingBuilder, Action{IProjectionBuilder{TProjection}})"/> path.
	/// </summary>
	[RequiresUnreferencedCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	private static void AddProjectionFromConfiguration<TProjection>(
		IEventSourcingBuilder builder,
		object config)
		where TProjection : class, new()
	{
		var typedConfig = (IProjectionConfiguration<TProjection>)config;
		builder.AddProjection<TProjection>(b => typedConfig.Configure(b));
	}

	/// <summary>
	/// Scans the specified assembly for types implementing
	/// <see cref="IEventNotificationHandler{TEvent}"/> and registers each discovered
	/// handler via <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="assembly">The assembly to scan for notification handler implementations.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// This provides consistency with <see cref="AddProjectionsFromAssembly"/> by allowing
	/// convention-based registration of notification handlers.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(builder =&gt;
	/// {
	///     builder.AddEventNotificationHandlersFromAssembly(typeof(OrderNotificationHandler).Assembly);
	/// }));
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Assembly scanning uses reflection to discover IEventNotificationHandler<T> implementations.")]
	[RequiresDynamicCode("Implementations serialize the projection type reflectively; supply JsonSerializerOptions with a source-generated resolver for trimming and AOT.")]
	public static IEventSourcingBuilder AddEventNotificationHandlersFromAssembly(
		this IEventSourcingBuilder builder,
		Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(assembly);

		var handlerInterfaceType = typeof(IEventNotificationHandler<>);

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

				// Register: IEventNotificationHandler<TEvent> -> ConcreteHandler
				builder.Services.TryAddEnumerable(ServiceDescriptor.Transient(iface, type));
			}
		}

		// Ensure notification infrastructure is registered
		builder.UseEventNotification();

		return builder;
	}

	/// <summary>
	/// Registers the <see cref="IProjectionRecovery"/> service, which re-folds one aggregate's
	/// projection row from its event stream on demand.
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
	/// services.AddExcalibur(x => x.AddEventSourcing(builder =&gt;
	/// {
	///     builder.UseProjectionRecovery();
	/// }));
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseProjectionRecovery(
		this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ProjectionRecoveryService>(sp =>
			new ProjectionRecoveryService(
				sp.GetRequiredService<IProjectionRegistry>(),
				sp.GetRequiredKeyedService<IEventStore>("default"),
				sp.GetRequiredService<Excalibur.Dispatch.IEventSerializer>(),
				sp,
				sp.GetRequiredService<Logging.ILogger<ProjectionRecoveryService>>()));
        builder.Services.TryAddSingleton<IProjectionRecovery>(sp => sp.GetRequiredService<ProjectionRecoveryService>());

		return builder;
	}

	/// <summary>
	/// Registers the event notification broker and the projection registry.
	/// Called automatically by <see cref="AddProjection{TProjection}(IEventSourcingBuilder, Action{IProjectionBuilder{TProjection}})"/>.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IEventSourcingBuilder UseEventNotification(
		this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Idempotent: only register once
		builder.Services.TryAddSingleton<IProjectionRegistry, InMemoryProjectionRegistry>();
		_ = builder.Services.AddDefaultTenantContext();
		builder.Services.TryAddSingleton<IEventNotificationBroker, EventNotificationBroker>();
		builder.Services.TryAddSingleton<ICursorMapStore, InMemoryCursorMapStore>();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Options.IValidateOptions<EventNotificationOptions>, EventNotificationOptionsValidator>());
		builder.UseProjectionRecovery();

		// The read half of session consistency. Registered unconditionally and costing nothing until a
		// caller passes a token: a read with no token never waits and never touches the checkpoint store.
		builder.Services.TryAddSingleton<IProjectionSessionReader, ProjectionSessionReader>();
		_ = builder.Services.AddOptions<ProjectionSessionOptions>().ValidateOnStart();
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Options.IValidateOptions<ProjectionSessionOptions>, ProjectionSessionOptionsValidator>());

		// Observability: metrics, health state
		//
		// AddMetrics() is OURS to call, not the consumer's. ProjectionObservability takes IMeterFactory
		// as a required constructor parameter, and a generic host happens to register one -- so this
		// resolved for anyone using WebApplication.CreateBuilder and threw for anyone composing a
		// ServiceCollection by hand, which is the shape a test or a worker uses. A library that depends
		// on IMeterFactory ensures it; the call is idempotent, so a consumer who already called it is
		// unaffected.
		_ = builder.Services.AddMetrics();
		builder.Services.TryAddSingleton<ProjectionHealthState>();
		builder.Services.TryAddSingleton<Excalibur.EventSourcing.Diagnostics.ProjectionObservability>();

		builder.Services.TryAddSingleton<IEphemeralProjectionEngine>(sp =>
			new EphemeralProjectionEngine(
				sp.GetRequiredKeyedService<IEventStore>("default"),
				sp.GetRequiredService<Excalibur.Dispatch.IEventSerializer>(),
				sp.GetRequiredService<IProjectionRegistry>(),
				sp.GetRequiredService<Logging.ILogger<EphemeralProjectionEngine>>(),
				// The SCOPE FACTORY, not the root provider: the engine is a singleton and its handlers
				// may take scoped dependencies, which need a scope per build rather than the root's
				// lifetime.
				sp.GetRequiredService<IServiceScopeFactory>(),
				sp.GetService<Caching.Distributed.IDistributedCache>(),
				sp.GetService<System.Text.Json.JsonSerializerOptions>()));

		return builder;
	}

	/// <summary>
	/// Adds a health check for projections.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers a health check named <c>"projections"</c> that monitors projection lag
	/// and error state. This is opt-in — projections work without health checks.
	/// </para>
	/// <para>
	/// To customize thresholds, configure <see cref="Excalibur.EventSourcing.Health.ProjectionHealthCheckOptions"/>
	/// via the options pattern.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// builder.Services.AddEventSourcing(es =>
	/// {
	///     es.AddProjection&lt;OrderSummary&gt;()
	///       .WithProjectionHealthChecks();
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder WithProjectionHealthChecks(this IEventSourcingBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		if (!builder.Services.Any(d => d.ServiceType == typeof(Excalibur.EventSourcing.Health.ProjectionHealthCheck)))
		{
			_ = builder.Services.AddOptions<Excalibur.EventSourcing.Health.ProjectionHealthCheckOptions>().ValidateOnStart();
			builder.Services.TryAddEnumerable(
				ServiceDescriptor.Singleton<
					Options.IValidateOptions<Excalibur.EventSourcing.Health.ProjectionHealthCheckOptions>,
					Excalibur.EventSourcing.Health.ProjectionHealthCheckOptionsValidator>());
			builder.Services.AddSingleton<Excalibur.EventSourcing.Health.ProjectionHealthCheck>();
			builder.Services.AddHealthChecks()
				.AddCheck<Excalibur.EventSourcing.Health.ProjectionHealthCheck>("projections");
		}

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
	/// Captures a pre-configured projection builder and registers it in the
	/// projection registry at startup.
	/// </summary>
	internal sealed class ConfigureProjection<TProjection> : IConfigureProjection
		where TProjection : class, new()
	{
		private readonly Action _configure;
		private readonly Lock _configureGate = new();
		private bool _configured;

        [RequiresUnreferencedCode("Legacy projection stores may serialize reflectively.")]
        [RequiresDynamicCode("Legacy projection stores may serialize reflectively.")]
        internal ConfigureProjection(IProjectionRegistry registry, ProjectionBuilder<TProjection> projectionBuilder)
            : this(() => projectionBuilder.Build(registry)) { }

        internal ConfigureProjection(Action configure) => _configure = configure;

		public void Configure()
		{
			lock (_configureGate)
			{
				if (_configured)
				{
					return;
				}

				_configure();
				_configured = true;
			}
		}
	}
}

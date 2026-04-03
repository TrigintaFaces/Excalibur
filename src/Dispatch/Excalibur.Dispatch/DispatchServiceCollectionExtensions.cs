// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.TypeResolution;
using Excalibur.Dispatch.ZeroAlloc;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection" /> to register dispatch messaging services.
/// </summary>
public static class DispatchServiceCollectionExtensions
{
	/// <summary>
	/// Registers the core Dispatch services and middleware pipeline.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "Handler activator and LocalMessageBus use reflection for dispatch plan construction; AOT paths rely on source-generated handlers and precompiled activators.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "HandlerActivator and LocalMessageBus require dynamic code for typed invoker construction. In AOT scenarios, source-generated dispatchers bypass these code paths.")]
	public static IServiceCollection AddDispatchPipeline(this IServiceCollection services)
	{
		services.TryAddSingleton<IMessageBusProvider, MessageBusProvider>();
		services.TryAddSingleton<IMessageContextAccessor, MessageContextAccessor>();
		services.TryAddSingleton<IMessageContextPool>(static sp => new MessageContextPool(sp));
		services.TryAddSingleton<IMessageContextFactory>(static sp =>
			new PooledMessageContextFactory(sp.GetRequiredService<IMessageContextPool>()));
		services.TryAddSingleton<IMiddlewareApplicabilityStrategy, DefaultMiddlewareApplicabilityStrategy>();
		services.TryAddSingleton<IPipelineProfileRegistry, PipelineProfileRegistry>();

		// Transport context resolution - enables pipeline profile selection based on message origin
		services.TryAddSingleton<TransportBindingRegistry>();
		services.TryAddSingleton<ITransportContextProvider, TransportContextProvider>();

		services.TryAddSingleton<IDispatcher, Dispatcher>();
		services.TryAddSingleton<IStreamingDispatcher>(static sp =>
			(IStreamingDispatcher)sp.GetRequiredService<IDispatcher>());
		services.TryAddSingleton<IProgressDispatcher>(static sp =>
			(IProgressDispatcher)sp.GetRequiredService<IDispatcher>());
		services.TryAddSingleton<IDirectLocalDispatcher>(static sp =>
			(IDirectLocalDispatcher)sp.GetRequiredService<IDispatcher>());
		// Legacy fallback: discovers middleware from DI via GetServices<IDispatchMiddleware>().
		// When DispatchBuilder.Build() is called (the modern path), these TryAdd registrations
		// are replaced via Services.Replace() with builder-materialized middleware.
		services.TryAddSingleton<IDispatchPipeline>(sp => new DispatchPipeline(
			sp.GetServices<IDispatchMiddleware>(),
			sp.GetRequiredService<IMiddlewareApplicabilityStrategy>()));
		services.TryAddSingleton<IDispatchMiddlewareInvoker>(sp => new DispatchMiddlewareInvoker(
			sp.GetServices<IDispatchMiddleware>(),
			sp.GetRequiredService<IMiddlewareApplicabilityStrategy>()));
		services.TryAddSingleton<IDictionary<string, MessageBusOptions>>(static _ =>
			new Dictionary<string, MessageBusOptions>(StringComparer.Ordinal));
		services.TryAddSingleton<IRetryPolicy>(static _ => NoOpRetryPolicy.Instance);
		services.TryAddSingleton<FinalDispatchHandler>();

		// Configure handler invocation based on AOT requirements
		services.ConfigureHandlerInvoker();

		// Lean default path for all targets: HandlerActivator supports precompiled context injection when available.
		services.TryAddSingleton<IHandlerActivator, HandlerActivator>();
		services.TryAddSingleton<DispatchJsonSerializer>();

		// Default no-op telemetry sanitizer — overridden by AddDispatchObservability() with HashingTelemetrySanitizer
		services.TryAddSingleton<Excalibur.Dispatch.Abstractions.Telemetry.ITelemetrySanitizer>(
			static _ => Excalibur.Dispatch.Abstractions.Telemetry.NullTelemetrySanitizer.Instance);

		services.TryAddSingleton<LocalMessageBus>();
		_ = services.AddMessageBus(
			"Local",
			isRemote: false,
			static sp => sp.GetRequiredService<LocalMessageBus>());

		// Note: Routing functionality will be registered when AddDispatchRouting() is called explicitly This allows pay-for-play routing
		// configuration based on requirements
		return services;
	}

	/// <summary>
	/// Registers dispatch handlers found in the provided assemblies.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="assembliesToScan"> Assemblies containing handlers or <c> null </c>. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <c> null </c>. </exception>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Handler registration uses source generation when USE_SOURCE_GENERATION is defined, falls back to reflection otherwise.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
		Justification = "Handler warmup uses expression compilation. In AOT scenarios, source-generated dispatchers bypass these code paths.")]
	public static IServiceCollection AddDispatchHandlers(this IServiceCollection services, params Assembly[]? assembliesToScan)
	{
		ArgumentNullException.ThrowIfNull(services);

		var assemblies = assembliesToScan ?? [];

		services.TryAddSingleton<IHandlerRegistry>(serviceProvider =>
		{
			var registry = new HandlerRegistry();
			HandlerRegistryBootstrapper.Bootstrap(registry, assemblies);

			// Also register handlers that were manually added to DI
			var handlerInterfaces = new[]
			{
				typeof(IActionHandler<>), typeof(IActionHandler<,>), typeof(IEventHandler<>), typeof(IDocumentHandler<>),
			};

			foreach (var descriptor in services)
			{
				if (descriptor.ServiceType.IsGenericType)
				{
					var genericDef = descriptor.ServiceType.GetGenericTypeDefinition();
					if (handlerInterfaces.Contains(genericDef))
					{
						var messageType = descriptor.ServiceType.GetGenericArguments()[0];
						var handlerType = descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType();

						if (handlerType is { IsAbstract: false, IsInterface: false })
						{
							var expectsResponse = genericDef == typeof(IActionHandler<,>);
							registry.Register(messageType, handlerType, expectsResponse);
						}
					}
				}
			}

			if (registry is HandlerRegistry concreteRegistry)
			{
				concreteRegistry.PrecomputeSnapshots();
			}

			var registryEntries = registry.GetAll();
			var handlerTypes = registryEntries
				.Select(static entry => entry.HandlerType)
				.Distinct()
				.ToArray();

			HandlerActivator.PreWarmCache(handlerTypes);
			HandlerActivator.PreBindResolutionModes(serviceProvider, handlerTypes);
			HandlerInvoker.PreWarmGeneratedInvokerCache(registryEntries);

			return registry;
		});

		RegisterMessageHandlers(services, assemblies);

		return services;
	}

	/// <summary>
	/// Registers the core Dispatch pipeline and message handlers.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="assembliesToScan"> Assemblies containing handlers or <c> null </c>. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <c> null </c>. </exception>
	public static IServiceCollection AddDispatch(this IServiceCollection services, params Assembly[]? assembliesToScan)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Ensure MessageTypeResolver is initialized
		_ = MessageTypeResolver.Instance;

		var assemblies = assembliesToScan ?? [];

		_ = services.AddDispatchPipeline();
		_ = services.AddDispatchHandlers(assemblies);

		// Guard: if a builder-based AddDispatch(configure) already called Build(),
		// skip — the builder path already materialized the pipeline.
		if (services.Any(static d => d.ServiceType == typeof(DispatchBuilderSentinel)))
		{
			return services;
		}

		// Apply default performance promotion without calling Build().
		// Build() replaces IDispatchMiddlewareInvoker with a builder-materialized snapshot,
		// which would prevent any middleware registered later (via AddDispatchMiddleware<T>())
		// from being discovered by the legacy GetServices<IDispatchMiddleware>() path.
		_ = services.Configure<DispatchOptions>(static opt =>
			opt.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = true);

		return services;
	}

	/// <summary>
	/// Registers the core Dispatch pipeline and allows configuration via a builder action.
	/// This is the recommended primary entry point for Dispatch configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> An optional action to configure the <see cref="IDispatchBuilder" />. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <c> null </c>. </exception>
	/// <example>
	/// <code>
	/// // Simple usage (no configuration needed)
	/// services.AddDispatch();
	///
	/// // With configuration
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	///     dispatch.AddPipeline("default", pipeline => pipeline.UseValidation());
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddDispatch(
		this IServiceCollection services,
		Action<IDispatchBuilder>? configure)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Ensure MessageTypeResolver is initialized
		_ = MessageTypeResolver.Instance;

		// Add core pipeline
		_ = services.AddDispatchPipeline();

		// Ensure IHandlerRegistry is registered (required by LocalMessageBus)
		// This allows the builder pattern to work without explicitly calling AddHandlersFromAssembly
		_ = services.AddDispatchHandlers();

		// Mark that a builder-based configuration was applied, preventing subsequent
		// parameterless AddDispatch() calls from overwriting the middleware invoker.
		services.TryAddSingleton<DispatchBuilderSentinel>();

		// Create builder and apply default performance promotion BEFORE configure,
		// so consumers can opt out via configure action if desired.
		using var builder = new DispatchBuilder(services);
		EnableDefaultPerformancePromotion(builder);
		configure?.Invoke(builder);

		// Zero-config: auto-scan entry assembly for handlers if none were registered
		if (!builder.HasHandlerRegistrations)
		{
			var entryAssembly = Assembly.GetEntryAssembly();
			if (entryAssembly != null)
			{
				builder.AddHandlersFromAssembly(entryAssembly);
			}
		}

		// Materialize pipelines — without this call, ConfigurePipeline() configurations are silently lost
		_ = builder.Build();

		return services;
	}

	/// <summary>
	/// Registers the Dispatch pipeline with sensible defaults and handler discovery from the specified assembly.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="handlerAssembly">The assembly to scan for message handlers.</param>
	/// <returns>The configured <see cref="IServiceCollection"/>.</returns>
	/// <remarks>
	/// <para>
	/// This is a convenience method that registers Dispatch with a standard middleware stack:
	/// validation, logging, timeout, retry, and exception mapping.
	/// </para>
	/// <para>
	/// Equivalent to:
	/// <code>
	/// services.AddDispatch(dispatch => dispatch
	///     .AddHandlersFromAssembly(handlerAssembly)
	///     .WithDefaults());
	/// </code>
	/// </para>
	/// <para>
	/// For full control over middleware composition, use <see cref="AddDispatch(IServiceCollection, Action{IDispatchBuilder}?)"/> instead.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddDispatchWithDefaults(
		this IServiceCollection services,
		Assembly handlerAssembly)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(handlerAssembly);

		return services.AddDispatch(dispatch => dispatch
			.AddHandlersFromAssembly(handlerAssembly)
			.WithDefaults());
	}

	private static void EnableDefaultPerformancePromotion(IDispatchBuilder builder)
	{
		builder.WithOptions(options =>
			options.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = true);
	}

	/// <summary>
	/// Registers a dispatch middleware component if it has not already been registered.
	/// </summary>
	/// <typeparam name="TMiddleware"> Middleware type. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	public static IServiceCollection AddDispatchMiddleware<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TMiddleware>(this IServiceCollection services)
		where TMiddleware : class, IDispatchMiddleware
	{
		services.TryAddSingleton<TMiddleware>();

		// Also register as IDispatchMiddleware so the legacy GetServices<IDispatchMiddleware>()
		// discovery path (used when DispatchBuilder.Build() hasn't run) can find it.
		_ = services.AddSingleton<IDispatchMiddleware>(static sp => sp.GetRequiredService<TMiddleware>());

		return services;
	}

	/// <summary>
	/// Uses reflection to locate all message handler implementations in the provided assemblies. Scanning large sets of assemblies may slow
	/// start up, so callers should pass only those that actually contain handlers when invoking AddExcalibur.Dispatch.
	/// </summary>
	[RequiresUnreferencedCode("Uses reflection to scan assemblies for handler implementations")]
	private static void RegisterMessageHandlers(IServiceCollection services, Assembly[] assemblies)
	{
		// Build a list of concrete types implementing the handler interfaces
		var handlerTypes = assemblies
			.SelectMany(static a => a.GetTypes())
			.Where(static t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
			.Select(static t => new
			{
				Type = t,
				Interfaces = t.GetInterfaces()
					.Where(static i =>
						i.IsGenericType &&
						(
							i.GetGenericTypeDefinition() == typeof(IActionHandler<,>) ||
							i.GetGenericTypeDefinition() == typeof(IActionHandler<>) ||
							i.GetGenericTypeDefinition() == typeof(IEventHandler<>) ||
							i.GetGenericTypeDefinition() == typeof(IDocumentHandler<>)
						)),
			})
			.Where(static x => x.Interfaces.Any());

		// Register each handler against the DI container
		foreach (var handler in handlerTypes)
		{
			// Register the handler type itself so the activator can resolve it
			services.TryAddTransient(handler.Type);

			// Register the handler for each interface it implements
			foreach (var iface in handler.Interfaces)
			{
				services.TryAddTransient(iface, handler.Type);
			}
		}
	}

	/// <summary>
	/// Internal marker to detect that a builder-based AddDispatch(configure) has been called.
	/// Prevents subsequent parameterless AddDispatch() from overwriting the configured middleware invoker.
	/// </summary>
	internal sealed class DispatchBuilderSentinel;
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Performance;
using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.TypeResolution;
using Excalibur.Dispatch.ZeroAlloc;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Excalibur.Dispatch.Extensions;

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
	[SuppressMessage(
		"Maintainability",
		"CA1506:AvoidExcessiveClassCoupling",
		Justification =
			"This is the pipeline composition root, so it necessarily references every component it registers.")]
	public static IServiceCollection AddDispatchPipeline(this IServiceCollection services)
	{
		// Registered here rather than left to the consumer because pipeline components resolve it —
		// CircuitBreakerMiddleware times its open-duration deadline from it. `AddSystemTimeProvider` existed
		// but had NO callers, so nothing put a TimeProvider in the container and any component depending on
		// one would have failed to resolve. TryAdd, so a test or a consumer can substitute a fake clock.
		RegisterTimeProvider(services);

		services.TryAddSingleton<IMessageBusProvider, MessageBusProvider>();
		services.TryAddSingleton<IMessageContextAccessor, MessageContextAccessor>();
		services.TryAddSingleton<IMessageContextPool>(static sp => new MessageContextPool(sp));
		services.TryAddSingleton<IMessageContextFactory>(static sp =>
			new PooledMessageContextFactory(sp.GetRequiredService<IMessageContextPool>()));
		services.TryAddSingleton<IMiddlewareApplicabilityStrategy, DefaultMiddlewareApplicabilityStrategy>();
		services.TryAddSingleton<IPipelineProfileRegistry, PipelineProfileRegistry>();

		// Cache freezing. PerformanceOptions.AutoFreezeOnStart defaults to TRUE and the handler doc
		// comments tell consumers freezing happens automatically at startup, so both the manager and the
		// hosted service that acts on that option have to be in the container: without them the option is
		// settable, the promise is documented, and nothing ever freezes.
		services.TryAddSingleton<IDispatchCacheManager>(static sp => new DispatchCacheManager(
			sp.GetService<ILogger<DispatchCacheManager>>(),
			freezeLockTimeout: null));
		// Constructed by factory, not by the activator: the lifetime is optional (a container composed
		// without a generic host has none) and the activator cannot supply a missing constructor argument.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DispatchCacheOptimizationHostedService>(static sp =>
			new DispatchCacheOptimizationHostedService(
				sp.GetRequiredService<IDispatchCacheManager>(),
				sp.GetService<IHostApplicationLifetime>(),
				sp.GetRequiredService<IOptions<DispatchOptions>>(),
				sp.GetService<ILogger<DispatchCacheOptimizationHostedService>>())));

		// Transport context resolution - enables pipeline profile selection based on message origin
		services.TryAddSingleton<TransportBindingRegistry>();
		services.TryAddSingleton<ITransportContextProvider, TransportContextProvider>();

		services.TryAddSingleton<IDispatcher, Dispatcher>();
		services.TryAddSingleton<IStreamingDispatcher>(static sp =>
			(IStreamingDispatcher)sp.GetRequiredService<IDispatcher>());
		services.TryAddSingleton<IProgressDispatcher>(static sp =>
			(IProgressDispatcher)sp.GetRequiredService<IDispatcher>());
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
		// Shared failure classifier (S-A): the single retry-vs-dead-letter taxonomy consumed by
		// the retry policy/middleware and the outbox/inbox/CDC processors. Consumers override via TryAdd.
		services.TryAddSingleton<IMessageFailureClassifier, DefaultMessageFailureClassifier>();

		// Without this the outbox and inbox drains resolve nothing and fall through to their
		// NullTransportCircuitBreakerRegistry default, which means a host that has not taken the
		// opt-in Polly package runs those paths with no circuit breaker at all. The Polly package
		// RemoveAll()s this registration before substituting its own, so opting in still wins.
		services.TryAddSingleton<ITransportCircuitBreakerRegistry, TransportCircuitBreakerRegistry>();
		services.TryAddSingleton<FinalDispatchHandler>();

		// Configure handler invocation based on AOT requirements
		services.ConfigureHandlerInvoker();

		// The activator and the event serializer each have a reflection-based default and a
		// reflection-free alternative. Select on what the runtime can actually do, the way
		// ConfigureHandlerInvoker already selects the invoker -- rather than registering the reflective
		// one unconditionally and then annotating this method to say so.
		if (RuntimeFeature.IsDynamicCodeSupported)
		{
			services.TryAddSingleton<IHandlerActivator, HandlerActivator>();
		}
		else
		{
			// Resolves handlers from the container and applies context through IMessageContextAware:
			// no expression compilation, and no reflection over handler members.
			services.TryAddSingleton<IHandlerActivator, AotHandlerActivator>();
		}

		services.TryAddSingleton<DispatchJsonSerializer>();

		if (RuntimeFeature.IsDynamicCodeSupported)
		{
			// JSON-first default. Consumers override with a binary serializer package (for example
			// AddMemoryPackSerializer()), which registers its own IEventSerializer. The optional
			// consumer-registered event-type registry (AddEventTypes<T>()) lets this serializer resolve
			// registered types without a scan; no registry leaves its behaviour unchanged.
			RegisterReflectionEventSerializer(services);
		}
		else
		{
			// No default is possible here -- see AotEventSerializerRequirement. The composition is
			// rejected at startup, naming the call that fixes it.
			AotEventSerializerRequirement.Register(services);
		}

		// Default no-op telemetry sanitizer — overridden by AddDispatchObservability() with HashingTelemetrySanitizer
		services.TryAddSingleton<Excalibur.Dispatch.Telemetry.ITelemetrySanitizer>(
			static _ => Excalibur.Dispatch.Telemetry.NullTelemetrySanitizer.Instance);

		// Logging is infrastructure this registration owns, not something the consumer must remember.
		// AddHttpClient() calls AddLogging() for exactly this reason: every component it seats takes an
		// ILogger, so the extension that seats them makes ILogger resolvable. AddLogging is try-add based
		// and idempotent, so a host that has already configured logging — or that uses a Host builder —
		// is unaffected. This ENABLES logging; it is not a null-logger fallback, which would silence a
		// host that merely forgot the call.
		_ = services.AddLogging();

		// Default no-op message metrics, the sibling of the sanitizer above and for the same reason:
		// recording is a cross-cutting concern and must never be why a message fails to dispatch.
		// Any consumer registration replaces this one.
		services.TryAddSingleton<Excalibur.Dispatch.Diagnostics.IMessageMetrics>(
			static _ => Excalibur.Dispatch.Diagnostics.NullMessageMetrics.Instance);

		// Seat the default profile's middleware. Read from the same list the profile declares itself
		// from, so "declared but not registered" is not a state this framework can be in: a zero-config
		// AddDispatch() yields a pipeline that runs, rather than one that warns seven times and does
		// nothing. Try-add semantics throughout, so a consumer's own registration always wins.
		foreach (var entry in DefaultPipelineProfiles.DefaultProfileMiddleware)
		{
			entry.Register(services);
		}

		// Register telemetry provider by default so metrics and traces are emitted
		// automatically when the consumer adds OpenTelemetry with AddDispatchInstrumentation().
		// This follows the ASP.NET Core pattern: instrumentation is always available,
		// consumers just need to subscribe via AddOpenTelemetry().
		_ = services.AddDispatchTelemetry();

		// Captures handler ServiceLifetimes (built lazily from the final descriptor set) so the
		// singleton LocalMessageBus resolves scoped handlers from a scope, never the root container.
		services.TryAddSingleton(new HandlerLifetimeRegistry(services));

		// LocalMessageBus and Dispatcher both resolve IHandlerRegistry, which is built from the DI
		// descriptor set by AddDispatchHandlers. Calling it here with no assemblies scans nothing -- it only
		// seats the registry factory (TryAdd), so the pipeline this method advertises can actually be
		// constructed. AddDispatch's later AddDispatchHandlers(assemblies) call still registers the scanned
		// handlers; the registry reads the descriptor set lazily at resolve time, so it sees them.
		_ = services.AddDispatchHandlers();

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
	/// Registers the reflection-based JSON event serializer. Only reached where the runtime supports
	/// dynamic code.
	/// </summary>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"The only call site is behind RuntimeFeature.IsDynamicCodeSupported. The native compiler substitutes "
			+ "that property to false and removes the branch, so this serializer is never constructed in a trimmed, "
			+ "natively compiled application; where it is constructed, the reflection it performs is available.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Members annotated with 'RequiresDynamicCodeAttribute' may break when AOT compiling",
		Justification =
			"The only call site is behind RuntimeFeature.IsDynamicCodeSupported, and the native compiler removes "
			+ "that branch, so the dynamic code this serializer needs is never required in a natively compiled "
			+ "application.")]
	private static void RegisterReflectionEventSerializer(IServiceCollection services) =>
		services.TryAddSingleton<IEventSerializer>(static sp => new JsonEventSerializer(sp.GetService<IEventTypeRegistry>()));

	/// <summary>
	/// Registers the system <see cref="TimeProvider"/> unless the consumer already supplied one.
	/// </summary>
	/// <remarks>
	/// Pipeline components resolve <see cref="TimeProvider"/> from the container — CircuitBreakerMiddleware
	/// times its open-duration deadline from it. A public <c>AddSystemTimeProvider</c> extension existed but
	/// had NO callers, so nothing ever placed a TimeProvider in the container and any component taking one
	/// would have failed to resolve. <c>TryAdd</c> keeps a consumer's or a test's fake clock winning.
	/// Extracted to its own method so the registration does not push AddDispatchPipeline over its class-
	/// coupling budget.
	/// </remarks>
	private static void RegisterTimeProvider(IServiceCollection services) =>
		services.TryAddSingleton(static _ => TimeProvider.System);


	/// <summary>
	/// Registers dispatch handlers found in the provided assemblies and/or previously registered
	/// via <c>AddDiscoveredHandlers()</c> or <c>AddHandlersFromAssembly()</c>.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="assembliesToScan"> Assemblies containing handlers or <c> null </c>. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <c> null </c>. </exception>
	/// <remarks>
	/// <para>
	/// Handler discovery uses a single, explicit, consumer-controlled mechanism:
	/// </para>
	/// <list type="number">
	/// <item><description>
	/// Handlers registered in the DI container (via <c>AddHandlersFromAssembly</c>,
	/// <c>AddDiscoveredHandlers</c>, or manual <c>services.AddScoped&lt;IActionHandler&lt;T&gt;, MyHandler&gt;()</c>)
	/// are discovered by scanning <see cref="IServiceCollection"/> for handler interface descriptors.
	/// </description></item>
	/// <item><description>
	/// If <paramref name="assembliesToScan"/> is provided, those assemblies are scanned via reflection
	/// and their handler types are registered with the DI container before the scan.
	/// </description></item>
	/// </list>
	/// <para>
	/// No implicit scanning of <c>AppDomain.CurrentDomain.GetAssemblies()</c> or other magic discovery
	/// is performed. The consumer controls exactly which handlers are registered.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode(
		"Scans the supplied assemblies for handler types, which trimming may remove. Call the no-assembly overload "
		+ "with source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddDispatchHandlers(this IServiceCollection services, params Assembly[]? assembliesToScan)
	{
		ArgumentNullException.ThrowIfNull(services);

		var assemblies = assembliesToScan ?? [];

		// Step 1: If assemblies were provided, scan them and register their handlers in DI.
		// This is the reflection path for consumers using AddDispatch(typeof(Program).Assembly).
		RegisterMessageHandlers(services, assemblies);

		return services.AddDispatchHandlers();
	}

	/// <summary>
	/// Registers the handler registry over the handlers already present in the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The configured <see cref="IServiceCollection" />. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <c> null </c>. </exception>
	/// <remarks>
	/// <para>
	/// This overload scans no assemblies. It builds <c>IHandlerRegistry</c> from the DI descriptor set,
	/// so it discovers exactly the handlers already registered -- by source-generated registration, or by
	/// hand. It is the ahead-of-time compatible entry point: nothing here reads a type that the container
	/// does not already reference, so trimming and native compilation preserve everything it needs.
	/// </para>
	/// <para>
	/// To discover handlers by scanning an assembly instead, call
	/// <see cref="AddDispatchHandlers(IServiceCollection, Assembly[])" />, which is annotated because that
	/// scan is not statically analysable.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2072:Target parameter of 'HandlerRegistry.Register' has DynamicallyAccessedMemberTypes requirements.",
		Justification =
			"The handler types come from ServiceDescriptor.ImplementationType, which carries no annotation. Every one of "
			+ "them is already referenced by the registration that put it in the collection, so trimming keeps it and its "
			+ "constructors: this method reads types the composition root named, never types it discovered.")]
	public static IServiceCollection AddDispatchHandlers(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Build IHandlerRegistry from DI ServiceDescriptors.
		// This is the single source of truth — it discovers every handler that was registered
		// via AddDiscoveredHandlers() (AOT), AddHandlersFromAssembly() (reflection), or
		// manual DI registration. No magic AppDomain scanning.
		services.TryAddSingleton<IHandlerRegistry>(serviceProvider =>
		{
			var registry = new HandlerRegistry();

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
						// keyed-safe accessors handle the keyed/non-keyed distinction.
						var handlerType = descriptor.GetImplementationType() ?? descriptor.GetImplementationInstance()?.GetType();

						if (handlerType is { IsAbstract: false, IsInterface: false })
						{
							var expectsResponse = genericDef == typeof(IActionHandler<,>);
							var responseType = expectsResponse ? descriptor.ServiceType.GetGenericArguments()[1] : null;
							registry.Register(messageType, handlerType, expectsResponse, responseType);
						}
					}
				}
			}

			if (registry is HandlerRegistry concreteRegistry)
			{
				concreteRegistry.PrecomputeSnapshots();
			}

			var registryEntries = registry.GetAll();

			// Only pre-warm reflection-based caches in JIT mode. Under AOT the generated
			// direct-dispatch table resolves handlers with closed-generic calls and bypasses
			// these caches entirely, and pre-warming would fail anyway because BuildInvoker
			// uses GetMethod/Expression.Compile, which are unavailable there.
			if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
			{
				PreWarmJitCaches(serviceProvider, registryEntries);
			}

			return registry;
		});

		return services;
	}

	/// <summary>
	/// Pre-warms the reflection-backed invoker and activator caches. Reachable only when the runtime
	/// supports dynamic code.
	/// </summary>
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification =
			"Every call site is behind RuntimeFeature.IsDynamicCodeSupported. The native compiler substitutes that "
			+ "property to false and removes the branch, so this method is unreachable in a trimmed, natively "
			+ "compiled application; under a JIT runtime the reflection it performs is available and correct.")]
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Members annotated with 'RequiresDynamicCodeAttribute' may break when AOT compiling",
		Justification =
			"Every call site is behind RuntimeFeature.IsDynamicCodeSupported. The native compiler substitutes that "
			+ "property to false and removes the branch, so the expression compilation these calls perform is "
			+ "unreachable in a natively compiled application.")]
	private static void PreWarmJitCaches(
		IServiceProvider serviceProvider,
		IReadOnlyCollection<HandlerRegistryEntry> registryEntries)
	{
		var handlerTypes = registryEntries
			.Select(static entry => entry.HandlerType)
			.Distinct()
			.ToArray();

		HandlerActivator.PreWarmCache(handlerTypes);
		HandlerActivator.PreBindResolutionModes(serviceProvider, handlerTypes);
		HandlerInvoker.PreWarmGeneratedInvokerCache(registryEntries);
	}

	/// <summary>
	/// Registers the core Dispatch pipeline and message handlers.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="assembliesToScan">
	/// Assemblies containing handlers. When none are supplied, handlers are discovered from the entry
	/// assembly.
	/// </param>
	/// <returns> The <see cref="IDispatchBuilder" />, so components can be chained directly off this call. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <c> null </c>. </exception>
	/// <remarks>
	/// Handlers discovered from the entry assembly are registered as transient, and never replace a
	/// handler already registered for the same message: an implicit scan does not outrank a registration
	/// the consumer wrote by hand.
	/// </remarks>
	/// <example>
	/// <code>
	/// // Zero configuration: handlers are discovered from the entry assembly.
	/// services.AddDispatch();
	///
	/// // Explicit: scan the named assemblies instead.
	/// services.AddDispatch(typeof(Program).Assembly);
	/// </code>
	/// </example>
	[RequiresUnreferencedCode("Discovers handlers by scanning assemblies, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[RequiresDynamicCode("Discovers handlers by scanning assemblies and constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IDispatchBuilder AddDispatch(this IServiceCollection services, params Assembly[]? assembliesToScan)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Ensure MessageTypeResolver is initialized
		_ = MessageTypeResolver.Instance;

		var assemblies = assembliesToScan ?? [];

		// Zero-config: when the caller named no assembly, discover handlers from the entry assembly.
		//
		// This is the same fallback AddDispatch(Action<IDispatchBuilder>) already applies, and it is here
		// because a bare AddDispatch() cannot reach that overload — its parameter has no default value, so
		// overload resolution sends the no-argument call to this one. Without this, the two overloads
		// disagreed: the documented zero-config entry point registered no handlers at all, and said nothing
		// about it. The consumer learned of it on their first dispatch, at run time.
		//
		// Nothing is imposed on a caller who named their assemblies; this fires only when they named none.
		// The scan itself is deferential — RegisterMessageHandlers uses TryAdd, so a handler the consumer
		// registered explicitly wins over one merely found — and it registers transient, matching the
		// lifetime the builder path pins for the same reason (see AddDispatch(Action) below).
		if (assemblies.Length == 0)
		{
			var entryAssembly = Assembly.GetEntryAssembly();
			if (entryAssembly != null)
			{
				assemblies = [entryAssembly];
			}
		}

		_ = services.AddDispatchPipeline();
		_ = services.AddDispatchHandlers(assemblies);

		// Guard: if a builder-based AddDispatch(configure) already called Build(),
		// skip — the builder path already materialized the pipeline.
		if (services.Any(static d => d.ServiceType == typeof(DispatchBuilderSentinel)))
		{
			return new DispatchBuilder(services);
		}

		// Apply default performance promotion without calling Build().
		// Build() replaces IDispatchMiddlewareInvoker with a builder-materialized snapshot,
		// which would prevent any middleware registered later (via AddDispatchMiddleware<T>())
		// from being discovered by the legacy GetServices<IDispatchMiddleware>() path.
		_ = services.Configure<DispatchOptions>(static opt =>
			opt.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = true);

		// A builder over the same collection: the composition state lives with the collection, so this
		// continues the composition rather than starting one beside it.
		var builder = new DispatchBuilder(services);

		// This route deliberately never calls Build() (see the comment above), so it must reach the two
		// registrations Build() would otherwise apply directly -- otherwise a composition assembled through
		// this overload validates nothing at start-up and gets neither the empty-composition warning nor the
		// fail-closed authorization guard that AddDispatch(configure) callers receive.
		builder.RegisterOptions();
		builder.RegisterStartupSafetyNets();

		return builder;
	}

	/// <summary>
	/// Registers the core Dispatch pipeline and allows configuration via a builder action.
	/// This is the recommended primary entry point for Dispatch configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> An optional action to configure the <see cref="IDispatchBuilder" />. </param>
	/// <returns> The <see cref="IDispatchBuilder" />, so components can be chained directly off this call. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <c> null </c>. </exception>
	/// <example>
	/// <code>
	/// services.AddDispatch(dispatch =>
	/// {
	///     dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
	///     dispatch.AddPipeline("default", pipeline => pipeline.UseValidation());
	/// });
	/// </code>
	/// </example>
	/// <remarks>
	/// <para>
	/// <paramref name="configure" /> has no default value, so a no-argument <c> AddDispatch() </c> call
	/// resolves to <see cref="AddDispatch(IServiceCollection, Assembly[])" /> rather than to this overload.
	/// That overload discovers handlers from the entry assembly when none were named; this one registers
	/// exactly the handlers <paramref name="configure" /> names, so it introduces no assembly scanning of
	/// its own. A composition that names no handler at all still starts, and logs a warning at start-up
	/// naming the calls that register some.
	/// </para>
	/// <para>
	/// Ahead-of-time compilation is supported and trimming on its own is not, which is one distinction
	/// rather than two. Publishing ahead-of-time makes dynamic code unavailable, and the pipeline selects
	/// a source-generated invoker in that configuration; the reflective one is removed from the
	/// application entirely, so nothing it would have reflected over needs preserving.
	/// </para>
	/// <para>
	/// Publishing trimmed <em>without</em> ahead-of-time compilation is the case to plan for. Dynamic code
	/// remains available there, so the pipeline keeps the reflective invoker, and the handler types it
	/// resolves at run time may already have been trimmed away. Root those handler types, or publish
	/// ahead-of-time, if you trim.
	/// </para>
	/// <para>
	/// What is true of this call is not true of everything reached through it: what
	/// <paramref name="configure" /> registers determines whether the composition stays scanning-free.
	/// Registering handlers by name keeps it so; discovering them from an assembly does not, whatever this
	/// overload does.
	/// </para>
	/// </remarks>
	public static IDispatchBuilder AddDispatch(
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

		// Builder-mode idempotence guard. If a prior AddDispatch(configure) already
		// materialised the pipeline (detected via DispatchBuilderSentinel), skip
		// the builder path entirely to preserve first-configure-wins semantics.
		// A second consumer configure lambda is NOT invoked — silent no-op
		// masks consumer intent, so the guard fires *before* configure is
		// called. Consumers wanting a different pipeline config must call
		// AddDispatch(configure) exactly once per service collection.
		if (services.Any(static d => d.ServiceType == typeof(DispatchBuilderSentinel)))
		{
			return new DispatchBuilder(services);
		}

		// Mark that a builder-based configuration was applied, preventing subsequent
		// parameterless AddDispatch() calls from overwriting the middleware invoker.
		services.TryAddSingleton<DispatchBuilderSentinel>();

		// Create builder and apply default performance promotion BEFORE configure,
		// so consumers can opt out via configure action if desired.
		var builder = new DispatchBuilder(services);
		EnableDefaultPerformancePromotion(builder);
		configure?.Invoke(builder);

		// This overload deliberately does NOT scan for handlers. It once fell back to
		// Assembly.GetEntryAssembly() whenever the configure action named none, and that single branch
		// made the trim analyser treat EVERY caller as reflective — including a caller who composed
		// entirely from source-generated registrations and reflected over nothing. The analyser reads the
		// body, not the argument, so no runtime condition could have narrowed it: the scan had to leave
		// the method for the diagnostic to stop reaching correct consumers.
		//
		// Zero-config is unaffected. A bare AddDispatch() cannot reach this overload at all — configure
		// has no default value, so overload resolution sends it to AddDispatch(params Assembly[]), which
		// discovers the entry assembly and carries the honest annotation for doing so.
		//
		// Materialize pipelines — without this call, ConfigurePipeline() configurations are silently lost.
		// Build() also registers the empty-composition warning and the fail-closed authorization guard
		// (DispatchBuilder.RegisterStartupSafetyNets()) — the params route (AddDispatch(Assembly[]), which
		// deliberately never calls Build()) reaches the same two registrations by calling that method directly.
		_ = builder.Build();

		return builder;
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
	[RequiresUnreferencedCode("Scans the supplied assembly for handlers, which requires types that trimming may remove. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	[RequiresDynamicCode("Scans the supplied assembly for handlers and constructs typed invokers at runtime. Use the source-generated handler registration for an ahead-of-time compatible composition.")]
	public static IServiceCollection AddDispatchWithDefaults(
		this IServiceCollection services,
		Assembly handlerAssembly)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(handlerAssembly);

		_ = services.AddDispatch(dispatch => dispatch
			.AddHandlersFromAssembly(handlerAssembly)
			.WithDefaults());

		return services;
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
			.SelectMany(static a => a.GetLoadableTypes())
			.Where(static t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
			.Select(static t => (
				Type: t,
				Interfaces: t.GetInterfaces()
					.Where(static i =>
						i.IsGenericType &&
						(
							i.GetGenericTypeDefinition() == typeof(IActionHandler<,>) ||
							i.GetGenericTypeDefinition() == typeof(IActionHandler<>) ||
							i.GetGenericTypeDefinition() == typeof(IEventHandler<>) ||
							i.GetGenericTypeDefinition() == typeof(IDocumentHandler<>)
						))
					.ToArray()))
			.Where(static x => x.Interfaces.Length > 0)
			.ToList();

		RegisterAmbiguousSingleHandlerScanWarnings(services, handlerTypes);

		// Register each handler against the DI container
		foreach (var handler in handlerTypes)
		{
			// Register the handler type itself so the activator can resolve it
			services.TryAddTransient(handler.Type);

			// Register the handler for each interface it implements. Which of the two branches applies
			// is decided by the same predicate HandlerRegistry.Register uses to choose fan-out over
			// replacement, so the container and the registry cannot disagree about whether a message
			// type has one handler or many.
			foreach (var iface in handler.Interfaces)
			{
				var descriptor = ServiceDescriptor.Transient(iface, handler.Type);

				if (typeof(IDispatchEvent).IsAssignableFrom(iface.GetGenericArguments()[0]))
				{
					// Events fan out to every handler. A plain TryAdd here would match on the service
					// type alone and silently discard every event handler after the first one found.
					services.TryAddEnumerable(descriptor);
				}
				else
				{
					// One handler wins for everything else, and it must be the consumer's: TryAdd
					// yields to a registration made before this call, and a registration made after it
					// already wins, because the registry takes the last descriptor for a non-event
					// message type.
					services.TryAdd(descriptor);
				}
			}
		}
	}

	/// <summary>
	/// Detects when a single assembly scan discovers more than one handler for a message type that takes
	/// exactly one -- <see cref="IActionHandler{TMessage}" />, <see cref="IActionHandler{TMessage,TResponse}" />
	/// or <see cref="IDocumentHandler{TMessage}" /> -- and arranges for it to be reported once at start-up
	/// via <see cref="AmbiguousHandlerScanFinding" />/<see cref="AmbiguousHandlerScanStartupWarning" />.
	/// Which of the colliding handlers <c>TryAdd</c> keeps depends on <see cref="Assembly.GetTypes" />
	/// enumeration order, which is neither documented nor stable, so silently picking one is never a safe
	/// default -- the ambiguity is the defect, not the choice of winner. Two handlers for one EVENT type is
	/// legitimate fan-out and is excluded: only interfaces whose message type does not implement
	/// <see cref="IDispatchEvent" /> are considered. This is scanned-versus-scanned only; a consumer
	/// registration made before the scan still wins via the existing <c>TryAdd</c> yield-to-caller
	/// behavior and never reaches here.
	/// </summary>
	private static void RegisterAmbiguousSingleHandlerScanWarnings(
		IServiceCollection services, List<(Type Type, Type[] Interfaces)> handlerTypes)
	{
		Dictionary<Type, List<Type>>? handlersByInterface = null;

		foreach (var handler in handlerTypes)
		{
			foreach (var iface in handler.Interfaces)
			{
				if (typeof(IDispatchEvent).IsAssignableFrom(iface.GetGenericArguments()[0]))
				{
					continue;
				}

				handlersByInterface ??= [];
				if (!handlersByInterface.TryGetValue(iface, out var handlersForInterface))
				{
					handlersForInterface = [];
					handlersByInterface[iface] = handlersForInterface;
				}

				handlersForInterface.Add(handler.Type);
			}
		}

		if (handlersByInterface is null)
		{
			return;
		}

		var foundAmbiguity = false;
		foreach (var (iface, handlersForInterface) in handlersByInterface)
		{
			if (handlersForInterface.Count < 2)
			{
				continue;
			}

			foundAmbiguity = true;
			services.AddSingleton(new AmbiguousHandlerScanFinding(
				iface.GetGenericArguments()[0], iface.Name, handlersForInterface));
		}

		if (foundAmbiguity)
		{
			services.TryAddEnumerable(
				ServiceDescriptor.Singleton<IHostedService, AmbiguousHandlerScanStartupWarning>());
		}
	}

	/// <summary>
	/// Internal marker to detect that a builder-based AddDispatch(configure) has been called.
	/// Prevents subsequent parameterless AddDispatch() from overwriting the configured middleware invoker.
	/// </summary>
	internal sealed class DispatchBuilderSentinel;
}

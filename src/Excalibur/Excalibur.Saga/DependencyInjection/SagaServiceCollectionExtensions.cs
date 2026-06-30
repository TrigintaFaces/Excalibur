// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Saga;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Orchestration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Excalibur.Saga services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class SagaServiceCollectionExtensions
{
	/// <summary>
	/// Adds the core Excalibur.Saga services with default options.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for chaining.</returns>
	internal static IServiceCollection AddExcaliburSaga(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// ADR-078: Register Dispatch primitives first (IDispatcher, IMessageBus, etc.)
		_ = services.AddDispatch();

		// AD-252-2: Use AddOptions pattern for proper configuration binding
		// AddOptions<T> ensures IOptions<T>, IOptionsSnapshot<T>, IOptionsMonitor<T> are registered
		// TryAddEnumerable prevents duplicate IConfigureOptions registrations
		_ = services.AddOptions<SagaOptions>()
			.ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<SagaOptions>, DefaultSagaOptionsSetup>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<SagaOptions>, SagaOptionsValidator>());

		// Instance-scoped accumulator for saga registrations discovered during DI composition.
		// Replaces previous static ConcurrentBag fields to prevent test contamination.
		// Registered as an instance singleton so it's accessible at DI composition time
		// (before the container is built) via GetPendingRegistrations().
		if (!services.Any(d => d.ServiceType == typeof(SagaPendingRegistrations)))
		{
			services.AddSingleton(new SagaPendingRegistrations());
		}

		// Register AOT-safe saga registries as singletons.
		// Populated during AddSaga<TSaga, TSagaState>() calls at DI composition time.
		services.TryAddSingleton<ISagaTypeRegistry, SagaTypeRegistry>();
		services.TryAddSingleton<ISagaDispatchRegistry, SagaDispatchRegistry>();

		// iuv3s1: do NOT silently bind an in-memory saga store. Saga state is as stateful as the outbox /
		// event store (it is lost on restart/scale-out), so saga registration adopts the same fail-fast
		// posture: a "default" ISagaStore is a required deployment decision. The in-memory store is
		// available only via an explicit opt-in (AddInMemorySagaStore() / ISagaBuilder.UseInMemoryStore()),
		// and SagaPrerequisiteValidator fails loud at host startup if neither a persistent provider nor the
		// explicit opt-in registered one. Mirrors EventSourcingPrerequisiteValidator / the signing-key guard.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, SagaPrerequisiteValidator>());

		return services;
	}

	/// <summary>
	/// Explicitly registers the in-memory <see cref="Excalibur.Dispatch.Messaging.ISagaStore"/> as the
	/// default saga store. This is an opt-in: it is never registered implicitly, because the in-memory
	/// store loses all in-flight saga state on restart or scale-out and must not be a silent production
	/// default. For production, register a persistent provider instead (for example a SQL Server saga store).
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Uses <c>TryAdd</c> semantics so a persistent provider registered first wins. Registers the store
	/// under the keyed <c>"inmemory"</c> and <c>"default"</c> names plus a non-keyed convenience alias.
	/// </remarks>
	public static IServiceCollection AddInMemorySagaStore(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<Excalibur.Saga.Orchestration.InMemorySagaStore>();
		services.TryAddKeyedSingleton<Excalibur.Dispatch.Messaging.ISagaStore>(
			"inmemory", (sp, _) => sp.GetRequiredService<Excalibur.Saga.Orchestration.InMemorySagaStore>());
		services.TryAddKeyedSingleton<Excalibur.Dispatch.Messaging.ISagaStore>(
			"default", (sp, _) => sp.GetRequiredKeyedService<Excalibur.Dispatch.Messaging.ISagaStore>("inmemory"));

		// Non-keyed ISagaStore convenience alias: forwards to keyed "default" so consumers (and the
		// SagaCoordinator constructor) can inject ISagaStore directly without [FromKeyedServices("default")].
		services.TryAddSingleton<Excalibur.Dispatch.Messaging.ISagaStore>(sp =>
			sp.GetRequiredKeyedService<Excalibur.Dispatch.Messaging.ISagaStore>("default"));

		return services;
	}

	/// <summary>
	/// Adds the core Excalibur.Saga services with the specified configuration.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <param name="configure">The action to configure saga options.</param>
	/// <returns>The service collection for chaining.</returns>
	internal static IServiceCollection AddExcaliburSaga(
		this IServiceCollection services,
		Action<SagaOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure base services are registered (including Dispatch primitives)
		_ = services.AddExcaliburSaga();

		_ = services.Configure(configure);

		return services;
	}

	/// <summary>
	/// Adds the core Excalibur.Saga services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <param name="configuration">The configuration section to bind options from.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	internal static IServiceCollection AddExcaliburSaga(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Ensure base services are registered (including Dispatch primitives)
		_ = services.AddExcaliburSaga();

		_ = services.AddOptions<SagaOptions>()
			.Bind(configuration);

		return services;
	}

	/// <summary>
	/// Adds the core Excalibur.Saga services and returns a builder for configuring
	/// sub-features such as orchestration, timeouts, and instrumentation.
	/// </summary>
	/// <param name="services">The service collection to add services to.</param>
	/// <param name="configure">Action to configure saga sub-features via the builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the recommended entry point for configuring all saga services.
	/// It replaces multiple separate <c>AddExcaliburAdvancedSagas</c>,
	/// <c>AddSagaTimeoutDelivery</c>, and <c>AddSagaInstrumentation</c> calls
	/// with a single, discoverable builder pattern.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddSagas(saga => saga
	///     .WithCoordination()
	///     .WithTimeouts(opts => opts.PollInterval = TimeSpan.FromSeconds(30))
	///     .WithInstrumentation()));
	/// </code>
	/// </example>
	internal static IServiceCollection AddExcaliburSaga(
		this IServiceCollection services,
		Action<ISagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register core saga services first
		_ = services.AddExcaliburSaga();

		// Apply builder configuration
		var builder = new SagaBuilder(services);
		configure(builder);

		return services;
	}

	/// <summary>
	/// Checks if Excalibur.Saga services have been registered.
	/// </summary>
	/// <param name="services">The service collection to check.</param>
	/// <returns>True if saga services are registered, false otherwise.</returns>
	public static bool HasExcaliburSaga(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// AD-252-2: Check for IConfigureOptions registration (proper options pattern)
		return services.Any(s =>
			s.ServiceType == typeof(IConfigureOptions<SagaOptions>) ||
			s.GetImplementationType() == typeof(DefaultSagaOptionsSetup));
	}

	/// <summary>
	/// Registers a saga type and its state type, populating the AOT-safe registries
	/// for runtime type resolution and dispatch.
	/// </summary>
	/// <typeparam name="TSaga">The saga type (must extend <see cref="SagaBase{TSagaState}"/>).</typeparam>
	/// <typeparam name="TSagaState">The saga state type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddSaga<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSaga,
		TSagaState>(this IServiceCollection services)
		where TSaga : SagaBase<TSagaState>
		where TSagaState : Excalibur.Dispatch.Messaging.SagaState, new()
	{
		ArgumentNullException.ThrowIfNull(services);

		// Ensure core saga services are registered
		_ = services.AddExcaliburSaga();

		// Register the saga as scoped
		services.TryAddScoped<TSaga>();

		// Retrieve the instance-scoped registration accumulator from the service collection.
		var pending = GetPendingRegistrations(services);

		// Accumulate saga/state types for AOT-safe registry population.
		// SagaPendingRegistrations is an instance-scoped accumulator read by SagaTypeRegistryPopulator
		// (registered as IPostConfigureOptions) which runs on first options resolution.
		pending.TypeRegistrations.Add(typeof(TSaga));
		pending.TypeRegistrations.Add(typeof(TSagaState));

		// Accumulate typed dispatch delegate for AOT-safe dispatch.
		// At DI composition time, TSaga and TSagaState are concrete types,
		// so the AOT compiler preserves the concrete HandleEventInternalAsync<TSaga, TSagaState> instantiation.
		pending.DispatchRegistrations.Add(CreateDispatchDelegate<TSaga, TSagaState>());

		// Register the populators once (idempotent via TryAddEnumerable)
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<SagaOptions>, SagaTypeRegistryPopulator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<SagaOptions>, SagaDispatchRegistryPopulator>());

		return services;
	}

	/// <summary>
	/// Creates a typed dispatch delegate registration action for a saga type pair.
	/// Isolated method to scope the IL2026/IL3050 suppressions precisely.
	/// </summary>
	[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
		Justification = "Typed dispatch delegate created at DI composition time with concrete generic arguments. " +
		"AOT compiler preserves the concrete HandleEventInternalAsync<TSaga, TSagaState> instantiation.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Typed dispatch delegate created at DI composition time with concrete generic arguments. " +
		"No runtime code generation needed — the generic method instantiation is known at compile time.")]
	private static Action<ISagaDispatchRegistry> CreateDispatchDelegate<TSaga, TSagaState>()
		where TSaga : SagaBase<TSagaState>
		where TSagaState : Excalibur.Dispatch.Messaging.SagaState, new()
	{
		return static registry => registry.Register(
			typeof(TSaga),
			typeof(TSagaState),
			static (coordinator, ctx, evt, info, ct) =>
				((SagaCoordinator)coordinator).HandleEventInternalAsync<TSaga, TSagaState>(ctx, evt, info, ct));
	}

	/// <summary>
	/// Default options setup for SagaOptions.
	/// </summary>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
		Justification = "Instantiated via DI through TryAddEnumerable ServiceDescriptor")]
	private sealed class DefaultSagaOptionsSetup : IConfigureOptions<SagaOptions>
	{
		public void Configure(SagaOptions options)
		{
			// Defaults are already set in SagaOptions constructor
			// This class exists to enable proper options pattern integration
		}
	}

	/// <summary>
	/// Retrieves the <see cref="SagaPendingRegistrations"/> instance from the service collection.
	/// The instance is stored as a singleton descriptor's implementation instance, accessible
	/// at DI composition time (before the container is built).
	/// </summary>
	private static SagaPendingRegistrations GetPendingRegistrations(IServiceCollection services)
	{
		var descriptor = services.First(
			d => d.ServiceType == typeof(SagaPendingRegistrations));

		return (SagaPendingRegistrations)descriptor.GetImplementationInstance()!;
	}

	/// <summary>
	/// Populates <see cref="ISagaTypeRegistry"/> from types accumulated during DI composition.
	/// Runs once on first <see cref="SagaOptions"/> resolution via <see cref="IPostConfigureOptions{TOptions}"/>.
	/// </summary>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
		Justification = "Instantiated via DI through TryAddEnumerable ServiceDescriptor")]
	private sealed class SagaTypeRegistryPopulator(
		ISagaTypeRegistry typeRegistry,
		SagaPendingRegistrations pending) : IPostConfigureOptions<SagaOptions>
	{
		private volatile bool _populated;

		public void PostConfigure(string? name, SagaOptions options)
		{
			if (_populated)
			{
				return;
			}

			_populated = true;

			foreach (var type in pending.TypeRegistrations)
			{
				typeRegistry.RegisterType(type);
			}

			typeRegistry.Freeze();
		}
	}

	/// <summary>
	/// Populates <see cref="ISagaDispatchRegistry"/> from dispatch delegate actions accumulated during DI composition.
	/// Runs once on first <see cref="SagaOptions"/> resolution via <see cref="IPostConfigureOptions{TOptions}"/>.
	/// </summary>
	[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
		Justification = "Instantiated via DI through TryAddEnumerable ServiceDescriptor")]
	private sealed class SagaDispatchRegistryPopulator(
		ISagaDispatchRegistry dispatchRegistry,
		SagaPendingRegistrations pending) : IPostConfigureOptions<SagaOptions>
	{
		private volatile bool _populated;

		public void PostConfigure(string? name, SagaOptions options)
		{
			if (_populated)
			{
				return;
			}

			_populated = true;

			foreach (var registration in pending.DispatchRegistrations)
			{
				registration(dispatchRegistry);
			}

			dispatchRegistry.Freeze();
		}
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CoreDispatcher = Excalibur.Dispatch.Delivery.Dispatcher;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Fluent builder for configuring the Excalibur framework.
/// </summary>
public sealed partial class DispatchBuilder : IDispatchBuilder, IDisposable
{
	// Aliases into the collection-held state, not per-builder collections. See DispatchBuilderState:
	// the composition must stay continuable after this builder is gone, so every builder over one
	// service collection accumulates into the same objects.
	private readonly DispatchBuilderState _state;
	private readonly Dictionary<string, Action<IPipelineBuilder>> _pipelineConfigurations;
	private readonly Dictionary<string, ITransportAdapter> _transportAdapters;
	private readonly List<Action<IBindingConfigurationBuilder>> _bindingConfigurations;
	private readonly List<Type> _globalMiddleware;
	private readonly DispatchOptions _options;
	private readonly PipelineProfileRegistry _profileRegistry;
	private readonly TransportBindingRegistry _bindingRegistry;
	private volatile bool _disposed;

	/// <summary>
	/// Gets a value indicating whether any handler registrations have been made via
	/// <c>AddHandlersFromAssembly</c>.
	/// </summary>
	internal bool HasHandlerRegistrations
	{
		get => _state.HasHandlerRegistrations;
		set => _state.HasHandlerRegistrations = value;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchBuilder"/> class.
	/// Creates a new Dispatch builder.
	/// </summary>
	public DispatchBuilder(IServiceCollection services)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));

		// One state per service collection. A second builder over the same collection continues the same
		// composition rather than starting an invisible one beside it.
		_state = DispatchBuilderState.GetOrAdd(services);
		_pipelineConfigurations = _state.PipelineConfigurations;
		_transportAdapters = _state.TransportAdapters;
		_bindingConfigurations = _state.BindingConfigurations;
		_globalMiddleware = _state.GlobalMiddleware;
		_options = _state.Options;
		_profileRegistry = _state.ProfileRegistry;
		_bindingRegistry = _state.BindingRegistry;

		// Register core services — first-wins TryAdd semantics so a consumer's
		// explicit pre-registration of any of these services survives a
		// subsequent AddDispatch(configure) call.
		// IMiddlewareApplicabilityStrategy is already TryAdd-registered by
		// AddDispatchPipeline (which always runs before this constructor via the
		// AddDispatch entry points); the redundant ctor registration was the
		// D1 row 3 drifter and is intentionally removed here.
		// [ rows 1+2]
		Services.TryAddSingleton(_profileRegistry);
		Services.TryAddSingleton(_bindingRegistry);

		// Bind IPipelineProfileRegistry to the SAME instance the builder configures — but ONLY
		// when no consumer has supplied their own registry.
		//
		// AddDispatchPipeline (always runs first) TryAdd-registers a framework-default
		// IPipelineProfileRegistry -> PipelineProfileRegistry *type* registration, which the
		// container would otherwise activate as a DIFFERENT, empty instance than the builder's
		// configured _profileRegistry. That split meant build-time profile resolution
		// (PipelineBuilder.UseProfile -> GetService<IPipelineProfileRegistry>()) saw a registry
		// WITHOUT the builder.RegisterProfile(...)/configured-default profiles — a configured
		// profile resolved "not found". So the builder's instance must win over the framework
		// default.
		//
		// BUT a consumer's pre-AddDispatch registration of IPipelineProfileRegistry — in ANY
		// form (instance, implementation-type, OR factory) — MUST win over the builder
		// (first-wins / consumer-override,). A consumer who replaces the registry
		// OWNS profile registration (Microsoft-first "replace a service = take ownership"; the
		// framework does NOT merge its defaults into a consumer-owned instance). If such a
		// consumer references an unpopulated profile via UseProfile(...), PipelineBuilder fails
		// LOUD (ArgumentException at :147-153), never silently — so ownership transfer is safe.
		//
		// Discriminator: replace ONLY when the existing registration is the framework's OWN
		// default — the type-registration TryAddSingleton<IPipelineProfileRegistry,
		// PipelineProfileRegistry>() (ImplementationType == typeof(PipelineProfileRegistry) AND
		// no instance/factory). Every consumer-override form fails that test and is left
		// authoritative untouched — so "clobber a consumer override" is structurally
		// inexpressible. No field retarget — preserves the DispatchCacheManager concrete-cast
		// perf path.
		var existing = Services.FirstOrDefault(
			static d => d.ServiceType == typeof(IPipelineProfileRegistry));
		// keyed-safe accessors (raw reads — including property patterns — throw on keyed descriptors).
		var isFrameworkDefault =
			existing is not null
			&& existing.GetImplementationInstance() is null
			&& existing.GetImplementationFactory() is null
			&& existing.GetImplementationType() == typeof(PipelineProfileRegistry);
		if (existing is null || isFrameworkDefault)
		{
			_ = Services.Replace(
				ServiceDescriptor.Singleton<IPipelineProfileRegistry>(_profileRegistry));
		}

		// The transport binding registry needs the identical treatment, for the identical reason, and did
		// not have it. AddDispatchPipeline TryAdd-registers TransportBindingRegistry as a TYPE, so the
		// TryAddSingleton of the builder's instance above is a no-op and the container activates a second,
		// empty registry. Anything resolving TransportBindingRegistry from the container therefore saw none
		// of the bindings the builder configured -- and a binding that resolves from nothing does not fail,
		// it just never matches, so the pipeline profile a received message selects is silently the default.
		//
		// Same discriminator as above: replace ONLY the framework's own type registration. A consumer who
		// supplied an instance, a factory, or their own implementation type owns the registry and is left
		// untouched.
		var existingBindings = Services.FirstOrDefault(
			static d => d.ServiceType == typeof(TransportBindingRegistry));
		var bindingsAreFrameworkDefault =
			existingBindings is not null
			&& existingBindings.GetImplementationInstance() is null
			&& existingBindings.GetImplementationFactory() is null
			&& existingBindings.GetImplementationType() == typeof(TransportBindingRegistry);
		if (existingBindings is null || bindingsAreFrameworkDefault)
		{
			_ = Services.Replace(ServiceDescriptor.Singleton(_bindingRegistry));
		}

		// Registered from the constructor so it is present on EVERY composition route, and resolved at
		// start-up so it reads the FINISHED composition. The equivalent check inside Build() runs while the
		// composition is still being written, so it cannot see middleware a consumer chains on afterwards --
		// which is the normal shape now that AddDispatch hands the builder back.
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, AuthorizationFeatureWiringValidator>());
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, AuthorizationFeatureWiringValidator>());
	}

	/// <inheritdoc />
	public IServiceCollection Services { get; }

	/// <inheritdoc />
	public IDispatchBuilder ConfigurePipeline(string name, Action<IPipelineBuilder> configure)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(configure);

		_pipelineConfigurations[name] = configure;
		return this;
	}

	/// <inheritdoc />
	public IDispatchBuilder RegisterProfile(IPipelineProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile);

		_profileRegistry.RegisterProfile(profile);
		return this;
	}

	/// <inheritdoc />
	public IDispatchBuilder AddBinding(Action<IBindingConfigurationBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		_bindingConfigurations.Add(configure);
		return this;
	}

	/// <inheritdoc />
	public IDispatchBuilder UseMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
		where TMiddleware : IDispatchMiddleware
	{
		// Idempotent by concrete type, matching how BuildPipeline already deduplicates a middleware
		// registered both here and as an IDispatchMiddleware. A type appearing twice in the global set runs
		// twice on every message, which for a short-circuiting middleware is a defect rather than a
		// preference: a second InboxMiddleware sees the message its own first pass admitted, treats it as a
		// duplicate, and suppresses the handler entirely. Reachable now that a metapackage places the inbox
		// and a consumer may also call UseInbox() themselves.
		if (!_globalMiddleware.Contains(typeof(TMiddleware)))
		{
			_globalMiddleware.Add(typeof(TMiddleware));
		}

		// Transient is not a claim about scope: the pipeline walks the middleware's constructor graph and
		// opens a per-dispatch scope only when something it depends on is Scoped. TryAdd so a consumer's own
		// registration keeps the lifetime they declared.
		Services.TryAddTransient(typeof(TMiddleware));
		return this;
	}

	/// <inheritdoc />
	public IDispatchBuilder ConfigureOptions<TOptions>(Action<TOptions> configure)
		where TOptions : class
	{
		ArgumentNullException.ThrowIfNull(configure);

		if (typeof(TOptions) == typeof(DispatchOptions))
		{
			configure((TOptions)(object)_options);
		}

		return this;
	}

	/// <summary>
	/// Configures pipeline profiles using a fluent API.
	/// </summary>
	/// <param name="configure"> Configuration action for pipeline profiles. </param>
	/// <returns> The builder for chaining. </returns>
	public IDispatchBuilder WithPipelineProfiles(Action<IPipelineProfilesConfigurationBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		var profilesBuilder = new PipelineProfilesConfigurationBuilder(_profileRegistry);
		configure(profilesBuilder);

		return this;
	}

	/// <summary>
	/// Configures dispatch options using a fluent API.
	/// </summary>
	/// <param name="configure"> Configuration action for dispatch options. </param>
	/// <returns> The builder for chaining. </returns>
	public IDispatchBuilder WithOptions(Action<DispatchOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(configure);

		configure(_options);
		return this;
	}

	/// <summary>
	/// Materializes configured pipelines and registers the dispatcher in the service collection.
	/// This is an internal implementation detail called by <c>AddDispatch()</c> entry points.
	/// </summary>
	/// <returns>The configured dispatcher instance.</returns>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL2072:'serviceType' argument does not satisfy 'DynamicallyAccessedMemberTypes.PublicConstructors' in call to 'Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddScoped(IServiceCollection, Type)'",
		Justification =
			"Middleware types are registered at configuration time and are preserved through explicit registration. All middleware types implement IDispatchMiddleware and have their constructors preserved.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
		Justification = "HandlerLifetimeAnalyzer uses reflection for handler constructor inspection which is safe for known registered handler types.")]
	internal IDispatcher Build()
	{
		// Defense in depth — the AddDispatch(configure) entry point already
		// guards against a second Build() invocation by short-circuiting before
		// the builder runs. This guard protects the descriptor graph when
		// Build() is reached through any future path that bypasses the entry
		// point. Keyed on Build() itself rather than on the deferred composition: the params route
		// registers that composition WITHOUT building, and a later AddDispatch(configure) over the same
		// collection must still run the rest of Build().
		if (_state.IsBuilt)
		{
			return new DeferredDispatcher(RegisterDeferredComposition());
		}

		RegisterOptions();
		RegisterStartupSafetyNets();

		// Fail closed at startup: authorization was explicitly wired into the pipeline
		// (UseAuthorization() → AuthorizationMiddleware in the global middleware set) but its activating
		// feature is disabled, so the synthesizer/evaluator would silently drop the authorization stage and
		// let authorization-required Action messages reach their handlers unauthorized. Fail loud, naming the
		// missing feature — mirroring ASP.NET Core's missing-UseAuthorization InvalidOperationException. A
		// consumer who never called UseAuthorization() has no descriptor here, so this never fires (the
		// opt-in-complexity design principle is preserved; disabling the feature without wiring auth is a
		// deliberate opt-out, not a misconfiguration).
		if (_globalMiddleware.Contains(typeof(AuthorizationMiddleware)) && !_options.Features.EnableAuthorization)
		{
			throw new InvalidOperationException(
				"AuthorizationMiddleware is registered in the dispatch pipeline (via UseAuthorization()), but the " +
				"Authorization feature is disabled (DispatchOptions.Features.EnableAuthorization = false). This would " +
				"silently drop the authorization stage and allow authorization-required Action messages to bypass " +
				"authorization. Enable the Authorization feature, or remove UseAuthorization() if authorization is " +
				"intentionally not used.");
		}

		foreach (var middlewareType in _globalMiddleware)
		{
			Services.TryAddTransient(middlewareType);
		}

		_state.IsBuilt = true;
		var dispatcherHolder = RegisterDeferredComposition();

		// PERF: Auto-promote eligible transient handlers to singleton when opted in.
		if (_options.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton)
		{
			HandlerLifetimeAnalyzer.PromoteEligibleHandlers(Services);
		}

		return new DeferredDispatcher(dispatcherHolder);
	}

	/// <summary>
	/// Registers the pipeline composition as factories the container runs when the pipeline is first
	/// resolved, not now. Idempotent per service collection.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Nothing here reads the middleware set. The runtime state reads the collection-held composition when
	/// the provider resolves it, so middleware added after this call returns -- by a <c>Use*()</c> chained
	/// off the builder <c>AddDispatch</c> hands back, from an extension method, or by a second builder over
	/// the same collection -- is in the pipeline exactly as if it had been added inside the configuration
	/// callback. There is no point after which a <c>Use*()</c> call is accepted and ignored.
	/// </para>
	/// <para>
	/// Internal, not private: both entry points reach it. <c>AddDispatch(configure)</c> reaches it through
	/// <see cref="Build"/>; <c>AddDispatch(Assembly[])</c> calls it directly. Without the second call that
	/// route composed its pipeline only from <see cref="IDispatchMiddleware"/> registrations, so every
	/// <c>Use*()</c> made on the builder it returned landed in a set nothing read.
	/// </para>
	/// </remarks>
	internal void RegisterPipelineComposition() => _ = RegisterDeferredComposition();

	private DispatcherHolder RegisterDeferredComposition()
	{
		if (Services.FirstOrDefault(static d => d.ServiceType == typeof(DispatcherHolder))
			?.GetImplementationInstance() is DispatcherHolder existing)
		{
			return existing;
		}

		// Register the synthesizer used during runtime construction
		Services.TryAddSingleton<PipelineProfileSynthesizer>();

		// Register runtime state that materializes pipelines using the caller's provider scope.
		// Reached at most once per service collection due to the guard above.
		_ = Services.AddSingleton(BuildRuntimeState);

		// Ensure the configured dispatch pipeline and middleware invoker use
		// the builder-materialized middleware, not the empty DI fallback.
		_ = Services.Replace(ServiceDescriptor.Singleton<IDispatchPipeline>(sp =>
			sp.GetRequiredService<DispatchRuntimeState>().DefaultPipeline.Pipeline));

		// Materialize the invoker from the resolved default pipeline (global + profile
		// middleware), not from _globalMiddleware alone. The DispatchAsync hot path
		// consults the invoker, never the configured IDispatchPipeline; sourcing the
		// invoker from _globalMiddleware ONLY left a UseProfile/ConfigurePipeline-
		// configured default pipeline invisible to the dispatcher (HasMiddleware==false
		// → full bypass). The default pipeline is built by PipelineBuilder.Build(),
		// which is the single canonical resolve-safe materialization site (GetService +
		// skip-and-log for unregistered/unconstructable profile middleware). The invoker
		// reuses that already-resolved middleware so there is ONE resolution path, not a
		// second divergent one.
		_ = Services.Replace(ServiceDescriptor.Singleton<IDispatchMiddlewareInvoker>(sp =>
		{
			var runtimeState = sp.GetRequiredService<DispatchRuntimeState>();
			return new DispatchMiddlewareInvoker(
				runtimeState.DefaultPipeline.ResolvedMiddleware,
				sp.GetRequiredService<IMiddlewareApplicabilityStrategy>());
		}));

		var dispatcherHolder = new DispatcherHolder();
		Services.TryAddSingleton(dispatcherHolder);

		Services.TryAddSingleton(sp =>
		{
			var dispatcher = ActivatorUtilities.CreateInstance<CoreDispatcher>(sp);
			dispatcherHolder.Set(dispatcher);
			return dispatcher;
		});

		_ = Services.Replace(ServiceDescriptor.Singleton<IDispatcher>(sp => sp.GetRequiredService<CoreDispatcher>()));

		return dispatcherHolder;
	}

	/// <summary>
	/// Disposes the builder and releases the binding registry.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		// The binding registry is owned by the container (TryAddSingleton in the constructor) and shared
		// by every builder over this collection, so disposing it here would tear down live state a later
		// builder -- or the running host -- still reads.
		_disposed = true;
	}

	/// <summary>
	/// Registers <see cref="DispatchOptions"/> validation and the option types it composes. Internal, not
	/// private: both the builder route (<c>AddDispatch(configure)</c>, via <see cref="Build"/>) and the
	/// params route (<c>AddDispatch(Assembly[])</c>, which never calls <see cref="Build"/>) must reach this
	/// registration — a composition assembled through either route is expected to validate at start-up, not
	/// just one of them.
	/// </summary>
	internal void RegisterOptions()
	{
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DispatchOptions>, DispatchOptionsValidator>());

		// InboxMiddleware injects IOptions<InboxConfigurationOptions>, but every writer of those values --
		// WithInbox(), WithInboxMode(), WithLightMode(), the configuration binder -- writes DispatchOptions.Inbox,
		// a nested object on a DIFFERENT options type. Without this bridge the middleware resolved a
		// freshly-constructed default whose Enabled is false, so it forwarded every message untouched and
		// deduplicated nothing, in every configuration, with no error. Hands it the SAME nested instance the
		// DispatchOptions pipeline produced, so the two cannot drift.
		_ = Services.AddSingleton<IOptions<InboxConfigurationOptions>>(static sp =>
			Microsoft.Extensions.Options.Options.Create(sp.GetRequiredService<IOptions<DispatchOptions>>().Value.Inbox));

		// Enabling the inbox selects durable, store-backed deduplication, but the store lives in a
		// persistence package this one does not reference and cannot register. Registering the gate here —
		// on the single path every builder configuration flows through — means the flag cannot be set by
		// any entry point without the store requirement being checked, rather than each entry point that
		// promises durability having to remember to opt in. The gate is inert while the inbox is disabled.
		var services = Services;
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, DurableInboxPrerequisiteValidator>(
				sp => new DurableInboxPrerequisiteValidator(services, sp.GetRequiredService<IOptions<DispatchOptions>>())));
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, DurableInboxPrerequisiteValidator>(
				sp => new DurableInboxPrerequisiteValidator(services, sp.GetRequiredService<IOptions<DispatchOptions>>())));

		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<Options.Core.InMemoryBusOptions>, Options.Core.InMemoryBusOptionsValidator>());

		_ = Services.AddOptions<DispatchOptions>()
			.Configure(opt =>
			{
				opt.DefaultTimeout = _options.DefaultTimeout;
				opt.MaxConcurrency = _options.MaxConcurrency;
				opt.UseLightMode = _options.UseLightMode;
				opt.MessageBufferSize = _options.MessageBufferSize;
				opt.EnablePipelineSynthesis = _options.EnablePipelineSynthesis;
				// Mutate the existing Features/CrossCutting instances in place, and only where the builder was
				// actually asked to change something -- never swap the reference, and never blindly overwrite a
				// property with the builder's own untouched default. A consumer's Configure<DispatchOptions>()
				// registered before AddDispatch (the framework's own default-promotion call among them) may
				// already have mutated a property on these SAME objects; only WithOptions()-expressed intent
				// (a value that differs from the type default) is entitled to override it here.
				var featureDefaults = new DispatchFeatureOptions();
				CopyIfChanged(_options.Features.EnableCorrelation, featureDefaults.EnableCorrelation, v => opt.Features.EnableCorrelation = v);
				CopyIfChanged(_options.Features.EnableMetrics, featureDefaults.EnableMetrics, v => opt.Features.EnableMetrics = v);
				CopyIfChanged(_options.Features.ValidateMessageSchemas, featureDefaults.ValidateMessageSchemas, v => opt.Features.ValidateMessageSchemas = v);
				CopyIfChanged(_options.Features.EnableMultiTenancy, featureDefaults.EnableMultiTenancy, v => opt.Features.EnableMultiTenancy = v);
				CopyIfChanged(_options.Features.EnableVersioning, featureDefaults.EnableVersioning, v => opt.Features.EnableVersioning = v);
				CopyIfChanged(_options.Features.EnableAuthorization, featureDefaults.EnableAuthorization, v => opt.Features.EnableAuthorization = v);
				CopyIfChanged(_options.Features.EnableTransactions, featureDefaults.EnableTransactions, v => opt.Features.EnableTransactions = v);

				var performanceDefaults = new PerformanceOptions();
				CopyIfChanged(_options.CrossCutting.Performance.EnableTypeMetadataCaching, performanceDefaults.EnableTypeMetadataCaching, v => opt.CrossCutting.Performance.EnableTypeMetadataCaching = v);
				CopyIfChanged(_options.CrossCutting.Performance.MessagePoolSize, performanceDefaults.MessagePoolSize, v => opt.CrossCutting.Performance.MessagePoolSize = v);
				CopyIfChanged(_options.CrossCutting.Performance.UseAllocationFreeExecution, performanceDefaults.UseAllocationFreeExecution, v => opt.CrossCutting.Performance.UseAllocationFreeExecution = v);
				CopyIfChanged(_options.CrossCutting.Performance.AutoFreezeOnStart, performanceDefaults.AutoFreezeOnStart, v => opt.CrossCutting.Performance.AutoFreezeOnStart = v);
				CopyIfChanged(_options.CrossCutting.Performance.EmitDirectLocalResultMetadata, performanceDefaults.EmitDirectLocalResultMetadata, v => opt.CrossCutting.Performance.EmitDirectLocalResultMetadata = v);
				CopyIfChanged(_options.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton, performanceDefaults.AutoPromoteStatelessHandlersToSingleton, v => opt.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = v);

				opt.Inbox.Enabled = _options.Inbox.Enabled;
				opt.Inbox.DeduplicationExpiryHours = _options.Inbox.DeduplicationExpiryHours;
				opt.Inbox.AckAfterHandle = _options.Inbox.AckAfterHandle;
				opt.Inbox.MaxRetries = _options.Inbox.MaxRetries;
				opt.Inbox.RetryDelayMinutes = _options.Inbox.RetryDelayMinutes;
				opt.Inbox.MaxRetention = _options.Inbox.MaxRetention;
				opt.Inbox.CleanupInterval = _options.Inbox.CleanupInterval;

				opt.Outbox.Enabled = _options.Outbox.Enabled;

				opt.Consumer.Dedupe.Enabled = _options.Consumer.Dedupe.Enabled;
				opt.Consumer.Dedupe.DefaultExpiry = _options.Consumer.Dedupe.DefaultExpiry;
				opt.Consumer.Dedupe.CleanupInterval = _options.Consumer.Dedupe.CleanupInterval;
				opt.Consumer.AckAfterHandle = _options.Consumer.AckAfterHandle;
				opt.Consumer.MaxConcurrentMessages = _options.Consumer.MaxConcurrentMessages;
				opt.Consumer.VisibilityTimeout = _options.Consumer.VisibilityTimeout;
				opt.Consumer.MaxRetries = _options.Consumer.MaxRetries;
			})
			.ValidateOnStart();

		static void CopyIfChanged<T>(T current, T defaultValue, Action<T> assign)
			where T : IEquatable<T>
		{
			if (!current.Equals(defaultValue))
			{
				assign(current);
			}
		}
	}

	/// <summary>
	/// Registers the start-up hosted services/validators that warn or fail closed on a mis-composed
	/// dispatcher, rather than letting the gap surface later as a silent runtime drop.
	/// </summary>
	/// <remarks>
	/// Internal, not private, for the same reason as <see cref="RegisterOptions"/>: the params route
	/// (<c>AddDispatch(Assembly[])</c>) never calls <see cref="Build"/> and must call this directly so a
	/// composition assembled that way gets the same start-up safety net as the builder route. <c>TryAdd</c>/
	/// <c>TryAddEnumerable</c> throughout, so calling this more than once per service collection (builder
	/// route via <see cref="Build"/>, plus a direct call from the params route) registers each validator once.
	/// </remarks>
	internal void RegisterStartupSafetyNets()
	{
		// A composition that reaches here and names no handler is not an error — a send-only host is a
		// supported shape — but it is far more often a mistake, and an expensive one to find later:
		// an action or query with no handler throws on the first dispatch, while an EVENT with no handler
		// only logs, so a broken composition can run for a long time quietly dropping events. Say so once,
		// at start-up, and name both remedies.
		//
		// Registered unconditionally, and it re-reads the handler registry when the host starts rather
		// than trusting what the builder knew here: a consumer may register handlers after this call
		// returns, and a warning that fired for them would be the kind of false alarm that teaches people
		// to filter the category out.
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, NoHandlersRegisteredStartupWarning>());

		// The fail-closed authorization guard. It throws at start-up when the DEFAULT profile declares the
		// authorization stage but no AuthorizationMiddleware is resolvable -- messages would otherwise be
		// routed through that profile with authorization silently absent.
		//
		// BOTH lifecycle contracts, deliberately. An IHostedService fires only when something calls
		// IHost.StartAsync, so a serverless entry point or a manual BuildServiceProvider() would never
		// trigger it -- leaving the guarantee silently inert for the hosts least able to afford it.
		// IStartupPrerequisiteValidator is what ValidateStartupGates() reaches on the host-less path.
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, AuthorizationWiringPrerequisiteValidator>());
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, AuthorizationWiringPrerequisiteValidator>());
	}

	private DispatchRuntimeState BuildRuntimeState(IServiceProvider serviceProvider)
	{
		EnsureSynthesizedProfiles(serviceProvider);
		BuildBindings();

		var pipelines = new Dictionary<string, PipelineRuntimeEntry>(StringComparer.OrdinalIgnoreCase);

		if (_pipelineConfigurations.Count == 0)
		{
			var entry = BuildPipeline(serviceProvider, "Default", static _ => { });
			pipelines["Default"] = entry;
		}
		else
		{
			foreach (var (name, configure) in _pipelineConfigurations)
			{
				var entry = BuildPipeline(serviceProvider, name, configure);
				pipelines[name] = entry;
			}
		}

		var defaultPipeline = pipelines.Values.First();
		return new DispatchRuntimeState(pipelines, defaultPipeline, _bindingRegistry);
	}

	private void EnsureSynthesizedProfiles(IServiceProvider serviceProvider)
	{
		if (!_options.EnablePipelineSynthesis || _profileRegistry.GetProfileNames().Any())
		{
			return;
		}

		var logger = serviceProvider.GetService<ILogger<DispatchBuilder>>();
		if (logger != null)
		{
			LogNoPipelineProfilesRegisteredSynthesizing(logger);
		}

		var synthesizer = serviceProvider.GetRequiredService<PipelineProfileSynthesizer>();
		var synthesisResult = synthesizer.SynthesizeRequiredProfiles();

		foreach (var (profileName, profile) in synthesisResult.Profiles)
		{
			_profileRegistry.RegisterProfile(profile);
			if (logger != null)
			{
				LogRegisteredSynthesizedProfile(logger, profileName);
			}
		}

		foreach (var (messageKind, profileName) in synthesisResult.Mappings)
		{
			if (logger != null)
			{
				LogMappedMessageKindToProfile(logger, messageKind.ToString(), profileName);
			}
		}

		foreach (var warning in synthesisResult.Warnings)
		{
			if (logger != null)
			{
				LogPipelineSynthesisWarning(logger, warning.Message);
			}
		}
	}

	private void BuildBindings()
	{
		foreach (var bindingConfig in _bindingConfigurations)
		{
			var bindingBuilder = new BindingConfigurationBuilder(_transportAdapters, _profileRegistry);
			bindingConfig(bindingBuilder);
			var binding = bindingBuilder.Build();
			_bindingRegistry.RegisterBinding(binding);
		}
	}

	private PipelineRuntimeEntry BuildPipeline(
		IServiceProvider serviceProvider,
		string name,
		Action<IPipelineBuilder> configure)
	{
		var applicabilityStrategy = serviceProvider.GetService<IMiddlewareApplicabilityStrategy>();
		var pipelineBuilder = new PipelineBuilder(name, serviceProvider, applicabilityStrategy);

		foreach (var middlewareType in _globalMiddleware)
		{
			// Pass the type through: it is already in hand here, and without it an unresolvable
			// global middleware appears in the build failure as an unnamed factory entry, which
			// tells a consumer nothing about what to register.
			_ = pipelineBuilder.Use(
				middlewareType,
				sp => (IDispatchMiddleware)sp.GetRequiredService(middlewareType));
		}

		// Union in middleware registered directly as IDispatchMiddleware (services.TryAddEnumerable(
		// ServiceDescriptor.Singleton<IDispatchMiddleware, T>()) -- e.g. AddOrderingValidation()), not
		// just _globalMiddleware (the Use<T>() list). Without this, that documented registration path
		// is silently inert here: the legacy AddDispatch(Assembly) entry point already composes its
		// pipeline from serviceProvider.GetServices<IDispatchMiddleware>() (see
		// DispatchServiceCollectionExtensions.AddDispatchPipeline), but the modern AddDispatch(configure)
		// path -- reached only through THIS method -- never read that registration at all. No exception,
		// no log: a consumer's validation middleware simply never ran.
		//
		// Sourced through the same serviceProvider this whole method already resolves _globalMiddleware
		// from, so it is the identical single resolution path the invoker above depends on (:307-315) --
		// not a second, divergent one. Deduplicated by concrete type against _globalMiddleware, so a
		// middleware registered both ways (Use<T>() AND TryAddEnumerable) runs once, not twice.
		//
		// ORDER: DispatchPipeline sorts the final list by each middleware's own declared Stage (see
		// DispatchPipeline.cs), so where an entry lands in THIS list only breaks a tie between two
		// middleware sharing the identical Stage -- it does not by itself decide execution order.
		// Appended AFTER _globalMiddleware, so an explicit Use<T>() registration wins that tie over a
		// DI-only one, which was already the effective behavior for _globalMiddleware relative to itself
		// (registration order) and is the more explicit of the two registration paths.
		//
		// Enumerated inside a scope, and registered as a FACTORY rather than as the instance enumerated
		// here. The provider this method receives is the root (the runtime state is a singleton), so a
		// middleware registered Scoped cannot be enumerated from it at all under scope validation -- the
		// container refuses IEnumerable<IDispatchMiddleware> outright. Enumerating in a scope fixes that,
		// and then the instances belong to a scope that ends with this method: only the TYPE survives it,
		// and the factory resolves the middleware again from whichever provider the pipeline build or a
		// dispatch supplies.
		using (var enumerationScope = serviceProvider.GetService<IServiceScopeFactory>()?.CreateScope())
		{
			var enumerationProvider = enumerationScope?.ServiceProvider ?? serviceProvider;

			foreach (var middleware in enumerationProvider.GetServices<IDispatchMiddleware>())
			{
				var middlewareType = middleware.GetType();
				if (_globalMiddleware.Contains(middlewareType))
				{
					continue;
				}

				_ = pipelineBuilder.Use(middlewareType, ResolveFromRegisteredMiddleware(middlewareType));
			}
		}

		configure(pipelineBuilder);

		if (!pipelineBuilder.HasMiddlewareRegistered)
		{
			// Fast default path: no middleware unless explicitly opted-in via profile or middleware registration.
			_ = pipelineBuilder.UseProfile(DefaultPipelineProfiles.Direct);
		}

		var pipeline = pipelineBuilder.Build();
		return new PipelineRuntimeEntry(
			name,
			pipeline,
			pipelineBuilder.ConfiguredMiddlewareTypes,
			pipelineBuilder.ResolvedMiddleware);
	}

	/// <summary>
	/// Resolves one middleware out of the registered <see cref="IDispatchMiddleware"/> set by concrete
	/// type, from whichever provider the caller supplies.
	/// </summary>
	/// <remarks>
	/// The set is registered as an enumerable, so there is no per-type registration to resolve directly.
	/// Matching on the concrete type is what makes the entry re-resolvable rather than captured, which is
	/// what a Scoped registration in that set requires. The documented registration for this path is
	/// Singleton, and a Singleton entry is resolved once during the pipeline build and held, so this
	/// enumeration is not on the dispatch path for it.
	/// </remarks>
	private static Func<IServiceProvider, IDispatchMiddleware> ResolveFromRegisteredMiddleware(Type middlewareType) =>
		serviceProvider =>
		{
			foreach (var candidate in serviceProvider.GetServices<IDispatchMiddleware>())
			{
				if (candidate.GetType() == middlewareType)
				{
					return candidate;
				}
			}

			throw new InvalidOperationException(
				$"'{middlewareType.FullName}' was registered as an {nameof(IDispatchMiddleware)} when the dispatch " +
				"pipeline was composed, but is no longer resolvable from the current scope.");
		};

	private sealed class DispatcherHolder
	{
		private IDispatcher? _dispatcher;

		public void Set(IDispatcher dispatcher)
		{
			ArgumentNullException.ThrowIfNull(dispatcher);
			_dispatcher = dispatcher;
		}

		public IDispatcher GetOrThrow() =>
			_dispatcher ?? throw new InvalidOperationException(Resources.DispatchBuilder_DispatcherNotInitialized);
	}

	private sealed class DeferredDispatcher(DispatcherHolder holder) : IDispatcher, IStreamingDispatcher, IProgressDispatcher
	{
		/// <inheritdoc />
		public IServiceProvider? ServiceProvider => holder.GetOrThrow().ServiceProvider;

		public Task<IMessageResult> DispatchAsync<TMessage>(
			TMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TMessage : IDispatchMessage =>
			holder.GetOrThrow().DispatchAsync(message, context, cancellationToken);

		public Task<IMessageResult<TResponse>> DispatchAsync<TMessage, TResponse>(
			TMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TMessage : IDispatchAction<TResponse> =>
			holder.GetOrThrow().DispatchAsync<TMessage, TResponse>(message, context, cancellationToken);

		public IAsyncEnumerable<TOutput> DispatchStreamingAsync<TDocument, TOutput>(
			TDocument document,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TDocument : IDispatchDocument =>
			((IStreamingDispatcher)holder.GetOrThrow()).DispatchStreamingAsync<TDocument, TOutput>(document, context, cancellationToken);

		public Task DispatchStreamAsync<TDocument>(
			IAsyncEnumerable<TDocument> documents,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TDocument : IDispatchDocument =>
			((IStreamingDispatcher)holder.GetOrThrow()).DispatchStreamAsync(documents, context, cancellationToken);

		public IAsyncEnumerable<TOutput> DispatchTransformStreamAsync<TInput, TOutput>(
			IAsyncEnumerable<TInput> input,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TInput : IDispatchDocument =>
			((IStreamingDispatcher)holder.GetOrThrow()).DispatchTransformStreamAsync<TInput, TOutput>(input, context, cancellationToken);

		public Task DispatchWithProgressAsync<TDocument>(
			TDocument document,
			IMessageContext context,
			IProgress<DocumentProgress> progress,
			CancellationToken cancellationToken)
			where TDocument : IDispatchDocument =>
			((IProgressDispatcher)holder.GetOrThrow()).DispatchWithProgressAsync(document, context, progress, cancellationToken);
	}

	private sealed class DispatchRuntimeState(
		IReadOnlyDictionary<string, PipelineRuntimeEntry> pipelines,
		PipelineRuntimeEntry defaultPipeline,
		TransportBindingRegistry bindingRegistry)
	{
		public IReadOnlyDictionary<string, PipelineRuntimeEntry> Pipelines { get; } = pipelines;

		public PipelineRuntimeEntry DefaultPipeline { get; } = defaultPipeline;

		public TransportBindingRegistry BindingRegistry { get; } = bindingRegistry;
	}

	private sealed class PipelineRuntimeEntry(
		string name,
		IDispatchPipeline pipeline,
		IReadOnlyList<Type> middlewareTypes,
		IReadOnlyList<IDispatchMiddleware> resolvedMiddleware)
	{
		public string Name { get; } = name;

		public IDispatchPipeline Pipeline { get; } = pipeline;

		public IReadOnlyList<Type> MiddlewareTypes { get; } = middlewareTypes;

		public IReadOnlyList<IDispatchMiddleware> ResolvedMiddleware { get; } = resolvedMiddleware;
	}

	#region LoggerMessage Definitions

	[LoggerMessage(CoreEventId.SynthesisBeginning, LogLevel.Information,
		"No pipeline profiles registered. Synthesizing default profiles.")]
	private static partial void LogNoPipelineProfilesRegisteredSynthesizing(ILogger logger);

	[LoggerMessage(CoreEventId.ProfileSynthesized, LogLevel.Information,
		"Registered synthesized profile: {ProfileName}")]
	private static partial void LogRegisteredSynthesizedProfile(ILogger logger, string profileName);

	[LoggerMessage(CoreEventId.MappedMessageKinds, LogLevel.Debug,
		"Mapped message kind {MessageKind} to profile {ProfileName}")]
	private static partial void LogMappedMessageKindToProfile(ILogger logger, string messageKind, string profileName);

	[LoggerMessage(CoreEventId.SynthesisWarning, LogLevel.Warning,
		"Pipeline synthesis warning: {WarningMessage}")]
	private static partial void LogPipelineSynthesisWarning(ILogger logger, string warningMessage);

	#endregion
}

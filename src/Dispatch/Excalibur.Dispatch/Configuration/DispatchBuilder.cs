// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CoreDispatcher = Excalibur.Dispatch.Delivery.Dispatcher;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Fluent builder for configuring the Excalibur framework.
/// </summary>
public sealed partial class DispatchBuilder : IDispatchBuilder, IDisposable
{
	private readonly Dictionary<string, Action<IPipelineBuilder>> _pipelineConfigurations = [];
	private readonly Dictionary<string, ITransportAdapter> _transportAdapters = [];
	private readonly List<Action<IBindingConfigurationBuilder>> _bindingConfigurations = [];
	private readonly List<Type> _globalMiddleware = [];
	private readonly DispatchOptions _options = new();
	private readonly PipelineProfileRegistry _profileRegistry;
	private readonly TransportBindingRegistry _bindingRegistry;
	private volatile bool _disposed;

	/// <summary>
	/// Gets a value indicating whether any handler registrations have been made via
	/// <c>AddHandlersFromAssembly</c>.
	/// </summary>
	internal bool HasHandlerRegistrations { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchBuilder"/> class.
	/// Creates a new Dispatch builder.
	/// </summary>
	public DispatchBuilder(IServiceCollection services)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));
		_profileRegistry = new PipelineProfileRegistry();
		_bindingRegistry = new TransportBindingRegistry();

		// Register core services — first-wins TryAdd semantics so a consumer's
		// explicit pre-registration of any of these services survives a
		// subsequent AddDispatch(configure) call.
		// IMiddlewareApplicabilityStrategy is already TryAdd-registered by
		// AddDispatchPipeline (which always runs before this constructor via the
		// AddDispatch entry points); the redundant ctor registration was the
		// S794 D1 row 3 drifter and is intentionally removed here.
		// [S794 bd-ffecs4 rows 1+2]
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
		// default [S849 K keystone rb4g4b].
		//
		// BUT a consumer's pre-AddDispatch registration of IPipelineProfileRegistry — in ANY
		// form (instance, implementation-type, OR factory) — MUST win over the builder
		// (first-wins / consumer-override, S794 bd-ffecs4). A consumer who replaces the registry
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
		// perf path. [S849 txmwh9]
		var existing = Services.FirstOrDefault(
			static d => d.ServiceType == typeof(IPipelineProfileRegistry));
		var isFrameworkDefault =
			existing is { ImplementationInstance: null, ImplementationFactory: null }
			&& existing.ImplementationType == typeof(PipelineProfileRegistry);
		if (existing is null || isFrameworkDefault)
		{
			_ = Services.Replace(
				ServiceDescriptor.Singleton<IPipelineProfileRegistry>(_profileRegistry));
		}
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
		_globalMiddleware.Add(typeof(TMiddleware));
		_ = Services.AddScoped(typeof(TMiddleware));
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
		// point. [S794 bd-ffecs4 rows 5+7 / COMPASS msg 1480]
		if (Services.Any(static d => d.ServiceType == typeof(DispatchRuntimeState)))
		{
			return new DeferredDispatcher(Services
				.First(static d => d.ServiceType == typeof(DispatcherHolder))
				.ImplementationInstance is DispatcherHolder holder
					? holder
					: new DispatcherHolder());
		}

		// Register the synthesizer used during runtime construction
		Services.TryAddSingleton<PipelineProfileSynthesizer>();

		RegisterOptions();

		foreach (var middlewareType in _globalMiddleware)
		{
			_ = Services.AddScoped(middlewareType);
		}

		// Register runtime state that materializes pipelines using the caller's provider scope.
		// Reached at most once per service collection due to the guard above plus the
		// AddDispatch(configure) entry-point guard.
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

		// PERF: Auto-promote eligible transient handlers to singleton when opted in.
		if (_options.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton)
		{
			HandlerLifetimeAnalyzer.PromoteEligibleHandlers(Services);
		}

		return new DeferredDispatcher(dispatcherHolder);
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

		_bindingRegistry?.Dispose();
		_disposed = true;
	}

	private void RegisterOptions()
	{
		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<DispatchOptions>, DispatchOptionsValidator>());

		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<Options.Core.InMemoryBusOptions>, Options.Core.InMemoryBusOptionsValidator>());

		Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<Options.Core.CompressionOptions>, Options.Core.CompressionOptionsValidator>());

		_ = Services.AddOptions<DispatchOptions>()
			.Configure(opt =>
			{
				opt.DefaultTimeout = _options.DefaultTimeout;
				opt.MaxConcurrency = _options.MaxConcurrency;
				opt.UseLightMode = _options.UseLightMode;
				opt.MessageBufferSize = _options.MessageBufferSize;
				opt.EnablePipelineSynthesis = _options.EnablePipelineSynthesis;
				opt.Features = _options.Features;
				opt.CrossCutting = _options.CrossCutting;

				opt.Inbox.Enabled = _options.Inbox.Enabled;
				opt.Inbox.DeduplicationExpiryHours = _options.Inbox.DeduplicationExpiryHours;
				opt.Inbox.AckAfterHandle = _options.Inbox.AckAfterHandle;
				opt.Inbox.MaxRetries = _options.Inbox.MaxRetries;
				opt.Inbox.RetryDelayMinutes = _options.Inbox.RetryDelayMinutes;
				opt.Inbox.MaxRetention = _options.Inbox.MaxRetention;
				opt.Inbox.CleanupInterval = _options.Inbox.CleanupInterval;

				opt.Outbox.Enabled = _options.Outbox.Enabled;
				opt.Outbox.BatchSize = _options.Outbox.BatchSize;
				opt.Outbox.PublishIntervalMs = _options.Outbox.PublishIntervalMs;
				opt.Outbox.MaxRetries = _options.Outbox.MaxRetries;
				opt.Outbox.SentMessageRetention = _options.Outbox.SentMessageRetention;
				opt.Outbox.UseInMemoryStorage = _options.Outbox.UseInMemoryStorage;

				opt.Consumer.Dedupe.Enabled = _options.Consumer.Dedupe.Enabled;
				opt.Consumer.Dedupe.DefaultExpiry = _options.Consumer.Dedupe.DefaultExpiry;
				opt.Consumer.Dedupe.CleanupInterval = _options.Consumer.Dedupe.CleanupInterval;
				opt.Consumer.AckAfterHandle = _options.Consumer.AckAfterHandle;
				opt.Consumer.MaxConcurrentMessages = _options.Consumer.MaxConcurrentMessages;
				opt.Consumer.VisibilityTimeout = _options.Consumer.VisibilityTimeout;
				opt.Consumer.MaxRetries = _options.Consumer.MaxRetries;
			})
			.ValidateOnStart();
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
			_ = pipelineBuilder.Use(sp => (IDispatchMiddleware)sp.GetRequiredService(middlewareType));
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

		[UnconditionalSuppressMessage("Trimming", "IL2046",
			Justification = "IDispatcher interface is kept clean for AOT consumers. DeferredDispatcher delegates to real Dispatcher which handles AOT branching.")]
		[UnconditionalSuppressMessage("AOT", "IL3051",
			Justification = "IDispatcher interface is kept clean for AOT consumers. DeferredDispatcher delegates to real Dispatcher which handles AOT branching.")]
		public Task<IMessageResult> DispatchAsync<TMessage>(
			TMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TMessage : IDispatchMessage =>
			holder.GetOrThrow().DispatchAsync(message, context, cancellationToken);

		[UnconditionalSuppressMessage("Trimming", "IL2046",
			Justification = "IDispatcher interface is kept clean for AOT consumers. DeferredDispatcher delegates to real Dispatcher which handles AOT branching.")]
		[UnconditionalSuppressMessage("AOT", "IL3051",
			Justification = "IDispatcher interface is kept clean for AOT consumers. DeferredDispatcher delegates to real Dispatcher which handles AOT branching.")]
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

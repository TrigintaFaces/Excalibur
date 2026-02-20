// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Configuration;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using CoreDispatcher = Excalibur.Dispatch.Delivery.Dispatcher;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Fluent builder for configuring the Dispatch messaging framework.
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
	/// Initializes a new instance of the <see cref="DispatchBuilder"/> class.
	/// Creates a new Dispatch builder.
	/// </summary>
	public DispatchBuilder(IServiceCollection services)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));
		_profileRegistry = new PipelineProfileRegistry();
		_bindingRegistry = new TransportBindingRegistry();

		// Register core services
		_ = Services.AddSingleton(_profileRegistry);
		_ = Services.AddSingleton(_bindingRegistry);
		_ = Services.AddSingleton<IMiddlewareApplicabilityStrategy, DefaultMiddlewareApplicabilityStrategy>();
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
		_ = Services.AddScoped(typeof(IDispatchMiddleware), typeof(TMiddleware));
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
	internal IDispatcher Build()
	{
		// Register the synthesizer used during runtime construction
		_ = Services.AddSingleton<PipelineProfileSynthesizer>();

		RegisterOptions();

		foreach (var middlewareType in _globalMiddleware)
		{
			_ = Services.AddScoped(middlewareType);
			_ = Services.AddScoped(typeof(IDispatchMiddleware), middlewareType);
		}

		// Register runtime state that materializes pipelines using the caller's provider scope
		_ = Services.AddSingleton(BuildRuntimeState);

		// Ensure the configured dispatch pipeline is the one used by the dispatcher
		_ = Services.Replace(ServiceDescriptor.Singleton(sp =>
			sp.GetRequiredService<DispatchRuntimeState>().DefaultPipeline.Pipeline));

		var dispatcherHolder = new DispatcherHolder();
		_ = Services.AddSingleton(dispatcherHolder);

		_ = Services.AddSingleton(sp =>
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

	private void RegisterOptions() =>
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
				opt.Consumer.Dedupe.ExpiryHours = _options.Consumer.Dedupe.ExpiryHours;
				opt.Consumer.Dedupe.CleanupInterval = _options.Consumer.Dedupe.CleanupInterval;
				opt.Consumer.AckAfterHandle = _options.Consumer.AckAfterHandle;
				opt.Consumer.MaxConcurrentMessages = _options.Consumer.MaxConcurrentMessages;
				opt.Consumer.VisibilityTimeout = _options.Consumer.VisibilityTimeout;
				opt.Consumer.MaxRetries = _options.Consumer.MaxRetries;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

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
		return new PipelineRuntimeEntry(name, pipeline, pipelineBuilder.ConfiguredMiddlewareTypes);
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

	private sealed class DeferredDispatcher(DispatcherHolder holder) : IDispatcher
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
			holder.GetOrThrow().DispatchStreamingAsync<TDocument, TOutput>(document, context, cancellationToken);

		public Task DispatchStreamAsync<TDocument>(
			IAsyncEnumerable<TDocument> documents,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TDocument : IDispatchDocument =>
			holder.GetOrThrow().DispatchStreamAsync(documents, context, cancellationToken);

		public IAsyncEnumerable<TOutput> DispatchTransformStreamAsync<TInput, TOutput>(
			IAsyncEnumerable<TInput> input,
			IMessageContext context,
			CancellationToken cancellationToken)
			where TInput : IDispatchDocument =>
			holder.GetOrThrow().DispatchTransformStreamAsync<TInput, TOutput>(input, context, cancellationToken);

		public Task DispatchWithProgressAsync<TDocument>(
			TDocument document,
			IMessageContext context,
			IProgress<DocumentProgress> progress,
			CancellationToken cancellationToken)
			where TDocument : IDispatchDocument =>
			holder.GetOrThrow().DispatchWithProgressAsync(document, context, progress, cancellationToken);
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

	private sealed class PipelineRuntimeEntry(string name, IDispatchPipeline pipeline, IReadOnlyList<Type> middlewareTypes)
	{
		public string Name { get; } = name;

		public IDispatchPipeline Pipeline { get; } = pipeline;

		public IReadOnlyList<Type> MiddlewareTypes { get; } = middlewareTypes;
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

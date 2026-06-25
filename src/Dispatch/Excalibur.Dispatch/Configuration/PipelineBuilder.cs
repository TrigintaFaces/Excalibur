// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Middleware;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Fluent builder for configuring message processing pipelines.
/// </summary>
public sealed partial class PipelineBuilder : IPipelineBuilder
{
	private readonly IServiceProvider _serviceProvider;
	private readonly List<MiddlewareRegistration> _middlewares = [];
	private readonly IMiddlewareApplicabilityStrategy? _applicabilityStrategy;
	private MessageKinds? _messageKinds;
	private IReadOnlyList<IDispatchMiddleware> _resolvedMiddleware = [];

	/// <summary>
	/// Initializes a new instance of the <see cref="PipelineBuilder"/> class.
	/// Creates a new pipeline builder.
	/// </summary>
	public PipelineBuilder(
		string name,
		IServiceProvider serviceProvider,
		IMiddlewareApplicabilityStrategy? applicabilityStrategy = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(serviceProvider);

		Name = name;
		_serviceProvider = serviceProvider;
		_applicabilityStrategy = applicabilityStrategy;
	}

	/// <inheritdoc />
	public string Name { get; }

	internal bool HasMiddlewareRegistered => _middlewares.Count > 0;

	internal IReadOnlyList<Type> ConfiguredMiddlewareTypes =>
		_middlewares
			.Select(static registration => registration.Type)
			.Where(static type => type is not null)
			.Cast<Type>()
			.ToArray();

	/// <summary>
	/// Gets the middleware instances resolved by the most recent <see cref="Build"/> call.
	/// This is the canonical resolve-safe materialization (unregistered/unconstructable
	/// profile middleware are skipped), reused by the dispatcher's invoker so there is a
	/// single resolution path.
	/// </summary>
	internal IReadOnlyList<IDispatchMiddleware> ResolvedMiddleware => _resolvedMiddleware;

	/// <inheritdoc />
	public IPipelineBuilder Use<TMiddleware>()
		where TMiddleware : IDispatchMiddleware
	{
		var capturedKinds = _messageKinds;
		_middlewares.Add(new MiddlewareRegistration(
			typeof(TMiddleware),
			capturedKinds.HasValue
				? sp => new MessageKindFilteringMiddleware(
					sp.GetRequiredService<TMiddleware>(), capturedKinds.Value)
				: static sp => sp.GetRequiredService<TMiddleware>(),
			stage: null,
			condition: null));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder Use(Func<IServiceProvider, IDispatchMiddleware> middlewareFactory)
	{
		ArgumentNullException.ThrowIfNull(middlewareFactory);

		var capturedKinds = _messageKinds;
		_middlewares.Add(new MiddlewareRegistration(
			type: null,
			capturedKinds.HasValue
				? sp => new MessageKindFilteringMiddleware(
					middlewareFactory(sp), capturedKinds.Value)
				: middlewareFactory,
			stage: null,
			condition: null));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder UseAt<TMiddleware>(DispatchMiddlewareStage stage)
		where TMiddleware : IDispatchMiddleware
	{
		var capturedKinds = _messageKinds;
		_middlewares.Add(new MiddlewareRegistration(
			typeof(TMiddleware),
			capturedKinds.HasValue
				? sp => new MessageKindFilteringMiddleware(
					sp.GetRequiredService<TMiddleware>(), capturedKinds.Value)
				: static sp => sp.GetRequiredService<TMiddleware>(),
			stage,
			condition: null));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder UseWhen<TMiddleware>(Func<IServiceProvider, bool> condition)
		where TMiddleware : IDispatchMiddleware
	{
		ArgumentNullException.ThrowIfNull(condition);

		var capturedKinds = _messageKinds;
		_middlewares.Add(new MiddlewareRegistration(
			typeof(TMiddleware),
			capturedKinds.HasValue
				? sp => new MessageKindFilteringMiddleware(
					sp.GetRequiredService<TMiddleware>(), capturedKinds.Value)
				: static sp => sp.GetRequiredService<TMiddleware>(),
			stage: null,
			condition));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder ForMessageKinds(MessageKinds messageKinds)
	{
		_messageKinds = messageKinds;
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder UseProfile(string profileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

		// Look up profile from registry (would be injected)
		var registry = _serviceProvider.GetService<IPipelineProfileRegistry>() ?? throw new InvalidOperationException(
				Resources.PipelineBuilder_ProfileRegistryNotRegistered);

		var profile = registry.GetProfile(profileName) ??
			throw new ArgumentException(
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.PipelineBuilder_ProfileNotFoundFormat,
					profileName),
				nameof(profileName));

		return UseProfile(profile);
	}

	/// <inheritdoc />
	public IPipelineBuilder UseProfile(IPipelineProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile);

		// Clear existing middleware and apply profile
		_middlewares.Clear();

		// Add all middleware from the profile. Resolve-safe: a profile's middleware
		// list may name opt-in middleware whose services the consumer never registered
		// (only OutboxStaging is registered by default; the other canonical middleware
		// have required feature-service ctor deps and are opt-in). Use GetService (not
		// GetRequiredService) so an unregistered middleware resolves to null and is
		// skipped+logged in Build(), rather than throwing — the built pipeline is the
		// REGISTERED subset of the profile's middleware in canonical order (Microsoft
		// fail-open). This makes "profile middleware not DI-registered → throw"
		// structurally inexpressible.
		foreach (var middlewareType in profile.MiddlewareTypes)
		{
			_middlewares.Add(new MiddlewareRegistration(
				middlewareType,
				sp => sp.GetService(middlewareType) as IDispatchMiddleware,
				stage: null,
				condition: null));
		}

		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder Clear()
	{
		_middlewares.Clear();
		return this;
	}

	/// <inheritdoc />
	public IDispatchPipeline Build()
	{
		// Resolve all middleware instances
		var resolvedMiddleware = new List<IDispatchMiddleware>();

		foreach (var registration in _middlewares)
		{
			// Check condition if present
			if (registration.Condition != null && !registration.Condition(_serviceProvider))
			{
				continue;
			}

			// Resolve-safe materialization. A profile's middleware list may name opt-in
			// middleware the consumer never registered, or middleware whose required
			// constructor dependencies are not registered (e.g. OutboxStagingMiddleware
			// needs IOutboxStore; the .NET container throws while activating it even
			// though the C# parameter is nullable). Either case — a null result OR an
			// activation failure — means the middleware cannot be materialized; skip it
			// and log, rather than throwing on the configured-pipeline build (Microsoft
			// fail-open). The built pipeline is the REGISTERED, CONSTRUCTABLE subset of
			// the profile's middleware in canonical order. OutboxStaging's own no-store
			// self-guard still governs the staging no-op when its store IS resolvable.
			IDispatchMiddleware? middleware;
			try
			{
				middleware = registration.Factory(_serviceProvider);
			}
			catch (InvalidOperationException)
			{
				LogSkippedMiddleware(registration.Type);
				continue;
			}

			if (middleware is null)
			{
				LogSkippedMiddleware(registration.Type);
				continue;
			}

			// Override stage if specified in registration
			if (registration.Stage.HasValue && middleware is IConfigurableMiddleware configurable)
			{
				configurable.Stage = registration.Stage.Value;
			}

			resolvedMiddleware.Add(middleware);
		}

		// Cache the resolved instances so the dispatcher's invoker can reuse this single
		// resolution path rather than re-resolving (which would re-trigger the throws).
		_resolvedMiddleware = resolvedMiddleware;

		// Create pipeline with resolved middleware
		return new DispatchPipeline(resolvedMiddleware, _applicabilityStrategy);
	}

	private void LogSkippedMiddleware(Type? middlewareType)
	{
		if (middlewareType is null)
		{
			return;
		}

		var logger = _serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<PipelineBuilder>();
		if (logger is not null)
		{
			LogPipelineMiddlewareSkipped(logger, middlewareType.FullName ?? middlewareType.Name);
		}
	}

	/// <summary>
	/// Internal registration for middleware with metadata.
	/// </summary>
	private sealed class MiddlewareRegistration(
		Type? type,
		Func<IServiceProvider, IDispatchMiddleware?> factory,
		DispatchMiddlewareStage? stage,
		Func<IServiceProvider, bool>? condition)
	{
		public Type? Type { get; } = type;

		public Func<IServiceProvider, IDispatchMiddleware?> Factory { get; } = factory;

		public DispatchMiddlewareStage? Stage { get; } = stage;

		public Func<IServiceProvider, bool>? Condition { get; } = condition;
	}

	[LoggerMessage(CoreEventId.InvokerMiddlewareSkipped, LogLevel.Debug,
		"Skipping configured pipeline middleware {MiddlewareType}: not registered in the service provider.")]
	private static partial void LogPipelineMiddlewareSkipped(ILogger logger, string middlewareType);
}

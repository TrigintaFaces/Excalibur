// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Pipeline;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Fluent builder for configuring message processing pipelines.
/// </summary>
public sealed class PipelineBuilder : IPipelineBuilder
{
	private readonly IServiceProvider _serviceProvider;
	private readonly List<MiddlewareRegistration> _middlewares = [];
	private readonly IMiddlewareApplicabilityStrategy? _applicabilityStrategy;

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

	/// <inheritdoc />
	public IPipelineBuilder Use<TMiddleware>()
		where TMiddleware : IDispatchMiddleware
	{
		_middlewares.Add(new MiddlewareRegistration(
			typeof(TMiddleware),
			static sp => sp.GetRequiredService<TMiddleware>(),
			stage: null,
			condition: null));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder Use(Func<IServiceProvider, IDispatchMiddleware> middlewareFactory)
	{
		ArgumentNullException.ThrowIfNull(middlewareFactory);

		_middlewares.Add(new MiddlewareRegistration(
			type: null,
			middlewareFactory,
			stage: null,
			condition: null));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder UseAt<TMiddleware>(DispatchMiddlewareStage stage)
		where TMiddleware : IDispatchMiddleware
	{
		_middlewares.Add(new MiddlewareRegistration(
			typeof(TMiddleware),
			static sp => sp.GetRequiredService<TMiddleware>(),
			stage,
			condition: null));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder UseWhen<TMiddleware>(Func<IServiceProvider, bool> condition)
		where TMiddleware : IDispatchMiddleware
	{
		ArgumentNullException.ThrowIfNull(condition);

		_middlewares.Add(new MiddlewareRegistration(
			typeof(TMiddleware),
			static sp => sp.GetRequiredService<TMiddleware>(),
			stage: null,
			condition));
		return this;
	}

	/// <inheritdoc />
	public IPipelineBuilder ForMessageKinds(MessageKinds messageKinds) => this;

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

		// Add all middleware from the profile
		foreach (var middlewareType in profile.MiddlewareTypes)
		{
			_middlewares.Add(new MiddlewareRegistration(
				middlewareType,
				sp => (IDispatchMiddleware)sp.GetRequiredService(middlewareType),
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

			var middleware = registration.Factory(_serviceProvider);

			// Override stage if specified in registration
			if (registration.Stage.HasValue && middleware is IConfigurableMiddleware configurable)
			{
				configurable.Stage = registration.Stage.Value;
			}

			resolvedMiddleware.Add(middleware);
		}

		// Create pipeline with resolved middleware
		return new DispatchPipeline(resolvedMiddleware, _applicabilityStrategy);
	}

	/// <summary>
	/// Internal registration for middleware with metadata.
	/// </summary>
	private sealed class MiddlewareRegistration(
		Type? type,
		Func<IServiceProvider, IDispatchMiddleware> factory,
		DispatchMiddlewareStage? stage,
		Func<IServiceProvider, bool>? condition)
	{
		public Type? Type { get; } = type;

		public Func<IServiceProvider, IDispatchMiddleware> Factory { get; } = factory;

		public DispatchMiddlewareStage? Stage { get; } = stage;

		public Func<IServiceProvider, bool>? Condition { get; } = condition;
	}
}

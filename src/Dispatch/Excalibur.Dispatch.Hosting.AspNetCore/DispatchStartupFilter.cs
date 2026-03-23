// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Configuration;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.AspNetCore;

/// <summary>
/// Validates the dispatch pipeline configuration at application startup, before the first request.
/// </summary>
/// <remarks>
/// Runs during <c>WebApplication.Build()</c> via <see cref="IStartupFilter"/>,
/// providing earlier feedback than the <c>PipelineValidationHostedService</c>.
/// Checks for missing required services and conflicting configuration.
/// </remarks>
internal sealed partial class DispatchStartupFilter(
	IServiceProvider serviceProvider,
	ILogger<DispatchStartupFilter> logger) : IStartupFilter
{
	/// <inheritdoc />
	public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
	{
		ValidateDispatchServices();
		return next;
	}

	private void ValidateDispatchServices()
	{
		// Verify IDispatcher is registered (core requirement)
		var dispatcher = serviceProvider.GetService<IDispatcher>();
		if (dispatcher is null)
		{
			LogMissingService(logger, nameof(IDispatcher), "AddDispatch()");
			return;
		}

		// Verify at least one middleware is registered
		var middlewares = serviceProvider.GetServices<IDispatchMiddleware>();
		if (!middlewares.Any())
		{
			LogEmptyPipeline(logger);
		}

		// Check for outbox configuration without store
		var outboxOptions = serviceProvider.GetService<IOptions<OutboxConfigurationOptions>>();
		if (outboxOptions?.Value.Enabled == true)
		{
			var outboxStore = serviceProvider.GetKeyedService<IOutboxStore>("default");
			if (outboxStore is null)
			{
				LogMissingOutboxStore(logger);
			}
		}

		// Detect keyed service configuration for DI collision prevention
		ValidateKeyedServiceRegistrations();

		// Check if observability is configured
		var meterFactory = serviceProvider.GetService<System.Diagnostics.Metrics.IMeterFactory>();
		if (meterFactory is null)
		{
			LogNoObservability(logger);
		}
	}

	private void ValidateKeyedServiceRegistrations()
	{
		// Check critical keyed service interfaces have a "default" alias registered.
		// When multiple providers are configured but no default is set, warn the consumer.
		ValidateKeyedDefault<IOutboxStore>("IOutboxStore", "AddSqlServerOutboxStore()/AddPostgresOutboxStore()");
		ValidateKeyedDefault<IInboxStore>("IInboxStore", "AddSqlServerInboxStore()/AddPostgresInboxStore()");
	}

	private void ValidateKeyedDefault<TService>(string serviceName, string registrationHint) where TService : class
	{
		try
		{
			var service = serviceProvider.GetKeyedService<TService>("default");
			if (service is not null)
			{
				LogKeyedServiceResolved(logger, serviceName, service.GetType().Name);
			}
		}
		catch (InvalidOperationException)
		{
			// Keyed service resolution failed -- multiple providers may be registered
			// without a default alias. Log a warning.
			LogKeyedServiceCollision(logger, serviceName, registrationHint);
		}
	}

	[LoggerMessage(2600, LogLevel.Error,
		"Required service '{ServiceName}' is not registered. Register it via {RegistrationMethod}.")]
	private static partial void LogMissingService(ILogger logger, string serviceName, string registrationMethod);

	[LoggerMessage(2601, LogLevel.Warning,
		"No dispatch middleware registered. The pipeline is empty. Register middleware via AddDispatch(builder => builder.UseMiddleware<T>()) or enable pipeline synthesis.")]
	private static partial void LogEmptyPipeline(ILogger logger);

	[LoggerMessage(2602, LogLevel.Warning,
		"Outbox is enabled but no IOutboxStore is registered. Register an outbox store via AddOutbox<TStore>() or UseOutbox().")]
	private static partial void LogMissingOutboxStore(ILogger logger);

	[LoggerMessage(2603, LogLevel.Debug,
		"Keyed service '{ServiceName}' resolved to '{ImplementationType}' via \"default\" key.")]
	private static partial void LogKeyedServiceResolved(ILogger logger, string serviceName, string implementationType);

	[LoggerMessage(2604, LogLevel.Warning,
		"Keyed service '{ServiceName}' could not resolve \"default\" key. Multiple providers may be registered without a default alias. Register via {RegistrationHint} or call SetDefault{ServiceName}().")]
	private static partial void LogKeyedServiceCollision(ILogger logger, string serviceName, string registrationHint);

	[LoggerMessage(2605, LogLevel.Information,
		"No IMeterFactory registered. Dispatch metrics and tracing are disabled. " +
		"Add AddDispatchObservability() or register IMeterFactory for production monitoring.")]
	private static partial void LogNoObservability(ILogger logger);
}

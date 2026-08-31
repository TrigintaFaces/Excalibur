// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// A missing required service throws, so a host whose messaging cannot work does not start;
/// configuration that is merely sub-optimal (an empty pipeline, absent metrics) is logged instead.
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
		// Verify IDispatcher is registered (core requirement). A host without a dispatcher cannot serve a
		// single request, so failing to start is strictly better than starting healthy and failing later.
		_ = serviceProvider.GetService<IDispatcher>()
			?? throw new InvalidOperationException(
				$"Required service '{nameof(IDispatcher)}' is not registered. Register it via AddDispatch().");

		// Verify at least one middleware is registered
		var middlewares = serviceProvider.GetServices<IDispatchMiddleware>();
		if (!middlewares.Any())
		{
			LogEmptyPipeline(logger);
		}

		// Outbox enabled with no store is unrecoverable and silent: every staged message is accepted and
		// never persisted, so the loss surfaces as missing downstream messages long after the fact and far
		// from its cause. The keyed "default" lookup is the same resolution the outbox pipeline itself
		// performs, so this fails exactly when the runtime would have found nothing.
		var outboxOptions = serviceProvider.GetService<IOptions<OutboxConfigurationOptions>>();
		if (outboxOptions?.Value.Enabled == true)
		{
			_ = serviceProvider.GetKeyedService<IOutboxStore>("default")
				?? throw new InvalidOperationException(
					$"The outbox is enabled but required service '{nameof(IOutboxStore)}' is not registered. "
					+ "Register an outbox store via AddSqlServerOutboxStore()/AddPostgresOutboxStore(), "
					+ "or disable the outbox.");
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
		// Check critical keyed service interfaces resolve through their "default" alias. An alias that
		// cannot resolve is a broken registration, not a preference, so resolution failure throws.
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
		catch (InvalidOperationException ex)
		{
			// The "default" alias exists but could not be resolved -- typically it delegates to a provider
			// key nothing registered. Whatever depends on this service would fail at its first use, so stop
			// here instead. The inner exception carries the resolution failure.
			throw new InvalidOperationException(
				$"Required service '{serviceName}' is registered under the \"default\" key but could not be "
				+ $"resolved. Register a provider via {registrationHint}.",
				ex);
		}
	}

	[LoggerMessage(2601, LogLevel.Warning,
		"No dispatch middleware registered. The pipeline is empty. Register middleware via AddDispatch(builder => builder.UseMiddleware<T>()) or enable pipeline synthesis.")]
	private static partial void LogEmptyPipeline(ILogger logger);

	[LoggerMessage(2603, LogLevel.Debug,
		"Keyed service '{ServiceName}' resolved to '{ImplementationType}' via \"default\" key.")]
	private static partial void LogKeyedServiceResolved(ILogger logger, string serviceName, string implementationType);

	[LoggerMessage(2605, LogLevel.Information,
		"No IMeterFactory registered. Dispatch metrics and tracing are disabled. " +
		"Add AddDispatchObservability() or register IMeterFactory for production monitoring.")]
	private static partial void LogNoObservability(ILogger logger);
}

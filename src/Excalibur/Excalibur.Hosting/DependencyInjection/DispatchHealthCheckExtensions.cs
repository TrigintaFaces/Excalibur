// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Hosting.Options;
using Excalibur.Saga.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Convenience extension methods for registering all available Dispatch health checks in a single call.
/// </summary>
/// <remarks>
/// <para>
/// This extension conditionally registers health checks based on which services are available
/// in the DI container. Only health checks whose prerequisite services are registered will be added.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// services.AddHealthChecks()
///     .AddDispatchHealthChecks();
///
/// // Or with options to exclude specific checks:
/// services.AddHealthChecks()
///     .AddDispatchHealthChecks(options =&gt;
///     {
///         options.IncludeLeaderElection = false;
///     });
/// </code>
/// </para>
/// </remarks>
public static class DispatchHealthCheckExtensions
{
	/// <summary>
	/// Adds all available Dispatch health checks (outbox, inbox, saga, leader election)
	/// based on which services are registered in the DI container.
	/// </summary>
	/// <param name="builder">The health checks builder.</param>
	/// <param name="configure">Optional action to configure which health checks to include.</param>
	/// <returns>The health checks builder for chaining.</returns>
	public static IHealthChecksBuilder AddDispatchHealthChecks(
		this IHealthChecksBuilder builder,
		Action<DispatchHealthCheckOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		var options = new DispatchHealthCheckOptions();
		configure?.Invoke(options);

		var services = builder.Services;

		if (options.IncludeOutbox && HasService<IOutboxPublisher>(services))
		{
			builder.AddOutboxHealthCheck();
		}

		if (options.IncludeInbox && HasService<IInboxStore>(services))
		{
			builder.AddInboxHealthCheck();
		}

		if (options.IncludeSaga && HasService<ISagaMonitoringService>(services))
		{
			builder.AddSagaHealthCheck();
		}

		if (options.IncludeLeaderElection && HasService<ILeaderElection>(services))
		{
			builder.AddLeaderElectionHealthCheck();
		}

		return builder;
	}

	private static bool HasService<T>(IServiceCollection services)
	{
		foreach (var descriptor in services)
		{
			if (descriptor.ServiceType == typeof(T))
			{
				return true;
			}
		}

		return false;
	}
}

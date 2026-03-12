// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Consul;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.Consul;
using Excalibur.LeaderElection.Diagnostics;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Consul leader election on <see cref="ILeaderElectionBuilder"/>.
/// </summary>
public static class ConsulLeaderElectionBuilderExtensions
{
	/// <summary>
	/// Configures the leader election builder to use the Consul provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configureOptions">Optional action to configure Consul-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ILeaderElectionBuilder UseConsul(
		this ILeaderElectionBuilder builder,
		Action<ConsulLeaderElectionOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Configure options with validation
		var optionsBuilder = builder.Services.AddOptions<ConsulLeaderElectionOptions>();
		if (configureOptions != null)
		{
			optionsBuilder.Configure(configureOptions);
		}

		optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register cross-property validators
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ConsulLeaderElectionOptions>, ConsulLeaderElectionOptionsValidator>());

		// Register Consul client if not already registered
		builder.Services.TryAddSingleton<IConsulClient>(provider =>
		{
			var options = provider.GetRequiredService<IOptions<ConsulLeaderElectionOptions>>();
			return new ConsulClient(config =>
			{
				if (!string.IsNullOrEmpty(options.Value.ConsulAddress))
				{
					config.Address = new Uri(options.Value.ConsulAddress);
				}

				if (!string.IsNullOrEmpty(options.Value.Datacenter))
				{
					config.Datacenter = options.Value.Datacenter;
				}

				if (!string.IsNullOrEmpty(options.Value.Token))
				{
					config.Token = options.Value.Token;
				}
			});
		});

		// Register the factory with telemetry wrapping
		_ = builder.Services.AddSingleton<ILeaderElectionFactory>(sp =>
		{
			var inner = ActivatorUtilities.CreateInstance<ConsulLeaderElectionFactory>(sp);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "Consul");
		});

		return builder;
	}
}

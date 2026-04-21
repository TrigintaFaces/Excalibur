// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Consul;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Consul;
using Excalibur.LeaderElection.Diagnostics;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Consul-based leader election directly on <see cref="IServiceCollection"/>.
/// </summary>
public static class ConsulLeaderElectionExtensions
{
	/// <summary>
	/// Adds Consul-based leader election services to the service collection using a fluent builder.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the Consul builder.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddConsulLeaderElection(
		this IServiceCollection services,
		Action<ILeaderElectionConsulBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var consulBuilder = new LeaderElectionConsulBuilder();
		configure(consulBuilder);

		RegisterOptionsAndServices(services, consulBuilder);

		return services;
	}

	/// <summary>
	/// Adds a singleton leader election instance for a specific resource.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="resourceName">The resource name to elect a leader for.</param>
	/// <param name="candidateId">Optional candidate ID.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddConsulLeaderElectionForResource(
		this IServiceCollection services,
		string resourceName,
		string? candidateId = null)
	{
		_ = services.AddSingleton(provider =>
		{
			var factory = provider.GetRequiredService<ILeaderElectionFactory>();
			return factory.CreateElection(resourceName, candidateId);
		});

		return services;
	}

	/// <summary>
	/// Adds a singleton health-based leader election instance for a specific resource.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="resourceName">The resource name to elect a leader for.</param>
	/// <param name="candidateId">Optional candidate ID.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddConsulHealthBasedLeaderElectionForResource(
		this IServiceCollection services,
		string resourceName,
		string? candidateId = null)
	{
		_ = services.AddSingleton(provider =>
		{
			var factory = provider.GetRequiredService<ILeaderElectionFactory>();
			return factory.CreateHealthBasedElection(resourceName, candidateId);
		});

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		LeaderElectionConsulBuilder consulBuilder)
	{
		// Configure options from builder state
		var optionsBuilder = services.AddOptions<ConsulLeaderElectionOptions>();
		optionsBuilder.Configure(opt =>
		{
			if (consulBuilder.AddressValue is not null)
			{
				opt.ConsulAddress = consulBuilder.AddressValue;
			}

			if (consulBuilder.TokenValue is not null)
			{
				opt.Token = consulBuilder.TokenValue;
			}

			if (consulBuilder.DatacenterValue is not null)
			{
				opt.Datacenter = consulBuilder.DatacenterValue;
			}

			if (consulBuilder.SessionTtlValue.HasValue)
			{
				opt.SessionTTL = consulBuilder.SessionTtlValue.Value;
			}

			if (consulBuilder.LockKeyValue is not null)
			{
				opt.KeyPrefix = consulBuilder.LockKeyValue;
			}
		});

		// Register BindConfiguration if set
		if (consulBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<ConsulLeaderElectionOptions>()
				.BindConfiguration(consulBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		optionsBuilder.ValidateOnStart();

		// Register cross-property validators
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ConsulLeaderElectionOptions>, ConsulLeaderElectionOptionsValidator>());

		// Register Consul client if not already registered
		services.TryAddSingleton<IConsulClient>(provider =>
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
		_ = services.AddSingleton<ILeaderElectionFactory>(sp =>
		{
			var inner = ActivatorUtilities.CreateInstance<ConsulLeaderElectionFactory>(sp);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "Consul");
		});
	}
}

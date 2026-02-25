// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Consul;

using Excalibur.Dispatch.LeaderElection;

using Excalibur.LeaderElection.Consul;
using Excalibur.LeaderElection.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Consul-based leader election.
/// </summary>
public static class ConsulLeaderElectionExtensions
{
	/// <summary>
	/// Adds Consul-based leader election services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional action to configure options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddConsulLeaderElection(
		this IServiceCollection services,
		Action<ConsulLeaderElectionOptions>? configureOptions = null)
	{
		// Configure options with validation
		var optionsBuilder = services.AddOptions<ConsulLeaderElectionOptions>();
		if (configureOptions != null)
		{
			optionsBuilder.Configure(configureOptions);
		}

		optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

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

		return services;
	}

	/// <summary>
	/// Adds Consul-based leader election services with configuration from IConfiguration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section containing Consul options. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require types that cannot be statically analyzed. Consider using source generation.")]
	[RequiresDynamicCode("Configuration binding may require dynamic code generation which is not compatible with AOT compilation.")]
	public static IServiceCollection AddConsulLeaderElection(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		_ = services.AddOptions<ConsulLeaderElectionOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		return services.AddConsulLeaderElection();
	}

	/// <summary>
	/// Adds a singleton leader election instance for a specific resource.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="resourceName"> The resource name to elect a leader for. </param>
	/// <param name="candidateId"> Optional candidate ID. </param>
	/// <returns> The service collection for chaining. </returns>
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
	/// <param name="services"> The service collection. </param>
	/// <param name="resourceName"> The resource name to elect a leader for. </param>
	/// <param name="candidateId"> Optional candidate ID. </param>
	/// <returns> The service collection for chaining. </returns>
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

}

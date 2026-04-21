// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
	/// Configures the leader election builder to use the Consul provider via a fluent builder.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configure">Configuration action for the Consul builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddLeaderElection(le =&gt;
	/// {
	///     le.UseConsul(consul =&gt;
	///     {
	///         consul.Address("http://consul:8500")
	///               .Token("my-acl-token")
	///               .LockKey("my-app/leader");
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static ILeaderElectionBuilder UseConsul(
		this ILeaderElectionBuilder builder,
		Action<ILeaderElectionConsulBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var consulBuilder = new LeaderElectionConsulBuilder();
		configure(consulBuilder);

		RegisterOptionsAndServices(builder, consulBuilder);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		ILeaderElectionBuilder builder,
		LeaderElectionConsulBuilder consulBuilder)
	{
		// Configure options from builder state
		_ = builder.Services.Configure<ConsulLeaderElectionOptions>(opt =>
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
			builder.Services.AddOptions<ConsulLeaderElectionOptions>()
				.BindConfiguration(consulBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		builder.Services.AddOptions<ConsulLeaderElectionOptions>().ValidateOnStart();

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
	}
}

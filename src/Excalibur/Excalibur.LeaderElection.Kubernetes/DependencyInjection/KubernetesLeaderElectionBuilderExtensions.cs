// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Kubernetes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using k8s;

using K8sClient = k8s.Kubernetes;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Kubernetes leader election on <see cref="ILeaderElectionBuilder"/>.
/// </summary>
public static class KubernetesLeaderElectionBuilderExtensions
{
	/// <summary>
	/// Configures the leader election builder to use the Kubernetes provider.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configure">Optional action to configure Kubernetes-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ILeaderElectionBuilder UseKubernetes(
		this ILeaderElectionBuilder builder,
		Action<KubernetesLeaderElectionOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Configure options
		var optionsBuilder = builder.Services.AddOptions<KubernetesLeaderElectionOptions>();
		if (configure != null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder.ValidateOnStart();

		return builder.UseKubernetesCore();
	}

	/// <summary>
	/// Configures the leader election builder to use the Kubernetes provider with an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="KubernetesLeaderElectionOptions"/>.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ILeaderElectionBuilder UseKubernetes(
		this ILeaderElectionBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddOptions<KubernetesLeaderElectionOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		return builder.UseKubernetesCore();
	}

	private static ILeaderElectionBuilder UseKubernetesCore(this ILeaderElectionBuilder builder)
	{
		// Register cross-property validators
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<KubernetesLeaderElectionOptions>,
				KubernetesLeaderElectionOptionsValidator>());

		// Register Kubernetes client if not already registered
		_ = builder.Services.AddSingleton<IKubernetes>(static _ =>
		{
			if (IsRunningInKubernetes())
			{
				var config = KubernetesClientConfiguration.InClusterConfig();
				return new K8sClient(config);
			}
			else
			{
				var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
				return new K8sClient(config);
			}
		});

		// Register the factory with telemetry wrapping
		_ = builder.Services.AddSingleton<ILeaderElectionFactory>(sp =>
		{
			var inner = ActivatorUtilities.CreateInstance<KubernetesLeaderElectionFactory>(sp);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "Kubernetes");
		});

		return builder;
	}

	/// <summary>
	/// Determines if the application is running inside a Kubernetes cluster.
	/// </summary>
	private static bool IsRunningInKubernetes()
	{
		const string tokenPath = "/var/run/secrets/kubernetes.io/serviceaccount/token";
		if (File.Exists(tokenPath))
		{
			return true;
		}

		var kubernetesServiceHost = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
		var kubernetesServicePort = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT");

		return !string.IsNullOrEmpty(kubernetesServiceHost) && !string.IsNullOrEmpty(kubernetesServicePort);
	}
}

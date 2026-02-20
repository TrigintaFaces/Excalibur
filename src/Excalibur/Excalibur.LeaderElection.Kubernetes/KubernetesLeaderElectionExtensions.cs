// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Kubernetes;

using k8s;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using K8sClient = k8s.Kubernetes;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Kubernetes leader election.
/// </summary>
public static class KubernetesLeaderElectionExtensions
{
	/// <summary>
	/// Adds Kubernetes leader election support.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Action to configure the Kubernetes leader election options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddExcaliburKubernetesLeaderElection(
		this IServiceCollection services,
		Action<KubernetesLeaderElectionOptions>? configure = null)
	{
		// Configure options
		var optionsBuilder = services.AddOptions<KubernetesLeaderElectionOptions>();
		if (configure != null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register cross-property validators
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<KubernetesLeaderElectionOptions>,
				KubernetesLeaderElectionOptionsValidator>());

		// Register Kubernetes client if not already registered
		_ = services.AddSingleton<IKubernetes>(static _ =>
		{
			// Try to detect if we're running in a cluster
			if (IsRunningInKubernetes())
			{
				var config = KubernetesClientConfiguration.InClusterConfig();
				return new K8sClient(config);
			}
			else
			{
				// Load from default kubeconfig for local development
				var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
				return new K8sClient(config);
			}
		});

		// Register the factory with telemetry wrapping
		_ = services.AddSingleton<ILeaderElectionFactory>(sp =>
		{
			var inner = ActivatorUtilities.CreateInstance<KubernetesLeaderElectionFactory>(sp);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "Kubernetes");
		});

		// Register a hosted service for automatic leader election if needed
		_ = services.AddSingleton<KubernetesLeaderElectionHostedService>();

		return services;
	}

	/// <summary>
	/// Adds Kubernetes leader election with automatic management via hosted service.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="resourceName"> The resource name for leader election. </param>
	/// <param name="configure"> Action to configure the Kubernetes leader election options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddExcaliburKubernetesLeaderElectionHostedService(
		this IServiceCollection services,
		string resourceName,
		Action<KubernetesLeaderElectionOptions>? configure = null)
	{
		// Add base Kubernetes leader election
		_ = services.AddExcaliburKubernetesLeaderElection(configure);

		// Register the hosted service with specific resource
		_ = services.AddHostedService(sp =>
		{
			var factory = sp.GetRequiredService<ILeaderElectionFactory>();
			var options = sp.GetRequiredService<IOptions<KubernetesLeaderElectionOptions>>();
			var logger = sp.GetService<ILogger<KubernetesLeaderElectionHostedService>>();

			return new KubernetesLeaderElectionHostedService(
				factory,
				resourceName,
				options,
				logger);
		});

		return services;
	}

	/// <summary>
	/// Configures Kubernetes client for leader election.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureClient"> Action to configure the Kubernetes client. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection ConfigureExcaliburKubernetesClient(
		this IServiceCollection services,
		Action<KubernetesClientConfiguration> configureClient)
	{
		_ = services.AddSingleton<IKubernetes>(_ =>
		{
			var config = IsRunningInKubernetes()
				? KubernetesClientConfiguration.InClusterConfig()
				: KubernetesClientConfiguration.BuildConfigFromConfigFile();

			configureClient(config);
			return new K8sClient(config);
		});

		return services;
	}

	/// <summary>
	/// Determines if the application is running inside a Kubernetes cluster.
	/// </summary>
	/// <returns> True if running in Kubernetes, false otherwise. </returns>
	private static bool IsRunningInKubernetes()
	{
		// Check for Kubernetes service account token
		const string tokenPath = "/var/run/secrets/kubernetes.io/serviceaccount/token";
		if (File.Exists(tokenPath))
		{
			return true;
		}

		// Check for Kubernetes environment variables
		var kubernetesServiceHost = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
		var kubernetesServicePort = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT");

		return !string.IsNullOrEmpty(kubernetesServiceHost) && !string.IsNullOrEmpty(kubernetesServicePort);
	}
}

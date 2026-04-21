// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
/// Extension methods for configuring Kubernetes leader election directly on <see cref="IServiceCollection"/>.
/// </summary>
public static class KubernetesLeaderElectionExtensions
{
	/// <summary>
	/// Adds Kubernetes leader election support using a fluent builder.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the Kubernetes builder.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddExcaliburKubernetesLeaderElection(
		this IServiceCollection services,
		Action<ILeaderElectionKubernetesBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var k8sBuilder = new LeaderElectionKubernetesBuilder();
		configure(k8sBuilder);

		RegisterOptionsAndServices(services, k8sBuilder);

		return services;
	}

	/// <summary>
	/// Adds Kubernetes leader election with automatic management via hosted service.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="resourceName">The resource name for leader election.</param>
	/// <param name="configure">Configuration action for the Kubernetes builder.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddExcaliburKubernetesLeaderElectionHostedService(
		this IServiceCollection services,
		string resourceName,
		Action<ILeaderElectionKubernetesBuilder>? configure = null)
	{
		// Add base Kubernetes leader election
		_ = services.AddExcaliburKubernetesLeaderElection(configure ?? (_ => { }));

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
	/// <param name="services">The service collection.</param>
	/// <param name="configureClient">Action to configure the Kubernetes client.</param>
	/// <returns>The service collection for chaining.</returns>
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

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		LeaderElectionKubernetesBuilder k8sBuilder)
	{
		// Configure options from builder state
		var optionsBuilder = services.AddOptions<KubernetesLeaderElectionOptions>();
		optionsBuilder.Configure(opt =>
		{
			if (k8sBuilder.NamespaceValue is not null)
			{
				opt.Namespace = k8sBuilder.NamespaceValue;
			}

			if (k8sBuilder.LeaseNameValue is not null)
			{
				opt.LeaseName = k8sBuilder.LeaseNameValue;
			}

			if (k8sBuilder.LeaseIdentityValue is not null)
			{
				opt.CandidateId = k8sBuilder.LeaseIdentityValue;
			}

			if (k8sBuilder.LeaseDurationSeconds.HasValue)
			{
				opt.LeaseDurationSeconds = k8sBuilder.LeaseDurationSeconds.Value;
			}

			if (k8sBuilder.RenewDeadlineMilliseconds.HasValue)
			{
				opt.RenewIntervalMilliseconds = k8sBuilder.RenewDeadlineMilliseconds.Value;
			}

			if (k8sBuilder.RetryPeriodMilliseconds.HasValue)
			{
				opt.RetryIntervalMilliseconds = k8sBuilder.RetryPeriodMilliseconds.Value;
			}
		});

		// Register BindConfiguration if set
		if (k8sBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<KubernetesLeaderElectionOptions>()
				.BindConfiguration(k8sBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		_ = optionsBuilder.ValidateOnStart();

		// Register cross-property validators
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<KubernetesLeaderElectionOptions>,
				KubernetesLeaderElectionOptionsValidator>());

		// Register Kubernetes client
		if (k8sBuilder.UseInCluster)
		{
			_ = services.AddSingleton<IKubernetes>(static _ =>
			{
				var config = KubernetesClientConfiguration.InClusterConfig();
				return new K8sClient(config);
			});
		}
		else
		{
			// Auto-detect: try in-cluster first, fall back to kubeconfig
			_ = services.AddSingleton<IKubernetes>(static _ =>
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
		}

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

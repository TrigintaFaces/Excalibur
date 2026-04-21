// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.DependencyInjection;
using Excalibur.LeaderElection.Diagnostics;
using Excalibur.LeaderElection.Kubernetes;

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
	/// Configures the leader election builder to use the Kubernetes provider via a fluent builder.
	/// </summary>
	/// <param name="builder">The leader election builder.</param>
	/// <param name="configure">Configuration action for the Kubernetes builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddLeaderElection(le =&gt;
	/// {
	///     le.UseKubernetes(k8s =&gt;
	///     {
	///         k8s.Namespace("my-namespace")
	///            .LeaseName("my-app-leader")
	///            .LeaseDuration(15);
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static ILeaderElectionBuilder UseKubernetes(
		this ILeaderElectionBuilder builder,
		Action<ILeaderElectionKubernetesBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var k8sBuilder = new LeaderElectionKubernetesBuilder();
		configure(k8sBuilder);

		RegisterOptionsAndServices(builder, k8sBuilder);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		ILeaderElectionBuilder builder,
		LeaderElectionKubernetesBuilder k8sBuilder)
	{
		// Configure options from builder state
		_ = builder.Services.Configure<KubernetesLeaderElectionOptions>(opt =>
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
			builder.Services.AddOptions<KubernetesLeaderElectionOptions>()
				.BindConfiguration(k8sBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		builder.Services.AddOptions<KubernetesLeaderElectionOptions>().ValidateOnStart();

		// Register cross-property validators
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<KubernetesLeaderElectionOptions>,
				KubernetesLeaderElectionOptionsValidator>());

		// Register Kubernetes client
		if (k8sBuilder.UseInCluster)
		{
			_ = builder.Services.AddSingleton<IKubernetes>(static _ =>
			{
				var config = KubernetesClientConfiguration.InClusterConfig();
				return new K8sClient(config);
			});
		}
		else
		{
			// Auto-detect: try in-cluster first, fall back to kubeconfig
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
		}

		// Register the factory with telemetry wrapping
		_ = builder.Services.AddSingleton<ILeaderElectionFactory>(sp =>
		{
			var inner = ActivatorUtilities.CreateInstance<KubernetesLeaderElectionFactory>(sp);
			var meterFactory = sp.GetService<IMeterFactory>();
			var meter = meterFactory?.Create(LeaderElectionTelemetryConstants.MeterName) ?? new Meter(LeaderElectionTelemetryConstants.MeterName);
			var activitySource = new ActivitySource(LeaderElectionTelemetryConstants.ActivitySourceName);
			return new TelemetryLeaderElectionFactory(inner, meter, activitySource, "Kubernetes");
		});
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

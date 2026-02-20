// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Amazon.Extensions.NETCore.Setup;
using Amazon.Scheduler;

using Excalibur.Jobs.CloudProviders.Aws;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS EventBridge Scheduler services.
/// </summary>
public static class AwsServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS EventBridge Scheduler support for Excalibur jobs.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Action to configure AWS scheduler options. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static IServiceCollection AddAwsScheduler(
		this IServiceCollection services,
		Action<AwsSchedulerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Configure options
		_ = services.Configure(configure);

		// Add AWS EventBridge Scheduler client
		_ = services.AddAWSService<IAmazonScheduler>();
		_ = services.AddSingleton(static provider =>
			(AmazonSchedulerClient)provider.GetRequiredService<IAmazonScheduler>());

		// Add the job provider
		_ = services.AddSingleton<AwsSchedulerJobProvider>();

		return services;
	}

	/// <summary>
	/// Adds AWS EventBridge Scheduler support with AWS configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="awsOptions"> AWS configuration options. </param>
	/// <param name="configure"> Action to configure AWS scheduler options. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public static IServiceCollection AddAwsScheduler(
		this IServiceCollection services,
		AWSOptions awsOptions,
		Action<AwsSchedulerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(awsOptions);
		ArgumentNullException.ThrowIfNull(configure);

		// Configure options
		_ = services.Configure(configure);

		// Add AWS EventBridge Scheduler client with options
		_ = services.AddAWSService<IAmazonScheduler>(awsOptions);
		_ = services.AddSingleton(static provider =>
			(AmazonSchedulerClient)provider.GetRequiredService<IAmazonScheduler>());

		// Add the job provider
		_ = services.AddSingleton<AwsSchedulerJobProvider>();

		return services;
	}
}

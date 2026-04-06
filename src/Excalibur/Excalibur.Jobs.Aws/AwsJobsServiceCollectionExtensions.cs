// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Amazon.Extensions.NETCore.Setup;
using Amazon.Scheduler;

using Excalibur.Jobs.Aws;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS EventBridge Scheduler services.
/// </summary>
public static class AwsJobsServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS EventBridge Scheduler support for Excalibur jobs.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Action to configure AWS scheduler options. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAwsScheduler(
		this IServiceCollection services,
		Action<AwsSchedulerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Configure options
		_ = services.AddOptions<AwsSchedulerOptions>()
			.Configure(configure)
			.ValidateOnStart();

		// Add AWS EventBridge Scheduler client
		_ = services.AddAWSService<IAmazonScheduler>();
		_ = services.AddSingleton(static provider =>
			(AmazonSchedulerClient)provider.GetRequiredService<IAmazonScheduler>());

		// Add the job provider
		_ = services.AddSingleton<AwsSchedulerJobProvider>();

		return services;
	}

	/// <summary>
	/// Adds AWS EventBridge Scheduler support for Excalibur jobs
	/// using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section to bind options from. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("Binding configuration values requires dynamic code generation")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAwsScheduler(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Configure options
		_ = services.AddOptions<AwsSchedulerOptions>()
			.Bind(configuration)
			.ValidateOnStart();

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
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAwsScheduler(
		this IServiceCollection services,
		AWSOptions awsOptions,
		Action<AwsSchedulerOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(awsOptions);
		ArgumentNullException.ThrowIfNull(configure);

		// Configure options
		_ = services.AddOptions<AwsSchedulerOptions>()
			.Configure(configure)
			.ValidateOnStart();

		// Add AWS EventBridge Scheduler client with options
		_ = services.AddAWSService<IAmazonScheduler>(awsOptions);
		_ = services.AddSingleton(static provider =>
			(AmazonSchedulerClient)provider.GetRequiredService<IAmazonScheduler>());

		// Add the job provider
		_ = services.AddSingleton<AwsSchedulerJobProvider>();

		return services;
	}

	/// <summary>
	/// Adds AWS EventBridge Scheduler support with AWS configuration
	/// using an <see cref="IConfiguration"/> section for scheduler options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="awsOptions"> AWS configuration options. </param>
	/// <param name="configuration"> The configuration section to bind scheduler options from. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("Binding configuration values requires dynamic code generation")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAwsScheduler(
		this IServiceCollection services,
		AWSOptions awsOptions,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(awsOptions);
		ArgumentNullException.ThrowIfNull(configuration);

		// Configure options
		_ = services.AddOptions<AwsSchedulerOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		// Add AWS EventBridge Scheduler client with options
		_ = services.AddAWSService<IAmazonScheduler>(awsOptions);
		_ = services.AddSingleton(static provider =>
			(AmazonSchedulerClient)provider.GetRequiredService<IAmazonScheduler>());

		// Add the job provider
		_ = services.AddSingleton<AwsSchedulerJobProvider>();

		return services;
	}
}

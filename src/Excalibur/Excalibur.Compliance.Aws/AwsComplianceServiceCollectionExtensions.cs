// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Amazon.KeyManagementService;

using Excalibur.Compliance;
using Excalibur.Compliance.Aws;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering AWS KMS compliance services with dependency injection.
/// </summary>
public static class AwsComplianceServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS KMS key management provider to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the AWS KMS compliance builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddAwsKmsKeyManagement(aws =&gt;
	/// {
	///     aws.Region("us-east-1")
	///        .UseFipsEndpoint()
	///        .Environment("prod");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddAwsKmsKeyManagement(
		this IServiceCollection services,
		Action<IComplianceAwsBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new AwsKmsOptions();
		var builder = new ComplianceAwsBuilder(options);
		configure(builder);

		RegisterOptionsAndServices(services, builder, options);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		ComplianceAwsBuilder builder,
		AwsKmsOptions options)
	{
		_ = services.Configure<AwsKmsOptions>(opt =>
		{
			opt.Region = options.Region;
			opt.UseFipsEndpoint = options.UseFipsEndpoint;
			opt.KeyAliasPrefix = options.KeyAliasPrefix;
			opt.Environment = options.Environment;
			opt.ServiceUrl = options.ServiceUrl;
		});

		if (builder.BindConfigurationPath is not null)
		{
			_ = services.AddOptions<AwsKmsOptions>()
				.BindConfiguration(builder.BindConfigurationPath)
				.ValidateOnStart();
		}

		_ = services.AddOptions<AwsKmsOptions>().ValidateOnStart();

		RegisterAwsKmsCore(services);
	}

	private static void RegisterAwsKmsCore(IServiceCollection services)
	{
		// Register validator
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AwsKmsOptions>, AwsKmsOptionsValidator>());

		// Register AWS KMS client
		services.TryAddSingleton<IAmazonKeyManagementService>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<AwsKmsOptions>>().Value;
			var config = new AmazonKeyManagementServiceConfig();

			if (options.Region is not null)
			{
				config.RegionEndpoint = options.Region;
			}

			if (options.UseFipsEndpoint)
			{
				config.UseFIPSEndpoint = true;
			}

			if (!string.IsNullOrEmpty(options.ServiceUrl))
			{
				config.ServiceURL = options.ServiceUrl;
			}

			return new AmazonKeyManagementServiceClient(config);
		});

		// Register the provider
		services.TryAddSingleton<AwsKmsProvider>();
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<AwsKmsProvider>());
		services.TryAddSingleton<IKeyManagementAdmin>(sp => sp.GetRequiredService<AwsKmsProvider>());
	}
}

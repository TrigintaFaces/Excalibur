// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.KeyManagementService;

using Excalibur.Dispatch.Compliance;
using Excalibur.Dispatch.Compliance.Aws;

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
	/// <param name="configure">Optional configuration action for AWS KMS options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers:
	/// <list type="bullet">
	/// <item><see cref="IAmazonKeyManagementService"/> - AWS KMS client</item>
	/// <item><see cref="IKeyManagementProvider"/> - Key management via AWS KMS</item>
	/// <item><see cref="AwsKmsProvider"/> - Concrete implementation (for direct access if needed)</item>
	/// </list>
	/// </para>
	/// <para>
	/// Example usage:
	/// <code>
	/// services.AddAwsKmsKeyManagement(options =>
	/// {
	///     options.Region = RegionEndpoint.USEast1;
	///     options.UseFipsEndpoint = true; // For FIPS compliance
	///     options.Environment = "prod";
	///     options.EnableAutoRotation = true;
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddAwsKmsKeyManagement(
		this IServiceCollection services,
		Action<AwsKmsOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options
		if (configure is not null)
		{
			_ = services.Configure(configure);
		}

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

		return services;
	}

	/// <summary>
	/// Adds AWS KMS key management provider with a custom client factory.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="clientFactory">Factory function to create the KMS client.</param>
	/// <param name="configure">Optional configuration action for AWS KMS options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Use this overload when you need custom client configuration, such as:
	/// <list type="bullet">
	/// <item>Using assumed role credentials</item>
	/// <item>Using web identity federation</item>
	/// <item>Custom HTTP client configuration</item>
	/// </list>
	/// </remarks>
	public static IServiceCollection AddAwsKmsKeyManagement(
		this IServiceCollection services,
		Func<IServiceProvider, IAmazonKeyManagementService> clientFactory,
		Action<AwsKmsOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(clientFactory);

		// Configure options
		if (configure is not null)
		{
			_ = services.Configure(configure);
		}

		// Register custom client factory
		services.TryAddSingleton(clientFactory);

		// Register the provider
		services.TryAddSingleton<AwsKmsProvider>();
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<AwsKmsProvider>());

		return services;
	}

	/// <summary>
	/// Adds AWS KMS key management provider configured for LocalStack testing.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="localStackEndpoint">The LocalStack endpoint URL (default: http://localhost:4566).</param>
	/// <param name="configure">Optional additional configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// This method is intended for local development and testing with LocalStack.
	/// Do not use in production.
	/// </remarks>
	public static IServiceCollection AddAwsKmsKeyManagementLocalStack(
		this IServiceCollection services,
		string localStackEndpoint = "http://localhost:4566",
		Action<AwsKmsOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.Configure<AwsKmsOptions>(options =>
		{
			options.ServiceUrl = localStackEndpoint;
			options.Region = RegionEndpoint.USEast1;
			configure?.Invoke(options);
		});

		// Register LocalStack-configured client
		services.TryAddSingleton<IAmazonKeyManagementService>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<AwsKmsOptions>>().Value;
			var config = new AmazonKeyManagementServiceConfig
			{
				ServiceURL = options.ServiceUrl,
				RegionEndpoint = options.Region ?? RegionEndpoint.USEast1,
				UseHttp = options.ServiceUrl?.StartsWith("http://", StringComparison.Ordinal) ?? false
			};

			// LocalStack requires dummy credentials
			return new AmazonKeyManagementServiceClient(
				"test",
				"test",
				config);
		});

		// Register the provider
		services.TryAddSingleton<AwsKmsProvider>();
		services.TryAddSingleton<IKeyManagementProvider>(sp => sp.GetRequiredService<AwsKmsProvider>());

		return services;
	}

	/// <summary>
	/// Adds AWS KMS key management with multi-region support for disaster recovery.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="primaryRegion">The primary AWS region.</param>
	/// <param name="replicaRegions">The replica regions for multi-region keys.</param>
	/// <param name="configure">Optional additional configuration.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Multi-region keys (MRKs) allow seamless failover between regions.
	/// The same key ID can be used to encrypt/decrypt in any region where the key is replicated.
	/// </remarks>
	public static IServiceCollection AddAwsKmsKeyManagementMultiRegion(
		this IServiceCollection services,
		RegionEndpoint primaryRegion,
		IEnumerable<RegionEndpoint> replicaRegions,
		Action<AwsKmsOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(primaryRegion);
		ArgumentNullException.ThrowIfNull(replicaRegions);

		return services.AddAwsKmsKeyManagement(options =>
		{
			options.Region = primaryRegion;
			options.CreateMultiRegionKeys = true;
			options.ReplicaRegions = [.. replicaRegions];
			configure?.Invoke(options);
		});
	}
}

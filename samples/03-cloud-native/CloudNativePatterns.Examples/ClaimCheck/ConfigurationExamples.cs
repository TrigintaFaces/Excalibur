// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.CloudNativePatterns.Examples.ClaimCheck;

/// <summary>
/// Examples showing various configuration options for the Claim Check pattern.
/// </summary>
public static class ConfigurationExamples
{
	private static readonly string[] Tags = ["storage", "claim-check"];

	/// <summary>
	/// Example 1: Basic configuration with minimal settings.
	/// </summary>
	public static IHostBuilder ConfigureBasic(this IHostBuilder hostBuilder) =>
		hostBuilder.ConfigureServices(static services => services.AddClaimCheck<AzureBlobClaimCheckProvider>(static options =>
		{
			options.ConnectionString = "UseDevelopmentStorage=true";
			options.ContainerName = "claim-checks";
		}));

	/// <summary>
	/// Example 2: Production configuration with all features enabled.
	/// </summary>
	public static IHostBuilder ConfigureProduction(this IHostBuilder hostBuilder)
	{
		ArgumentNullException.ThrowIfNull(hostBuilder);

		return hostBuilder.ConfigureServices((context, services) =>
		{
			var configuration = context.Configuration;

			_ = services.AddClaimCheck<AzureBlobClaimCheckProvider>(options =>
			{
				// Connection settings from configuration
				options.ConnectionString = configuration.GetConnectionString("AzureStorage");
				options.ContainerName = configuration["ClaimCheck:ContainerName"] ?? "claim-checks";

				// Performance settings
				options.PayloadThreshold = 64 * 1024; // 64KB
				options.Storage.ChunkSize = 1024 * 1024; // 1MB chunks
				options.Storage.MaxConcurrency = Environment.ProcessorCount;
				options.Storage.BufferPoolSize = 100;

				// Compression settings
				options.EnableCompression = true;
				options.CompressionThreshold = 1024; // 1KB minimum
				options.MinCompressionRatio = 0.8; // 20% minimum reduction
				options.CompressionLevel = System.IO.Compression.CompressionLevel.Optimal;

				// Cleanup settings
				options.EnableCleanup = true;
				options.CleanupInterval = TimeSpan.FromMinutes(15);
				options.RetentionPeriod = TimeSpan.FromDays(7);
				options.Cleanup.CleanupBatchSize = 1000;

				// Timeout and retry
				options.Storage.OperationTimeout = TimeSpan.FromMinutes(5);
				options.Storage.MaxRetries = 3;
				options.Storage.RetryDelay = TimeSpan.FromSeconds(1);

				// Security
				options.ValidateChecksum = true;
				options.Storage.EnableEncryption = false; // Use Azure Storage encryption
			});
		});
	}

	/// <summary>
	/// Example 3: High-performance configuration for large file handling.
	/// </summary>
	public static IHostBuilder ConfigureHighPerformance(this IHostBuilder hostBuilder) =>
		hostBuilder.ConfigureServices(static services => services.AddClaimCheck<AzureBlobClaimCheckProvider>(static options =>
		{
			options.ConnectionString = "UseDevelopmentStorage=true";
			options.ContainerName = "large-files";

			// Optimized for large files
			options.PayloadThreshold = 1024 * 1024; // 1MB threshold
			options.Storage.ChunkSize = 4 * 1024 * 1024; // 4MB chunks
			options.Storage.MaxConcurrency = Environment.ProcessorCount * 2;
			options.Storage.BufferPoolSize = 50;

			// Disable compression for already-compressed files
			options.EnableCompression = false;

			// Longer timeouts for large files
			options.Storage.OperationTimeout = TimeSpan.FromMinutes(30);

			// Use more aggressive cleanup
			options.EnableCleanup = true;
			options.CleanupInterval = TimeSpan.FromMinutes(5);
			options.RetentionPeriod = TimeSpan.FromHours(1);
		}));

	/// <summary>
	/// Example 4: Configuration with custom naming strategy.
	/// </summary>
	public static IHostBuilder ConfigureWithCustomNaming(this IHostBuilder hostBuilder) =>
		hostBuilder.ConfigureServices(static services =>
		{
			// Register custom naming strategy
			_ = services.AddSingleton<IClaimCheckNamingStrategy, CustomNamingStrategy>();

			_ = services.AddClaimCheck<AzureBlobClaimCheckProvider>(static options =>
			{
				options.ConnectionString = "UseDevelopmentStorage=true";
				options.ContainerName = "custom-claims";
				options.BlobNamePrefix = "claims";
			});
		});

	/// <summary>
	/// Example 5: Configuration from appsettings.json.
	/// </summary>
	public static IHostBuilder ConfigureFromAppSettings(this IHostBuilder hostBuilder)
	{
		ArgumentNullException.ThrowIfNull(hostBuilder);

		return hostBuilder
			.ConfigureAppConfiguration((context, config) =>
			{
				_ = config.AddJsonFile("appsettings.json", optional: false);
				_ = config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
			})
			.ConfigureServices((context, services) =>
				// Bind configuration section to options
				services.AddClaimCheck<AzureBlobClaimCheckProvider>(options =>
					context.Configuration.GetSection("ClaimCheck").Bind(options)));
	}

	/// <summary>
	/// Example 6: Multi-tenant configuration.
	/// </summary>
	public static IHostBuilder ConfigureMultiTenant(this IHostBuilder hostBuilder) =>
		hostBuilder.ConfigureServices(static services =>
		{
			// Register tenant-specific providers
			_ = services.AddKeyedSingleton<IClaimCheckProvider>("TenantA", static (sp, key) =>
			{
				var options = new ClaimCheckOptions { ConnectionString = "UseDevelopmentStorage=true", ContainerName = "tenant-a-claims" };

				return new AzureBlobClaimCheckProvider(
					Microsoft.Extensions.Options.Options.Create(options),
					sp.GetRequiredService<ILogger<AzureBlobClaimCheckProvider>>());
			});

			_ = services.AddKeyedSingleton<IClaimCheckProvider>("TenantB", static (sp, key) =>
			{
				var options = new ClaimCheckOptions { ConnectionString = "UseDevelopmentStorage=true", ContainerName = "tenant-b-claims" };

				return new AzureBlobClaimCheckProvider(
					Microsoft.Extensions.Options.Options.Create(options),
					sp.GetRequiredService<ILogger<AzureBlobClaimCheckProvider>>());
			});

			// Tenant resolver
			_ = services.AddScoped(static sp =>
			{
				var tenantContext = sp.GetRequiredService<ITenantContext>();
				return sp.GetRequiredKeyedService<IClaimCheckProvider>(tenantContext.TenantId);
			});
		});

	/// <summary>
	/// Example 7: Configuration with health checks and monitoring.
	/// </summary>
	public static IHostBuilder ConfigureWithMonitoring(this IHostBuilder hostBuilder) =>
		hostBuilder.ConfigureServices(static services =>
		{
			_ = services.AddClaimCheck<AzureBlobClaimCheckProvider>(static options =>
			{
				options.ConnectionString = "UseDevelopmentStorage=true";
				options.ContainerName = "monitored-claims";
				options.EnableMetrics = true;
			});

			// Add health checks
			_ = services.AddHealthChecks()
				.AddAzureBlobStorage(
					connectionString: "UseDevelopmentStorage=true",
					containerName: "monitored-claims",
					name: "claim-check-storage",
					tags: Tags);

			// Note: Metrics collection is enabled through the options above The actual metrics are collected internally by the provider

			// Add OpenTelemetry (if you have the necessary packages installed) services.AddOpenTelemetry() .WithMetrics(builder => {
			// builder.AddMeter("Excalibur.Dispatch.CloudNativePatterns.ClaimCheck"); // Add exporters as needed (Prometheus, OTLP, etc.) });
		});
}

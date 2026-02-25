// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.CloudNativePatterns.Examples.ClaimCheck;

/// <summary>
/// Example application demonstrating the Claim Check pattern implementation. This example shows how to handle large messages by storing
/// payloads in Azure Blob Storage.
/// </summary>
public sealed class Program
{
	public static async Task Main(string[] args)
	{
		var host = Host.CreateDefaultBuilder(args)
			.ConfigureServices(static (context, services) =>
			{
				// Configure Azure Blob Storage for claim check
				_ = services.AddClaimCheck<AzureBlobClaimCheckProvider>(static options =>
				{
					options.ConnectionString = "UseDevelopmentStorage=true"; // Azurite connection
					options.ContainerName = "claim-checks";
					options.PayloadThreshold = 64 * 1024; // 64KB threshold
					options.EnableCompression = true;
					options.CompressionThreshold = 1024; // 1KB
					options.EnableCleanup = true;
					options.CleanupInterval = TimeSpan.FromMinutes(5);
					options.RetentionPeriod = TimeSpan.FromHours(24);
				});

				// Add example services
				_ = services.AddHostedService<ClaimCheckDemoService>();
			})
			.Build();

		await host.RunAsync().ConfigureAwait(false);
	}
}

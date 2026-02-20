// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Azure-specific Dispatch pattern services.
/// </summary>
public static class AzurePatternsServiceCollectionExtensions
{
	/// <summary>
	/// Adds Azure Blob Storage Claim Check implementation.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure claim check options. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// This registers <see cref="AzureBlobClaimCheckProvider"/> as the implementation of <see cref="IClaimCheckProvider"/>.
	/// Requires Azure.Storage.Blobs package and valid Azure Blob Storage connection string in options.
	/// </remarks>
	public static IServiceCollection AddAzureBlobClaimCheck(
		this IServiceCollection services,
		Action<ClaimCheckOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		_ = services.Configure(configureOptions);
		services.TryAddSingleton<IClaimCheckProvider, AzureBlobClaimCheckProvider>();

		return services;
	}

	/// <summary>
	/// Adds Azure Blob Storage Claim Check implementation with cleanup service.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure claim check options. </param>
	/// <param name="enableCleanup">
	/// Whether to enable background cleanup service. When <c>true</c>, registers
	/// a hosted background service for automatic cleanup of expired payloads
	/// based on <see cref="ClaimCheckOptions.CleanupInterval"/>.
	/// </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// This registers <see cref="AzureBlobClaimCheckProvider"/> as the implementation of <see cref="IClaimCheckProvider"/>
	/// and optionally adds a background service for automatic cleanup of expired payloads.
	/// </remarks>
	public static IServiceCollection AddAzureBlobClaimCheck(
		this IServiceCollection services,
		Action<ClaimCheckOptions> configureOptions,
		bool enableCleanup)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		return services.AddClaimCheck<AzureBlobClaimCheckProvider>(configureOptions, enableCleanup);
	}
}

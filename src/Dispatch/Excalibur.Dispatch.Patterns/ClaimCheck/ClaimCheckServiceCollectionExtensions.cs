// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Claim Check pattern services.
/// </summary>
/// <remarks>
/// For provider-specific implementations, use the appropriate provider package:
/// - Azure Blob Storage: Excalibur.Dispatch.Patterns.Azure (AddAzureBlobClaimCheck)
/// - AWS S3: Excalibur.Dispatch.Patterns.Aws (AddAwsS3ClaimCheck) [planned]
/// - Google Cloud Storage: Excalibur.Dispatch.Patterns.Gcp (AddGcpClaimCheck) [planned]
/// </remarks>
public static class ClaimCheckServiceCollectionExtensions
{
	/// <summary>
	/// Adds Claim Check pattern services to the service collection with a custom provider.
	/// </summary>
	/// <typeparam name="TProvider"> The type of claim check provider. </typeparam>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Action to configure claim check options. </param>
	/// <param name="enableCleanup"> Whether to enable automatic cleanup background service. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// Use this method to register a custom IClaimCheckProvider implementation.
	/// For built-in providers (Azure, AWS, GCP), use the provider-specific packages.
	/// </remarks>
	public static IServiceCollection AddClaimCheck<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
		this IServiceCollection services,
		Action<ClaimCheckOptions>? configureOptions = null,
		bool enableCleanup = false)
		where TProvider : class, IClaimCheckProvider
	{
		var optionsBuilder = services.AddOptions<ClaimCheckOptions>();
		if (configureOptions != null)
		{
			optionsBuilder.Configure(configureOptions);
		}

		optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ClaimCheckOptions>, ClaimCheckOptionsValidator>());

		services.TryAddSingleton<IClaimCheckProvider, TProvider>();
		services.TryAddSingleton<IClaimCheckNamingStrategy, DefaultClaimCheckNamingStrategy>();

		// Add background service for cleanup if enabled
		if (enableCleanup)
		{
			_ = services.AddHostedService<ClaimCheckCleanupService>();
		}

		return services;
	}
}

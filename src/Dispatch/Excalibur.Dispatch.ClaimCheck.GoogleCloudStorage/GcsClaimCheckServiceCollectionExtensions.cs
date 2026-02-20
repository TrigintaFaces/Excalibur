// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the Google Cloud Storage Claim Check provider.
/// </summary>
public static class GcsClaimCheckServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Google Cloud Storage Claim Check provider to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for GCS options.</param>
	/// <param name="configureClaimCheck">Optional configuration for core claim check options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddGcsClaimCheck(
		this IServiceCollection services,
		Action<GcsClaimCheckOptions> configure,
		Action<ClaimCheckOptions>? configureClaimCheck = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<GcsClaimCheckOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		var claimCheckBuilder = services.AddOptions<ClaimCheckOptions>();
		if (configureClaimCheck is not null)
		{
			claimCheckBuilder.Configure(configureClaimCheck);
		}

		claimCheckBuilder.ValidateDataAnnotations().ValidateOnStart();

		services.TryAddSingleton<IClaimCheckProvider, GcsClaimCheckStore>();

		return services;
	}
}

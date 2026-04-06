// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.ClaimCheck.AwsS3;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the AWS S3 Claim Check provider.
/// </summary>
public static class AwsS3ClaimCheckServiceCollectionExtensions
{
	/// <summary>
	/// Adds the AWS S3 Claim Check provider to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for S3 options.</param>
	/// <param name="configureClaimCheck">Optional configuration for core claim check options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddAwsS3ClaimCheck(
		this IServiceCollection services,
		Action<AwsS3ClaimCheckOptions> configure,
		Action<ClaimCheckOptions>? configureClaimCheck = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<AwsS3ClaimCheckOptions>()
			.Configure(configure)
			.ValidateOnStart();

		var claimCheckBuilder = services.AddOptions<ClaimCheckOptions>();
		if (configureClaimCheck is not null)
		{
			claimCheckBuilder.Configure(configureClaimCheck);
		}

		claimCheckBuilder.ValidateOnStart();

		services.TryAddSingleton<IClaimCheckProvider, AwsS3ClaimCheckStore>();

		return services;
	}

	/// <summary>
	/// Adds the AWS S3 Claim Check provider using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="AwsS3ClaimCheckOptions"/>.</param>
	/// <param name="claimCheckConfiguration">Optional configuration section for core <see cref="ClaimCheckOptions"/>.</param>
	/// <returns>The service collection for chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAwsS3ClaimCheck(
		this IServiceCollection services,
		IConfiguration configuration,
		IConfiguration? claimCheckConfiguration = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<AwsS3ClaimCheckOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		var claimCheckBuilder = services.AddOptions<ClaimCheckOptions>();
		if (claimCheckConfiguration is not null)
		{
			claimCheckBuilder.Bind(claimCheckConfiguration);
		}

		claimCheckBuilder.ValidateOnStart();

		services.TryAddSingleton<IClaimCheckProvider, AwsS3ClaimCheckStore>();

		return services;
	}
}

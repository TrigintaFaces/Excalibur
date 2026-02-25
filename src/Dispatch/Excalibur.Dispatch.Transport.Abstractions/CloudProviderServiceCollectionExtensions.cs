// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;


namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring cloud provider services.
/// </summary>
public static class CloudProviderServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS cloud provider support to the dispatch builder.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <param name="configure"> Configuration action for AWS services. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	public static IDispatchBuilder AddAwsProviders(this IDispatchBuilder builder, Action<IServiceCollection>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// AWS services registration will be handled by the concrete AWS provider package
		configure?.Invoke(builder.Services);
		return builder;
	}

	/// <summary>
	/// Adds Azure cloud provider support to the dispatch builder.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <param name="configure"> Configuration action for Azure services. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	public static IDispatchBuilder AddAzureProviders(this IDispatchBuilder builder, Action<IServiceCollection>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Azure services registration will be handled by the concrete Azure provider package
		configure?.Invoke(builder.Services);
		return builder;
	}

	/// <summary>
	/// Adds Google Cloud provider support to the dispatch builder.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <param name="configure"> Configuration action for Google Cloud services. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	public static IDispatchBuilder AddGoogleCloudProviders(this IDispatchBuilder builder, Action<IServiceCollection>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Google Cloud services registration will be handled by the concrete Google provider package
		configure?.Invoke(builder.Services);
		return builder;
	}

	/// <summary>
	/// Adds all cloud providers to the dispatch builder.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	public static IDispatchBuilder AddAllCloudProviders(this IDispatchBuilder builder) =>
		builder
			.AddAwsProviders()
			.AddAzureProviders()
			.AddGoogleCloudProviders();

	/// <summary>
	/// Register common cloud provider services.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	internal static IServiceCollection AddCloudProviderCore(this IServiceCollection services) =>

		// Register any common services needed by all cloud providers
		services;
}

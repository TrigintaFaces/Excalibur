// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring ordering key support in Google Pub/Sub.
/// </summary>
public static class OrderingKeyServiceCollectionExtensions
{
	/// <summary>
	/// Adds ordering key processor to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> Optional action to configure options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddGooglePubSubOrderingKey(
		this IServiceCollection services,
		Action<OrderingKeyOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Configure options
		if (configureOptions != null)
		{
			_ = services.Configure(configureOptions);
		}

		// Add options validator
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OrderingKeyOptions>, OrderingKeyOptionsValidator>());

		// Register the processor
		_ = services.AddSingleton<IOrderingKeyProcessor, OrderingKeyProcessor>();

		return services;
	}

	/// <summary>
	/// Adds ordering key processor to the service collection with configuration binding.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section to bind. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddGooglePubSubOrderingKey(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.Configure<OrderingKeyOptions>(configuration);
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<OrderingKeyOptions>, OrderingKeyOptionsValidator>());
		_ = services.AddSingleton<IOrderingKeyProcessor, OrderingKeyProcessor>();

		return services;
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Transport.Google;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring streaming pull services.
/// </summary>
public static class PubSubStreamingPullServiceCollectionExtensions
{
	/// <summary>
	/// Adds Google Pub/Sub streaming pull services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section containing streaming pull options. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("Configuration binding may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Configuration binding uses reflection to dynamically access and populate configuration types")]
	public static IServiceCollection AddGooglePubSubStreamingPull(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Register options
		_ = services.Configure<StreamingPullOptions>(configuration);

		// Register cross-property validator
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<StreamingPullOptions>, StreamingPullOptionsValidator>());

		// Register core services
		_ = services.AddSingleton<StreamHealthMonitor>();
		_ = services.AddSingleton<StreamingPullTelemetry>();
		_ = services.AddTransient<MessageStreamProcessor>();

		// Register factory for creating streaming pull managers
		_ = services.AddSingleton<IStreamingPullManagerFactory, StreamingPullManagerFactory>();

		return services;
	}

	/// <summary>
	/// Adds Google Pub/Sub streaming pull services to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configureOptions"> The action to configure options. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddGooglePubSubStreamingPull(
		this IServiceCollection services,
		Action<StreamingPullOptions> configureOptions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureOptions);

		// Configure options
		_ = services.Configure(configureOptions);

		// Register cross-property validator
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<StreamingPullOptions>, StreamingPullOptionsValidator>());

		// Register core services
		_ = services.AddSingleton<StreamHealthMonitor>();
		_ = services.AddSingleton<StreamingPullTelemetry>();
		_ = services.AddTransient<MessageStreamProcessor>();

		// Register factory for creating streaming pull managers
		_ = services.AddSingleton<IStreamingPullManagerFactory, StreamingPullManagerFactory>();

		return services;
	}
}

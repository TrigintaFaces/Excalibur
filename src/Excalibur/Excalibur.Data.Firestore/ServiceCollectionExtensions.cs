// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.Data.Firestore;

using Google.Cloud.Firestore;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;


namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Firestore services.
/// </summary>
public static class FirestoreServiceCollectionExtensions

{
	/// <summary>
	/// Adds Google Cloud Firestore data provider to the service collection.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> The configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddFirestore(
		this IServiceCollection services,
		Action<FirestoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<FirestoreOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Adds Google Cloud Firestore data provider to the service collection using configuration.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration section. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddFirestore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<FirestoreOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Adds Google Cloud Firestore data provider to the service collection using a named configuration section.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configuration"> The configuration. </param>
	/// <param name="sectionName"> The configuration section name. </param>
	/// <returns> The service collection for chaining. </returns>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddFirestore(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

		_ = services.AddOptions<FirestoreOptions>()
			.Bind(configuration.GetSection(sectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();
		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Adds Google Cloud Firestore data provider with an existing Firestore database.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> The configuration action. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddFirestoreWithDatabase(
		this IServiceCollection services,
		Action<FirestoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<FirestoreOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register the provider using the FirestoreDb from DI
		services.TryAddSingleton(sp =>
		{
			var db = sp.GetRequiredService<FirestoreDb>();
			var options = sp.GetRequiredService<Options.IOptions<FirestoreOptions>>();
			var logger = sp.GetRequiredService<Logging.ILogger<FirestorePersistenceProvider>>();
			return new FirestorePersistenceProvider(db, options, logger);
		});

		services.TryAddSingleton<ICloudNativePersistenceProvider>(sp =>
			sp.GetRequiredService<FirestorePersistenceProvider>());

		services.TryAddSingleton<FirestoreHealthCheck>();

		return services;
	}

	private static void RegisterCoreServices(IServiceCollection services)
	{
		services.TryAddSingleton<FirestorePersistenceProvider>();
		services.TryAddSingleton<ICloudNativePersistenceProvider>(sp =>
			sp.GetRequiredService<FirestorePersistenceProvider>());

		// Register health check
		services.TryAddSingleton<FirestoreHealthCheck>();
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Firestore.Cdc;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Firestore CDC services.
/// </summary>
public static class FirestoreCdcServiceCollectionExtensions
{
	/// <summary>
	/// Adds Firestore CDC processor services with the specified options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure CDC options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="IFirestoreCdcProcessor"/> with the
	/// <see cref="FirestoreCdcProcessor"/> implementation.
	/// </para>
	/// <para>
	/// Requires <see cref="Google.Cloud.Firestore.FirestoreDb"/> to be registered in the service collection.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddFirestoreCdc(
		this IServiceCollection services,
		Action<FirestoreCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<FirestoreCdcOptions>()
			.Configure(configure)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<IFirestoreCdcProcessor, FirestoreCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds Firestore CDC processor services to the service collection using configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration section.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="IFirestoreCdcProcessor"/> with the
	/// <see cref="FirestoreCdcProcessor"/> implementation.
	/// </para>
	/// <para>
	/// Requires <see cref="Google.Cloud.Firestore.FirestoreDb"/> to be registered in the service collection.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddFirestoreCdc(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<FirestoreCdcOptions>()
			.Bind(configuration)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<IFirestoreCdcProcessor, FirestoreCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds Firestore CDC processor services to the service collection using a named configuration section.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration.</param>
	/// <param name="sectionName">The configuration section name.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="IFirestoreCdcProcessor"/> with the
	/// <see cref="FirestoreCdcProcessor"/> implementation.
	/// </para>
	/// <para>
	/// Requires <see cref="Google.Cloud.Firestore.FirestoreDb"/> to be registered in the service collection.
	/// </para>
	/// </remarks>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	public static IServiceCollection AddFirestoreCdc(
		this IServiceCollection services,
		IConfiguration configuration,
		string sectionName)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

		_ = services.AddOptions<FirestoreCdcOptions>()
			.Bind(configuration.GetSection(sectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<IFirestoreCdcProcessor, FirestoreCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds a Firestore-backed state store for CDC position tracking.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Action to configure CDC state store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Stores positions in a dedicated Firestore collection.
	/// </para>
	/// <para>
	/// Requires <see cref="Google.Cloud.Firestore.FirestoreDb"/> to be registered in the service collection.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddFirestoreCdcStateStore(
		this IServiceCollection services,
		Action<FirestoreCdcStateStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		RegisterCdcStateStoreOptions(services, configure);
		services.TryAddSingleton<IFirestoreCdcStateStore, FirestoreCdcStateStore>();

		return services;
	}

	/// <summary>
	/// Adds a Firestore-backed state store for CDC position tracking with a custom collection name.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="collectionName">The collection name for storing positions.</param>
	/// <param name="configure">Optional action to configure additional CDC state store options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// <para>
	/// Stores positions in the specified collection in Firestore.
	/// </para>
	/// <para>
	/// Requires <see cref="Google.Cloud.Firestore.FirestoreDb"/> to be registered in the service collection.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddFirestoreCdcStateStore(
		this IServiceCollection services,
		string collectionName,
		Action<FirestoreCdcStateStoreOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);

		RegisterCdcStateStoreOptions(services, options =>
		{
			options.CollectionName = collectionName;
			configure?.Invoke(options);
		});

		services.TryAddSingleton<IFirestoreCdcStateStore, FirestoreCdcStateStore>();

		return services;
	}

	/// <summary>
	/// Adds an in-memory state store for Firestore CDC position tracking.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// This is intended for testing and development. Positions are not
	/// persisted and will be lost when the process exits.
	/// </remarks>
	public static IServiceCollection AddInMemoryFirestoreCdcStateStore(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<IFirestoreCdcStateStore, InMemoryFirestoreCdcStateStore>();
		return services;
	}

	private static void RegisterCdcStateStoreOptions(
		IServiceCollection services,
		Action<FirestoreCdcStateStoreOptions>? configure)
	{
		var optionsBuilder = services.AddOptions<FirestoreCdcStateStoreOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<FirestoreCdcStateStoreOptions>, FirestoreCdcStateStoreOptionsValidator>());
	}
}

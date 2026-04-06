// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Cdc.Firestore;

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
	public static IServiceCollection AddFirestoreCdc(
		this IServiceCollection services,
		Action<FirestoreCdcOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		_ = services.AddOptions<FirestoreCdcOptions>()
			.Configure(configure)
			.ValidateOnStart();
		services.TryAddSingleton<IFirestoreCdcProcessor, FirestoreCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds Firestore CDC processor services to the service collection using configuration.
	/// </summary>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddFirestoreCdc(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddOptions<FirestoreCdcOptions>()
			.Bind(configuration)
			.ValidateOnStart();
		services.TryAddSingleton<IFirestoreCdcProcessor, FirestoreCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds Firestore CDC processor services to the service collection using a named configuration section.
	/// </summary>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
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
			.ValidateOnStart();
		services.TryAddSingleton<IFirestoreCdcProcessor, FirestoreCdcProcessor>();

		return services;
	}

	/// <summary>
	/// Adds a Firestore-backed state store for CDC position tracking.
	/// </summary>
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
			.ValidateOnStart();

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<FirestoreCdcStateStoreOptions>, FirestoreCdcStateStoreOptionsValidator>());
	}
}

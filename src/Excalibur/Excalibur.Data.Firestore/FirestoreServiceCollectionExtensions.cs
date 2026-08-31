// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Grpc.Core;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.CloudNative;
using Excalibur.Data.Firestore;
using Excalibur.Data.Persistence;

using Google.Cloud.Firestore;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Firestore data services.
/// </summary>
public static class FirestoreServiceCollectionExtensions
{
	/// <summary>
	/// Adds Google Cloud Firestore data provider to the service collection using the fluent builder.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the Firestore data builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburFirestore(firestore =&gt;
	/// {
	///     firestore.ProjectId("my-project")
	///              .CollectionName("data");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddExcaliburFirestore(
		this IServiceCollection services,
		Action<IFirestoreDataBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new FirestoreOptions();
		var firestoreBuilder = new FirestoreDataBuilder(options);
		configure(firestoreBuilder);

		var hasBuilderClient = firestoreBuilder.ClientInstance is not null
			|| firestoreBuilder.ClientFactoryFunc is not null;

		RegisterOptionsAndServices(services, firestoreBuilder, options, hasBuilderClient);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		FirestoreDataBuilder firestoreBuilder,
		FirestoreOptions options,
		bool hasBuilderClient)
	{
		// Register store-specific options from builder state
		_ = services.Configure<FirestoreOptions>(opt =>
		{
			opt.ProjectId = options.ProjectId;
			opt.DefaultCollection = options.DefaultCollection;
			opt.CredentialsPath = options.CredentialsPath;
			opt.CredentialsJson = options.CredentialsJson;
			opt.EmulatorHost = options.EmulatorHost;
		});

		// Register BindConfiguration if set
		if (firestoreBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<FirestoreOptions>()
				.BindConfiguration(firestoreBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		services.AddOptions<FirestoreOptions>().ValidateOnStart();

		// Register validator
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<FirestoreOptions>, FirestoreOptionsValidator>());

		// Register FirestoreDb based on connection path
		if (hasBuilderClient)
		{
			RegisterBuilderManagedClient(services, firestoreBuilder, options);
		}
		else if (firestoreBuilder.EmulatorHostValue is not null)
		{
			var projectId = firestoreBuilder.ProjectIdValue ?? "emulator-project";
			var emulatorHost = firestoreBuilder.EmulatorHostValue;
			services.TryAddSingleton(_ =>
				new FirestoreDbBuilder
				{
					ProjectId = projectId,

					// Endpoint and EmulatorDetection.EmulatorOnly are mutually exclusive: setting both
					// throws "Endpoint is set, contrary to use of EmulatorDetection.EmulatorOnly", so this
					// registration could not resolve a client at all when an emulator host was configured.
					Endpoint = emulatorHost,
					ChannelCredentials = ChannelCredentials.Insecure,
				}.Build());
		}
		else if (firestoreBuilder.ProjectIdValue is not null)
		{
			var projectId = firestoreBuilder.ProjectIdValue;
			services.TryAddSingleton(_ => FirestoreDb.Create(projectId));
		}

		// Register core services
		RegisterCoreServices(services);
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		FirestoreDataBuilder firestoreBuilder,
		FirestoreOptions options)
	{
		const string sentinel = "builder-managed-firestore-project";

		options.ProjectId = sentinel;

		_ = services.Configure<FirestoreOptions>(opt =>
		{
			opt.ProjectId = sentinel;
		});

		if (firestoreBuilder.ClientInstance is not null)
		{
			var client = firestoreBuilder.ClientInstance;
			services.TryAddSingleton(client);
		}
		else if (firestoreBuilder.ClientFactoryFunc is not null)
		{
			var factory = firestoreBuilder.ClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
	}

	private static void RegisterCoreServices(IServiceCollection services)
	{
		services.TryAddSingleton<FirestorePersistenceProvider>();

		// Keyed by provider, matching the relational and search providers. An unkeyed TryAdd alone is a
		// no-op once any other cloud package has registered the same service type, so a host that adds two
		// of them silently resolves the first one's provider for both.
		services.AddKeyedSingleton<ICloudNativePersistenceProvider>("firestore",
			(sp, _) => sp.GetRequiredService<FirestorePersistenceProvider>());
		services.AddKeyedSingleton<IPersistenceProvider>("firestore",
			(sp, _) => sp.GetRequiredService<FirestorePersistenceProvider>());
		services.TryAddKeyedSingleton<IPersistenceProvider>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IPersistenceProvider>("firestore"));
		services.TryAddSingleton<ICloudNativePersistenceProvider>(sp =>
			sp.GetRequiredService<FirestorePersistenceProvider>());

		// Register health check
		services.TryAddSingleton<FirestoreHealthCheck>();
	}
}

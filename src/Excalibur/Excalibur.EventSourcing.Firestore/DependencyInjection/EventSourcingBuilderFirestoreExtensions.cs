// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Grpc.Core;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.CloudNative;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Firestore;

using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Firestore event sourcing on <see cref="IEventSourcingBuilder"/>.
/// </summary>
public static class EventSourcingBuilderFirestoreExtensions
{
	/// <summary>
	/// Configures the event sourcing builder to use Google Cloud Firestore for event storage.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for the Firestore event sourcing builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseFirestore(firestore =&gt;
	///     {
	///         firestore.ProjectId("my-project")
	///                  .CollectionName("events");
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IEventSourcingBuilder UseFirestore(
		this IEventSourcingBuilder builder,
		Action<IFirestoreEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new FirestoreEventStoreOptions();
		var firestoreBuilder = new FirestoreEventSourcingBuilder(options);
		configure(firestoreBuilder);

		var hasBuilderClient = firestoreBuilder.ClientInstance is not null
			|| firestoreBuilder.ClientFactoryFunc is not null;

		RegisterOptionsAndServices(builder, firestoreBuilder, options, hasBuilderClient);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IEventSourcingBuilder builder,
		FirestoreEventSourcingBuilder firestoreBuilder,
		FirestoreEventStoreOptions options,
		bool hasBuilderClient)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<FirestoreEventStoreOptions>(opt =>
		{
			opt.ProjectId = options.ProjectId;
			opt.EventsCollectionName = options.EventsCollectionName;
			opt.CredentialsPath = options.CredentialsPath;
			opt.CredentialsJson = options.CredentialsJson;
			opt.EmulatorHost = options.EmulatorHost;
		});

		// Register BindConfiguration if set
		if (firestoreBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<FirestoreEventStoreOptions>()
				.BindConfiguration(firestoreBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		builder.Services.AddOptions<FirestoreEventStoreOptions>().ValidateOnStart();

		// Register validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<FirestoreEventStoreOptions>, FirestoreEventStoreOptionsValidator>());

		// Register FirestoreDb based on connection path
		if (hasBuilderClient)
		{
			RegisterBuilderManagedClient(builder.Services, firestoreBuilder, options);
		}
		else if (firestoreBuilder.EmulatorHostValue is not null)
		{
			var projectId = firestoreBuilder.ProjectIdValue ?? "emulator-project";
			var emulatorHost = firestoreBuilder.EmulatorHostValue;
			builder.Services.TryAddSingleton(_ =>
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
			var credentialsJson = firestoreBuilder.CredentialsJsonValue;
			var credentialsPath = firestoreBuilder.CredentialsPathValue;

			builder.Services.TryAddSingleton(_ =>
			{
				var dbBuilder = new FirestoreDbBuilder { ProjectId = projectId };

				// An explicitly configured service account must reach the client. FirestoreDb.Create
				// only ever resolves application default credentials, so building through it would
				// discard the configured identity and connect as whatever ambient principal the host
				// happens to expose -- the wrong identity under least-privilege or per-tenant service
				// accounts. Json wins over Path to match the store's own client construction, so both
				// paths resolve the same identity from the same options.
				if (credentialsJson is not null)
				{
					dbBuilder.GoogleCredential = GoogleCredential.FromJson(credentialsJson);
				}
				else if (credentialsPath is not null)
				{
					dbBuilder.GoogleCredential = GoogleCredential.FromFile(credentialsPath);
				}

				// Neither supplied: leave the credential unset so the client falls back to
				// application default credentials.
				return dbBuilder.Build();
			});
		}

		// The store composes the ambient tenant into its document id, so the default context is registered
		// before it: a host that never enabled multi-tenancy still resolves the framework single-tenant
		// default rather than failing to construct the store.
		_ = builder.Services.AddDefaultTenantContext();

		// Register store services. AddTenantAwareStore, not a bare TryAddSingleton: it registers the store
		// AND emits the ITenantScopingCapability<IEventStore> marker inseparably, derived from the store's
		// own constructor shape. A store that stopped taking ITenantContext would silently lose the marker
		// rather than keep attesting a confinement it no longer provides.
		_ = builder.Services.AddTenantAwareStore<IEventStore, FirestoreEventStore>();

		// The store is also registered under ICloudNativeEventStore, which is separately [TenantOwned]. A
		// capability is required per CONTRACT, so attesting IEventStore alone leaves a multi-tenant host
		// refused on the document contract and this store's confinement unreachable through the supported
		// composition. Emitted from the same seam, over the same store, so neither attestation can be
		// present without the ambient tenant the store was built with.
		_ = builder.Services.AddTenantAwareStore<ICloudNativeEventStore, FirestoreEventStore>();
		builder.Services.AddKeyedSingleton<IEventStore>("firestore", (sp, _) => sp.GetRequiredService<FirestoreEventStore>());
		builder.Services.TryAddKeyedSingleton<IEventStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<IEventStore>("firestore"));
		builder.Services.TryAddSingleton<ICloudNativeEventStore>(sp => sp.GetRequiredService<FirestoreEventStore>());
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		FirestoreEventSourcingBuilder firestoreBuilder,
		FirestoreEventStoreOptions options)
	{
		const string sentinel = "builder-managed-firestore-project";

		// Set sentinel so the options validation passes
		options.ProjectId = sentinel;

		_ = services.Configure<FirestoreEventStoreOptions>(opt =>
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
}

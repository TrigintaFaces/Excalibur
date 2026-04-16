// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions.CloudNative;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Firestore;

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
	/// services.AddExcaliburEventSourcing(es =&gt;
	/// {
	///     es.UseFirestore(firestore =&gt;
	///     {
	///         firestore.ProjectId("my-project")
	///                  .CollectionName("events");
	///     })
	///     .AddRepository&lt;OrderAggregate, Guid&gt;();
	/// });
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
				new FirestoreDbBuilder { ProjectId = projectId, EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly, Endpoint = emulatorHost }.Build());
		}
		else if (firestoreBuilder.ProjectIdValue is not null)
		{
			var projectId = firestoreBuilder.ProjectIdValue;
			builder.Services.TryAddSingleton(_ => FirestoreDb.Create(projectId));
		}

		// Register store services
		builder.Services.TryAddSingleton<FirestoreEventStore>();
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

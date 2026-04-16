// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Saga.Firestore;

using Google.Cloud.Firestore;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Firestore saga stores on <see cref="ISagaBuilder"/>.
/// </summary>
public static class SagaBuilderFirestoreExtensions
{
	/// <summary>
	/// Configures the saga builder to use Google Cloud Firestore for saga state storage.
	/// </summary>
	/// <param name="builder">The saga builder.</param>
	/// <param name="configure">Configuration action for the Firestore saga builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="configure"/> is null.
	/// </exception>
	/// <example>
	/// <code>
	/// services.AddExcaliburSaga(saga =&gt;
	/// {
	///     saga.UseFirestore(firestore =&gt;
	///     {
	///         firestore.ProjectId("my-project")
	///                  .CollectionName("sagas");
	///     });
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static ISagaBuilder UseFirestore(
		this ISagaBuilder builder,
		Action<IFirestoreSagaBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new FirestoreSagaOptions();
		var firestoreBuilder = new FirestoreSagaBuilder(options);
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
		ISagaBuilder builder,
		FirestoreSagaBuilder firestoreBuilder,
		FirestoreSagaOptions options,
		bool hasBuilderClient)
	{
		// Register store-specific options from builder state
		_ = builder.Services.Configure<FirestoreSagaOptions>(opt =>
		{
			opt.ProjectId = options.ProjectId;
			opt.CollectionName = options.CollectionName;
			opt.CredentialsPath = options.CredentialsPath;
			opt.CredentialsJson = options.CredentialsJson;
			opt.EmulatorHost = options.EmulatorHost;
		});

		// Register BindConfiguration if set
		if (firestoreBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<FirestoreSagaOptions>()
				.BindConfiguration(firestoreBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		// Register ValidateOnStart
		builder.Services.AddOptions<FirestoreSagaOptions>().ValidateOnStart();

		// Register validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<FirestoreSagaOptions>, FirestoreSagaOptionsValidator>());

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
		builder.Services.TryAddSingleton<FirestoreSagaStore>();
		builder.Services.AddKeyedSingleton<ISagaStore>("firestore", (sp, _) => sp.GetRequiredService<FirestoreSagaStore>());
		builder.Services.TryAddKeyedSingleton<ISagaStore>("default", (sp, _) =>
			sp.GetRequiredKeyedService<ISagaStore>("firestore"));
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		FirestoreSagaBuilder firestoreBuilder,
		FirestoreSagaOptions options)
	{
		const string sentinel = "builder-managed-firestore-project";

		options.ProjectId = sentinel;

		_ = services.Configure<FirestoreSagaOptions>(opt =>
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

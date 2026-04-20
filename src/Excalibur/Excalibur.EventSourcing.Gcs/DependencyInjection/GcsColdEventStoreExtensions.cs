// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Gcs;
using Excalibur.EventSourcing.Gcs.DependencyInjection;

using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the GCS cold event store.
/// </summary>
public static class GcsColdEventStoreExtensions
{
	/// <summary>
	/// Registers the Google Cloud Storage cold event store provider using a fluent builder.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for the GCS builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseTieredStorage(policy =&gt; policy.MaxAge = TimeSpan.FromDays(90));
	///     es.UseGcsColdEventStore(gcs =&gt;
	///     {
	///         gcs.ProjectId("my-project")
	///            .BucketName("my-cold-events");
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IEventSourcingBuilder UseGcsColdEventStore(
		this IEventSourcingBuilder builder,
		Action<IEventSourcingGcsBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var gcsBuilder = new EventSourcingGcsBuilder();
		configure(gcsBuilder);

		RegisterOptionsAndServices(builder, gcsBuilder);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IEventSourcingBuilder builder,
		EventSourcingGcsBuilder gcsBuilder)
	{
		// Configure options from builder state
		_ = builder.Services.Configure<GcsColdEventStoreOptions>(opt =>
		{
			if (gcsBuilder.BucketNameValue is not null)
			{
				opt.BucketName = gcsBuilder.BucketNameValue;
			}

			if (gcsBuilder.ObjectPrefixValue is not null)
			{
				opt.ObjectPrefix = gcsBuilder.ObjectPrefixValue;
			}
		});

		// Register BindConfiguration if set
		if (gcsBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<GcsColdEventStoreOptions>()
				.BindConfiguration(gcsBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		builder.Services.AddOptionsWithValidateOnStart<GcsColdEventStoreOptions>();

		// Register StorageClient based on connection path
		var hasBuilderClient = gcsBuilder.ClientInstance is not null
			|| gcsBuilder.ClientFactoryFunc is not null;

		if (hasBuilderClient)
		{
			RegisterBuilderManagedClient(builder.Services, gcsBuilder);
		}

		// Register cold event store
		builder.Services.TryAddSingleton<IColdEventStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<GcsColdEventStoreOptions>>().Value;

			// Resolve or create StorageClient
			var storageClient = sp.GetService<StorageClient>();
			if (storageClient is null)
			{
				if (gcsBuilder.CredentialsPathValue is not null)
				{
					var credential = GoogleCredential.FromFile(gcsBuilder.CredentialsPathValue);
					storageClient = StorageClient.Create(credential);
				}
				else if (gcsBuilder.CredentialsJsonValue is not null)
				{
					var credential = GoogleCredential.FromJson(gcsBuilder.CredentialsJsonValue);
					storageClient = StorageClient.Create(credential);
				}
				else
				{
					storageClient = StorageClient.Create();
				}
			}

			return new GcsColdEventStore(
				storageClient,
				options.BucketName!,
				options.ObjectPrefix,
				sp.GetRequiredService<ILogger<GcsColdEventStore>>());
		});
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		EventSourcingGcsBuilder gcsBuilder)
	{
		if (gcsBuilder.ClientInstance is not null)
		{
			var client = gcsBuilder.ClientInstance;
			services.TryAddSingleton(client);
		}
		else if (gcsBuilder.ClientFactoryFunc is not null)
		{
			var factory = gcsBuilder.ClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
	}
}

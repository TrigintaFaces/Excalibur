// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Azure.Storage.Blobs;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.AzureBlob;
using Excalibur.EventSourcing.AzureBlob.DependencyInjection;
using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the Azure Blob cold event store.
/// </summary>
public static class AzureBlobColdEventStoreExtensions
{
	/// <summary>
	/// Registers the Azure Blob Storage cold event store provider using a fluent builder.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for the Azure Blob builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseTieredStorage(policy =&gt; policy.MaxAge = TimeSpan.FromDays(90));
	///     es.UseAzureBlobColdEventStore(blob =&gt;
	///     {
	///         blob.ConnectionString("UseDevelopmentStorage=true")
	///             .ContainerName("cold-events");
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IEventSourcingBuilder UseAzureBlobColdEventStore(
		this IEventSourcingBuilder builder,
		Action<IEventSourcingAzureBlobBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var blobBuilder = new EventSourcingAzureBlobBuilder();
		configure(blobBuilder);

		RegisterOptionsAndServices(builder, blobBuilder);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IEventSourcingBuilder builder,
		EventSourcingAzureBlobBuilder blobBuilder)
	{
		// Configure options from builder state
		_ = builder.Services.Configure<AzureBlobColdEventStoreOptions>(opt =>
		{
			if (blobBuilder.ConnectionStringValue is not null)
			{
				opt.ConnectionString = blobBuilder.ConnectionStringValue;
			}

			if (blobBuilder.ContainerNameValue is not null)
			{
				opt.ContainerName = blobBuilder.ContainerNameValue;
			}

			if (blobBuilder.CreateContainerIfNotExistsValue.HasValue)
			{
				opt.CreateContainerIfNotExists = blobBuilder.CreateContainerIfNotExistsValue.Value;
			}
		});

		// Register BindConfiguration if set
		if (blobBuilder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<AzureBlobColdEventStoreOptions>()
				.BindConfiguration(blobBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		builder.Services.AddOptionsWithValidateOnStart<AzureBlobColdEventStoreOptions>();

		// Register BlobServiceClient based on connection path
		var hasBuilderClient = blobBuilder.ClientInstance is not null
			|| blobBuilder.ClientFactoryFunc is not null;

		if (hasBuilderClient)
		{
			RegisterBuilderManagedClient(builder.Services, blobBuilder);
		}

		// Register cold event store
		builder.Services.TryAddSingleton<IColdEventStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<AzureBlobColdEventStoreOptions>>().Value;

			// Resolve or create BlobServiceClient
			var blobServiceClient = sp.GetService<BlobServiceClient>()
				?? new BlobServiceClient(options.ConnectionString);

			var containerClient = blobServiceClient.GetBlobContainerClient(options.ContainerName);

			if (options.CreateContainerIfNotExists)
			{
				containerClient.CreateIfNotExists();
			}

			return new AzureBlobColdEventStore(
				containerClient,
				sp.GetRequiredService<ILogger<AzureBlobColdEventStore>>());
		});
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		EventSourcingAzureBlobBuilder blobBuilder)
	{
		if (blobBuilder.ClientInstance is not null)
		{
			var client = blobBuilder.ClientInstance;
			services.TryAddSingleton(client);
		}
		else if (blobBuilder.ClientFactoryFunc is not null)
		{
			var factory = blobBuilder.ClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
	}
}

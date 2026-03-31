// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	/// Registers the Azure Blob Storage cold event store provider.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Action to configure the Azure Blob options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="IColdEventStore"/> backed by Azure Blob Storage.
	/// Used in combination with <c>UseTieredStorage</c> for hot/cold event separation.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddExcaliburEventSourcing(builder =&gt;
	/// {
	///     builder.UseTieredStorage(policy =&gt;
	///     {
	///         policy.MaxAge = TimeSpan.FromDays(90);
	///     });
	///     builder.UseAzureBlobColdEventStore(options =&gt;
	///     {
	///         options.ConnectionString = "UseDevelopmentStorage=true";
	///         options.ContainerName = "cold-events";
	///     });
	/// });
	/// </code>
	/// </example>
	public static IEventSourcingBuilder UseAzureBlobColdEventStore(
		this IEventSourcingBuilder builder,
		Action<AzureBlobColdEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.Configure(configure);
		builder.Services.AddOptionsWithValidateOnStart<AzureBlobColdEventStoreOptions>();

		builder.Services.TryAddSingleton<IColdEventStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<AzureBlobColdEventStoreOptions>>().Value;
			var blobServiceClient = new BlobServiceClient(options.ConnectionString);
			var containerClient = blobServiceClient.GetBlobContainerClient(options.ContainerName);

			if (options.CreateContainerIfNotExists)
			{
				containerClient.CreateIfNotExists();
			}

			return new AzureBlobColdEventStore(
				containerClient,
				sp.GetRequiredService<ILogger<AzureBlobColdEventStore>>());
		});

		return builder;
	}
}

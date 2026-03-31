// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Gcs;
using Excalibur.EventSourcing.Gcs.DependencyInjection;

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
	/// Registers the Google Cloud Storage cold event store provider.
	/// </summary>
	public static IEventSourcingBuilder UseGcsColdEventStore(
		this IEventSourcingBuilder builder,
		Action<GcsColdEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.Configure(configure);
		builder.Services.AddOptionsWithValidateOnStart<GcsColdEventStoreOptions>();

		builder.Services.TryAddSingleton<IColdEventStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<GcsColdEventStoreOptions>>().Value;
			var storageClient = StorageClient.Create();

			return new GcsColdEventStore(
				storageClient,
				options.BucketName!,
				options.ObjectPrefix,
				sp.GetRequiredService<ILogger<GcsColdEventStore>>());
		});

		return builder;
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.S3;

using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.AwsS3;
using Excalibur.EventSourcing.AwsS3.DependencyInjection;
using Excalibur.EventSourcing.DependencyInjection;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the AWS S3 cold event store.
/// </summary>
public static class AwsS3ColdEventStoreExtensions
{
	/// <summary>
	/// Registers the AWS S3 cold event store provider.
	/// </summary>
	public static IEventSourcingBuilder UseAwsS3ColdEventStore(
		this IEventSourcingBuilder builder,
		Action<AwsS3ColdEventStoreOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		builder.Services.Configure(configure);
		builder.Services.AddOptionsWithValidateOnStart<AwsS3ColdEventStoreOptions>();

		builder.Services.TryAddSingleton<IColdEventStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<AwsS3ColdEventStoreOptions>>().Value;

			var s3Config = new AmazonS3Config();
			if (options.Region is not null)
			{
				s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
			}

			var s3Client = new AmazonS3Client(s3Config);

			return new AwsS3ColdEventStore(
				s3Client,
				options.BucketName!,
				options.KeyPrefix,
				sp.GetRequiredService<ILogger<AwsS3ColdEventStore>>());
		});

		return builder;
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Amazon.S3;

using Excalibur.EventSourcing;
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
	/// Registers the AWS S3 cold event store provider using a fluent builder.
	/// </summary>
	/// <param name="builder">The event sourcing builder.</param>
	/// <param name="configure">Configuration action for the S3 builder.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddExcalibur(x => x.AddEventSourcing(es =&gt;
	/// {
	///     es.UseTieredStorage(policy =&gt; policy.MaxAge = TimeSpan.FromDays(90));
	///     es.UseAwsS3ColdEventStore(s3 =&gt;
	///     {
	///         s3.BucketName("my-cold-events")
	///           .Region("us-east-1");
	///     });
	/// }));
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IEventSourcingBuilder UseAwsS3ColdEventStore(
		this IEventSourcingBuilder builder,
		Action<IEventSourcingAwsS3Builder> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var s3Builder = new EventSourcingAwsS3Builder();
		configure(s3Builder);

		RegisterOptionsAndServices(builder, s3Builder);

		return builder;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IEventSourcingBuilder builder,
		EventSourcingAwsS3Builder s3Builder)
	{
		// Configure options from builder state
		_ = builder.Services.Configure<AwsS3ColdEventStoreOptions>(opt =>
		{
			if (s3Builder.BucketNameValue is not null)
			{
				opt.BucketName = s3Builder.BucketNameValue;
			}

			if (s3Builder.KeyPrefixValue is not null)
			{
				opt.KeyPrefix = s3Builder.KeyPrefixValue;
			}

			if (s3Builder.RegionValue is not null)
			{
				opt.Region = s3Builder.RegionValue;
			}
		});

		// Register BindConfiguration if set
		if (s3Builder.BindConfigurationPath is not null)
		{
			builder.Services.AddOptions<AwsS3ColdEventStoreOptions>()
				.BindConfiguration(s3Builder.BindConfigurationPath)
				.ValidateOnStart();
		}

		builder.Services.AddOptionsWithValidateOnStart<AwsS3ColdEventStoreOptions>();

		// Register IAmazonS3 based on connection path
		var hasBuilderClient = s3Builder.ClientInstance is not null
			|| s3Builder.ClientFactoryFunc is not null;

		if (hasBuilderClient)
		{
			RegisterBuilderManagedClient(builder.Services, s3Builder);
		}
		else if (s3Builder.ServiceUrlValue is not null)
		{
			var serviceUrl = s3Builder.ServiceUrlValue;
			builder.Services.TryAddSingleton<IAmazonS3>(_ =>
				new AmazonS3Client(new AmazonS3Config { ServiceURL = serviceUrl }));
		}

		// Register cold event store
		builder.Services.TryAddSingleton<IColdEventStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<AwsS3ColdEventStoreOptions>>().Value;

			// If no client registered via builder, create one from options
			var s3Client = sp.GetService<IAmazonS3>();
			if (s3Client is null)
			{
				var s3Config = new AmazonS3Config();
				if (options.Region is not null)
				{
					s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
				}

				s3Client = new AmazonS3Client(s3Config);
			}

			return new AwsS3ColdEventStore(
				s3Client,
				options.BucketName!,
				options.KeyPrefix,
				sp.GetRequiredService<ILogger<AwsS3ColdEventStore>>());
		});
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		EventSourcingAwsS3Builder s3Builder)
	{
		if (s3Builder.ClientInstance is not null)
		{
			var client = s3Builder.ClientInstance;
			services.TryAddSingleton(client);
		}
		else if (s3Builder.ClientFactoryFunc is not null)
		{
			var factory = s3Builder.ClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
	}
}

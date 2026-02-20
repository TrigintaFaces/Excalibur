// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using CloudNative.CloudEvents;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Options.CloudEvents;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring CloudEvents support in Excalibur.Dispatch.
/// </summary>
public static class CloudEventsServiceCollectionExtensions
{
	private static readonly Type CloudEventMapperOpenGenericType = Excalibur.Dispatch.TypeResolution.TypeResolver.ResolveType(
																	   "Excalibur.Dispatch.Transport.ICloudEventMapper`1, Excalibur.Dispatch.Transport.Abstractions")
																   ?? throw new InvalidOperationException(
																	   "Unable to locate Excalibur.Dispatch.Transport CloudEvent mapper type.");

	/// <summary>
	/// Adds CloudEvents support to the Dispatch pipeline.
	/// </summary>
	/// <param name="builder"> The dispatch builder. </param>
	/// <param name="configureOptions"> Optional action to configure CloudEvent options. </param>
	/// <returns> The dispatch builder for chaining. </returns>
	public static IDispatchBuilder AddCloudEvents(
		this IDispatchBuilder builder,
		Action<CloudEventOptions>? configureOptions = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Register CloudEvent options using the Options pattern
		_ = builder.Services.AddOptions<CloudEventOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		if (configureOptions != null)
		{
			_ = builder.Services.Configure(configureOptions);
		}

		builder.Services.TryAddSingleton(static serviceProvider =>
			serviceProvider.GetRequiredService<IOptions<CloudEventOptions>>().Value);

		// Core CloudEvent services
		builder.Services.TryAddSingleton<ICloudEventEnvelopeConverter, CloudEventEnvelopeConverter>();
		builder.Services.TryAddSingleton<IEnvelopeCloudEventBridge, EnvelopeCloudEventBridge>();

		// Expose bridge factory cached per transport
		builder.Services.TryAddSingleton<Func<string, IEnvelopeCloudEventBridge>>(static serviceProvider =>
		{
			var cache = new ConcurrentDictionary<string, IEnvelopeCloudEventBridge>(StringComparer.OrdinalIgnoreCase);
			return transportName =>
			{
				ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
				return cache.GetOrAdd(
					transportName,
					static (_, provider) => provider.GetRequiredService<IEnvelopeCloudEventBridge>(),
					serviceProvider);
			};
		});

		builder.Services.TryAddSingleton(static serviceProvider =>
			GetMapperFactory(serviceProvider));

		// Register middleware
		builder.Services.TryAddTransient<CloudEventMiddleware>();

		// Add to pipeline
		return builder.UseMiddleware<CloudEventMiddleware>();
	}

	/// <summary>
	/// Adds a schema registry for CloudEvent schema management.
	/// </summary>
	public static IDispatchBuilder AddCloudEventSchemaRegistry<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TRegistry>(
		this IDispatchBuilder builder)
		where TRegistry : class, ISchemaRegistry
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.TryAddSingleton<ISchemaRegistry, TRegistry>();

		// Enable schema features
		_ = builder.Services.Configure<CloudEventOptions>(static options =>
		{
			options.ValidateSchema = true;
			options.IncludeSchemaVersion = true;
		});

		return builder;
	}

	/// <summary>
	/// Adds an in-memory schema registry for development/testing.
	/// </summary>
	public static IDispatchBuilder AddInMemorySchemaRegistry(this IDispatchBuilder builder) =>
		builder.AddCloudEventSchemaRegistry<InMemorySchemaRegistry>();

	/// <summary>
	/// Configures automatic schema registration for CloudEvents.
	/// </summary>
	public static IDispatchBuilder AddCloudEventSchemaAutoRegistration(
		this IDispatchBuilder builder,
		Func<Type, string> schemaProvider,
		Func<Type, string>? versionProvider = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(schemaProvider);

		_ = builder.Services.Configure<CloudEventOptions>(options =>
		{
			options.AutoRegisterSchemas = true;
			options.SchemaProvider = schemaProvider;
			options.SchemaVersionProvider = versionProvider ?? (type => "1.0");
		});

		return builder;
	}

	/// <summary>
	/// Adds CloudEvent batching support.
	/// </summary>
	public static IDispatchBuilder AddCloudEventBatching(
		this IDispatchBuilder builder,
		Action<CloudEventBatchOptions>? configureBatch = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Register batch options
		var batchOptions = new CloudEventBatchOptions();
		configureBatch?.Invoke(batchOptions);
		builder.Services.TryAddSingleton(batchOptions);

		// Register batch processor
		builder.Services.TryAddTransient<ICloudEventBatchProcessor, DefaultCloudEventBatchProcessor>();

		return builder;
	}

	/// <summary>
	/// Adds custom CloudEvent validation.
	/// </summary>
	public static IDispatchBuilder AddCloudEventValidation(
		this IDispatchBuilder builder,
		Func<CloudEvent, CancellationToken, Task<bool>> validator)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(validator);

		_ = builder.Services.Configure<CloudEventOptions>(options => options.CustomValidator = validator);

		return builder;
	}

	/// <summary>
	/// Adds custom CloudEvent transformation for outgoing events.
	/// </summary>
	public static IDispatchBuilder AddCloudEventTransformation(
		this IDispatchBuilder builder,
		Func<CloudEvent, IDispatchEvent, IMessageContext, CancellationToken, Task> transformer)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(transformer);

		_ = builder.Services.Configure<CloudEventOptions>(options =>
		{
			var existingTransformer = options.OutgoingTransformer;
			options.OutgoingTransformer = async (ce, evt, ctx, ct) =>
			{
				// Call existing transformer first
				if (existingTransformer != null)
				{
					await existingTransformer(ce, evt, ctx, ct).ConfigureAwait(false);
				}

				// Then call the new transformer
				await transformer(ce, evt, ctx, ct).ConfigureAwait(false);
			};
		});

		return builder;
	}

	/// <summary>
	/// Configures CloudEvent extension attributes to include/exclude.
	/// </summary>
	public static IDispatchBuilder ConfigureCloudEventExtensions(
		this IDispatchBuilder builder,
		Action<HashSet<string>> configureExclusions)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configureExclusions);

		_ = builder.Services.Configure<CloudEventOptions>(options => configureExclusions(options.ExcludedExtensions));

		return builder;
	}

	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using member which requires dynamic code can break when AOT compiling",
		Justification =
			"Mapper resolution is opt-in and based on runtime transport types. Consumers targeting AOT should register concrete mappers explicitly.")]
	private static Func<Type, object> GetMapperFactory(IServiceProvider serviceProvider)
	{
		var cache = new ConcurrentDictionary<Type, object>();
		return transportType =>
		{
			ArgumentNullException.ThrowIfNull(transportType);
			return cache.GetOrAdd(
				transportType,
				static (type, provider) =>
				{
					var mapperType = CloudEventMapperOpenGenericType.MakeGenericType(type);
					return provider.GetRequiredService(mapperType);
				},
				serviceProvider);
		};
	}
}

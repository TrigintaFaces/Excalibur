// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.Protobuf;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Protobuf serialization support.
/// </summary>
/// <remarks>
/// This is an opt-in serialization package for GCP/AWS/external Protobuf interoperability.
/// Does NOT change Excalibur.Dispatch defaults (MemoryPack remains internal wire format).
/// </remarks>
public static class ProtobufSerializationExtensions
{
	/// <summary>
	/// Adds Protobuf serialization support to the Dispatch pipeline.
	/// This is an opt-in package for Google Cloud Platform and AWS interoperability.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration delegate for Protobuf serialization options.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddProtobufSerialization(
		this IServiceCollection services,
		Action<ProtobufSerializationOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Always register options
		var optionsBuilder = services.AddOptions<ProtobufSerializationOptions>()
			.Configure(options =>
			{
				configure?.Invoke(options);
			});
		optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register the consolidated Protobuf serializer
		services.TryAddSingleton<ProtobufSerializer>();
		services.TryAddSingleton<ISerializer>(sp => sp.GetRequiredService<ProtobufSerializer>());

		return services;
	}

	/// <summary>
	/// Registers the Protobuf serializer with the serialization builder (framework-assigned ID: 4).
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This extension enables the builder pattern:
	/// </para>
	/// <code>
	/// services.AddDispatch()
	///     .WithSerialization(config =>
	///     {
	///         config.RegisterProtobuf();
	///         config.UseProtobuf();
	///     });
	/// </code>
	/// </remarks>
	public static ISerializationBuilder RegisterProtobuf(this ISerializationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.Register(new ProtobufSerializer(), SerializerIds.Protobuf);
	}

	/// <summary>
	/// Registers the Protobuf serializer with configuration (framework-assigned ID: 4).
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <param name="configure">Configuration delegate for Protobuf serialization options.</param>
	/// <returns>The builder for method chaining.</returns>
	public static ISerializationBuilder RegisterProtobuf(
		this ISerializationBuilder builder,
		Action<ProtobufSerializationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ProtobufSerializationOptions();
		configure(options);

		return builder.Register(new ProtobufSerializer(options), SerializerIds.Protobuf);
	}

	/// <summary>
	/// Gets the Protobuf pluggable serializer singleton instance for use with <see cref="ISerializerRegistry"/>.
	/// </summary>
	/// <returns>The Protobuf pluggable serializer instance.</returns>
	public static ISerializer GetPluggableSerializer() => new ProtobufSerializer();

	/// <summary>
	/// Adds the Protobuf serializer to the pluggable serialization system.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="setAsCurrent">
	/// Whether to set Protobuf as the current serializer for new payloads.
	/// Default is <c>false</c> (JSON remains default per ADR-295).
	/// </param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddProtobufPluggableSerialization(
		this IServiceCollection services,
		bool setAsCurrent = false)
	{
		ArgumentNullException.ThrowIfNull(services);

		return PluggableSerializationServiceCollectionExtensions
			.AddPluggableSerializer(
				services,
				SerializerIds.Protobuf,
				new ProtobufSerializer(),
				setAsCurrent);
	}
}

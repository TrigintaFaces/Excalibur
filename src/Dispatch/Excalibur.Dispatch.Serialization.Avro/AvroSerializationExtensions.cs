// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.Avro;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Avro serialization support.
/// </summary>
/// <remarks>
/// This is an opt-in serialization package for Apache Avro (schema-based binary serialization).
/// Does NOT change Excalibur.Dispatch defaults (MemoryPack remains internal wire format).
/// </remarks>
public static class AvroSerializationExtensions
{
	/// <summary>
	/// Adds Avro serialization support to the Dispatch pipeline.
	/// This is an opt-in package for schema-based binary serialization
	/// optimized for streaming and Kafka scenarios.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddAvroSerialization(
		this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Register the consolidated Avro serializer
		services.TryAddSingleton<AvroSerializer>();
		services.TryAddSingleton<ISerializer>(sp => sp.GetRequiredService<AvroSerializer>());

		return services;
	}

	/// <summary>
	/// Registers the Avro serializer with the serialization builder (framework-assigned ID: 5).
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
	///         config.RegisterAvro();
	///         config.UseAvro();
	///     });
	/// </code>
	/// </remarks>
	public static ISerializationBuilder RegisterAvro(this ISerializationBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return builder.Register(new AvroSerializer(), SerializerIds.Avro);
	}

	/// <summary>
	/// Registers the Avro serializer with configuration (framework-assigned ID: 5).
	/// </summary>
	/// <param name="builder">The serialization builder.</param>
	/// <param name="configure">Configuration delegate for Avro serialization options.</param>
	/// <returns>The builder for method chaining.</returns>
	public static ISerializationBuilder RegisterAvro(
		this ISerializationBuilder builder,
		Action<AvroSerializationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new AvroSerializationOptions();
		configure(options);

		return builder.Register(new AvroSerializer(options), SerializerIds.Avro);
	}

	/// <summary>
	/// Gets the Avro pluggable serializer singleton instance for use with <see cref="ISerializerRegistry"/>.
	/// </summary>
	/// <returns>The Avro pluggable serializer instance.</returns>
	/// <remarks>
	/// <para>
	/// <b>Serializer ID:</b> <see cref="SerializerIds.Avro"/> (5)
	/// </para>
	/// </remarks>
	public static ISerializer GetPluggableSerializer() => new AvroSerializer();

	/// <summary>
	/// Adds the Avro serializer to the pluggable serialization system.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="setAsCurrent">
	/// Whether to set Avro as the current serializer for new payloads.
	/// Default is <c>false</c> (JSON remains default per ADR-295).
	/// </param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers <see cref="AvroSerializer"/> with
	/// <see cref="SerializerIds.Avro"/> (5) in the pluggable serialization registry.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddAvroPluggableSerialization(
		this IServiceCollection services,
		bool setAsCurrent = false)
	{
		ArgumentNullException.ThrowIfNull(services);

		return PluggableSerializationServiceCollectionExtensions
			.AddPluggableSerializer(
				services,
				SerializerIds.Avro,
				new AvroSerializer(),
				setAsCurrent);
	}
}

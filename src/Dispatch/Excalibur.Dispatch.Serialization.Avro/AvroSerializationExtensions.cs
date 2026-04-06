// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.Avro;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Avro serialization support.
/// </summary>
public static class AvroSerializationExtensions
{
	/// <summary>
	/// Adds Avro as the binary serializer for internal persistence (Outbox, Inbox, Event Store).
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <remarks>
	/// <para>
	/// This is the single entry point for opting into Avro. It registers:
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="ISerializer"/> — Avro serializer singleton.</description></item>
	/// <item><description>Serializer registry — Avro registered with ID <see cref="SerializerIds.Avro"/> and set as current.</description></item>
	/// </list>
	/// <para>
	/// <b>Usage:</b>
	/// </para>
	/// <code>
	/// services.AddAvroSerializer();
	/// </code>
	/// <para>
	/// <b>Note:</b> JSON (System.Text.Json) is the default serializer (ADR-295).
	/// Call this method to opt into Avro for schema-based binary serialization
	/// optimized for streaming and Kafka scenarios.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAvroSerializer(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		var serializer = new AvroSerializer();

		services.TryAddSingleton<ISerializer>(serializer);

		_ = services.AddOptions<PluggableSerializationOptions>()
			.ValidateOnStart();

		services.PostConfigure<PluggableSerializationOptions>(options =>
		{
			options.AddRegistration(registry => registry.Register(SerializerIds.Avro, serializer));
			options.CurrentSerializerName = "Avro";
		});

		return services;
	}

	/// <summary>
	/// Adds Avro as the binary serializer with custom options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Configuration delegate for Avro serialization options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddAvroSerializer(
		this IServiceCollection services,
		Action<AvroSerializationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new AvroSerializationOptions();
		configure(options);

		var serializer = new AvroSerializer(options);

		services.TryAddSingleton<ISerializer>(serializer);

		_ = services.AddOptions<PluggableSerializationOptions>()
			.ValidateOnStart();

		services.PostConfigure<PluggableSerializationOptions>(opts =>
		{
			opts.AddRegistration(registry => registry.Register(SerializerIds.Avro, serializer));
			opts.CurrentSerializerName = "Avro";
		});

		return services;
	}
}

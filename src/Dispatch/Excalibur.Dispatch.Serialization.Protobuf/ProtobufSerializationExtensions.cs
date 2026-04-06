// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.Protobuf;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Protobuf serialization support.
/// </summary>
public static class ProtobufSerializationExtensions
{
	/// <summary>
	/// Adds Protobuf as the binary serializer for internal persistence (Outbox, Inbox, Event Store).
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <remarks>
	/// <para>
	/// This is the single entry point for opting into Protobuf. It registers:
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="ISerializer"/> — Protobuf serializer singleton.</description></item>
	/// <item><description>Serializer registry — Protobuf registered with ID <see cref="SerializerIds.Protobuf"/> and set as current.</description></item>
	/// </list>
	/// <para>
	/// <b>Usage:</b>
	/// </para>
	/// <code>
	/// services.AddProtobufSerializer();
	/// </code>
	/// <para>
	/// <b>Note:</b> JSON (System.Text.Json) is the default serializer (ADR-295).
	/// Call this method to opt into Protobuf for Google Cloud Platform and AWS interoperability.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddProtobufSerializer(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		var serializer = new ProtobufSerializer();

		services.TryAddSingleton<ISerializer>(serializer);

		_ = services.AddOptions<PluggableSerializationOptions>()
			.ValidateOnStart();

		services.PostConfigure<PluggableSerializationOptions>(options =>
		{
			options.AddRegistration(registry => registry.Register(SerializerIds.Protobuf, serializer));
			options.CurrentSerializerName = "Protobuf";
		});

		return services;
	}

	/// <summary>
	/// Adds Protobuf as the binary serializer with custom options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="configure"> Configuration delegate for Protobuf serialization options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options validation/binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated alternatives.")]
	public static IServiceCollection AddProtobufSerializer(
		this IServiceCollection services,
		Action<ProtobufSerializationOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var options = new ProtobufSerializationOptions();
		configure(options);

		var serializer = new ProtobufSerializer(options);

		services.TryAddSingleton<ISerializer>(serializer);

		_ = services.AddOptions<PluggableSerializationOptions>()
			.ValidateOnStart();

		services.PostConfigure<PluggableSerializationOptions>(opts =>
		{
			opts.AddRegistration(registry => registry.Register(SerializerIds.Protobuf, serializer));
			opts.CurrentSerializerName = "Protobuf";
		});

		return services;
	}
}

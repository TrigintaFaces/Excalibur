// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MessagePack serialization support.
/// </summary>
public static class MessagePackSerializationExtensions
{
	/// <summary>
	/// Adds MessagePack as the binary serializer for internal persistence (Outbox, Inbox, Event Store).
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <remarks>
	/// <para>
	/// This is the single entry point for opting into MessagePack. It registers:
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="ISerializer"/> — MessagePack serializer singleton.</description></item>
	/// <item><description>Serializer registry — MessagePack registered with ID <see cref="SerializerIds.MessagePack"/> and set as current.</description></item>
	/// </list>
	/// <para>
	/// <b>Usage:</b>
	/// </para>
	/// <code>
	/// services.AddMessagePackSerializer();
	/// </code>
	/// <para>
	/// <b>Note:</b> JSON (System.Text.Json) is the default serializer (ADR-295).
	/// Call this method to opt into MessagePack for high-performance binary serialization.
	/// </para>
	/// </remarks>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "MessagePack serializer type is preserved through DI registration.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "MessagePack serializer requires dynamic code for type handling.")]
	public static IServiceCollection AddMessagePackSerializer(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		var serializer = new MessagePackSerializer();

		services.TryAddSingleton<ISerializer>(serializer);

		services.PostConfigure<PluggableSerializationOptions>(options =>
		{
			options.AddRegistration(registry => registry.Register(SerializerIds.MessagePack, serializer));
			options.CurrentSerializerName = "MessagePack";
		});

		return services;
	}

	/// <summary>
	/// Adds MessagePack as the binary serializer with custom options.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="options"> Custom MessagePack serializer options. </param>
	/// <returns> The service collection for method chaining. </returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "MessagePack serializer type is preserved through DI registration.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "MessagePack serializer requires dynamic code for type handling.")]
	public static IServiceCollection AddMessagePackSerializer(
		this IServiceCollection services,
		global::MessagePack.MessagePackSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		var serializer = new MessagePackSerializer(options);

		services.TryAddSingleton<ISerializer>(serializer);

		services.PostConfigure<PluggableSerializationOptions>(opts =>
		{
			opts.AddRegistration(registry => registry.Register(SerializerIds.MessagePack, serializer));
			opts.CurrentSerializerName = "MessagePack";
		});

		return services;
	}
}

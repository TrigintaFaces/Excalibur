// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring MemoryPack serialization.
/// </summary>
public static class MemoryPackSerializationServiceCollectionExtensions
{
	/// <summary>
	/// Adds MemoryPack as the binary serializer for internal persistence (Outbox, Inbox, Event Store).
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for method chaining. </returns>
	/// <remarks>
	/// <para>
	/// This is the single entry point for opting into MemoryPack. It registers:
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="ISerializer"/> — MemoryPack serializer singleton.</description></item>
	/// <item><description><see cref="IBinaryEnvelopeDeserializer"/> — Binary envelope support for inbox/outbox processors.</description></item>
	/// <item><description>Serializer registry — MemoryPack registered with ID <see cref="SerializerIds.MemoryPack"/> and set as current.</description></item>
	/// </list>
	/// <para>
	/// <b>Usage:</b>
	/// </para>
	/// <code>
	/// services.AddMemoryPackSerializer();
	/// </code>
	/// <para>
	/// <b>Note:</b> JSON (System.Text.Json) is the default serializer (ADR-295).
	/// Call this method to opt into MemoryPack for high-performance binary serialization
	/// in .NET-only environments. Consumer event types do not need <c>[MemoryPackable]</c> —
	/// only the internal envelope wrapper uses MemoryPack attributes.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddMemoryPackSerializer(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		var serializer = new MemoryPackSerializer();

		// DI registrations
		services.TryAddSingleton<ISerializer>(serializer);
		services.TryAddSingleton<IBinaryEnvelopeDeserializer, MemoryPackEnvelopeDeserializer>();

		// Serializer registry: register MemoryPack and set as current
		services.PostConfigure<PluggableSerializationOptions>(options =>
		{
			options.AddRegistration(registry => registry.Register(SerializerIds.MemoryPack, serializer));
			options.CurrentSerializerName = "MemoryPack";
		});

		return services;
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization;

using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extensions that select the reflection-free event serializer.
/// </summary>
/// <remarks>
/// <para>
/// The default event serializer resolves event types and their JSON shape by reflection, which the
/// trimmer cannot see through and Native AOT cannot execute. Registering the source-generated serializer
/// replaces that path entirely: event types resolve through the allow-list populated by
/// <c>AddEventTypes(...)</c>, and payloads are read and written through the consumer's
/// <see cref="JsonSerializerContext"/> — no reflection on either side.
/// </para>
/// <example>
/// <code>
/// [JsonSourceGenerationOptions(
///     PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
///     UseStringEnumConverter = true,
///     DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
/// [JsonSerializable(typeof(OrderPlaced))]
/// [JsonSerializable(typeof(OrderShipped))]
/// [JsonSerializable(typeof(string))]   // metadata value types
/// [JsonSerializable(typeof(int))]
/// internal sealed partial class AppEventContext : JsonSerializerContext;
///
/// services.AddDispatch();
/// services.AddEventTypes&lt;OrderPlaced&gt;().AddEventTypes&lt;OrderShipped&gt;();
/// services.AddAotEventSerializer(AppEventContext.Default);
/// </code>
/// </example>
/// </remarks>
public static class AotEventSerializerServiceCollectionExtensions
{
	/// <summary>
	/// Replaces the registered <see cref="IEventSerializer"/> with the reflection-free, source-generated
	/// serializer backed by <paramref name="jsonContext"/>.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="jsonContext">
	/// The source-generated JSON serializer context declaring every event type — and every metadata value
	/// type — the application serializes. Pass the generated <c>Default</c> instance. It must carry
	/// <c>[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
	/// UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]</c> so
	/// its payloads stay byte-compatible with events already written by the reflection-based serializer;
	/// a context that diverges is rejected when the serializer is resolved.
	/// </param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	/// <remarks>
	/// Call order does not matter: this registration replaces the default serializer if it is already
	/// present, and suppresses it if <c>AddDispatch()</c> runs afterwards. Register the event types the
	/// application loads back from the store with <c>AddEventTypes(...)</c>; this serializer never falls
	/// back to an assembly scan, so an unregistered type name stays unresolvable.
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="services"/> or <paramref name="jsonContext"/> is <see langword="null"/>.
	/// </exception>
	public static IServiceCollection AddAotEventSerializer(
		this IServiceCollection services,
		JsonSerializerContext jsonContext)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(jsonContext);

		// Share the one mutable allow-list AddEventTypes(...) populates, whichever call runs first: TryAdd
		// yields to an existing registry, and AddEventTypes adopts this instance when it finds one.
		services.TryAddSingleton<IEventTypeRegistry>(new EventTypeRegistry());

		// Replace rather than Add: the default reflection serializer registers itself with TryAdd, so a
		// plain Add would leave both descriptors present and make the winner depend on call order.
		services.Replace(ServiceDescriptor.Singleton<IEventSerializer>(
			sp => new AotJsonEventSerializer(sp.GetRequiredService<IEventTypeRegistry>(), jsonContext)));

		return services;
	}
}

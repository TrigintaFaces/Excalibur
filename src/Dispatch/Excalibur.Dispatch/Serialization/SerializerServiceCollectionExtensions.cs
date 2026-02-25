// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Options.Serialization;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection.Extensions;
// Type alias for backward compatibility
using JsonMessageSerializer = Excalibur.Dispatch.Serialization.DispatchJsonSerializer;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering message serializers with the dependency injection container. Provides support for default JSON
/// serialization and custom serializer registration with versioning.
/// </summary>
public static class SerializerServiceCollectionExtensions
{
	/// <summary>
	/// Registers the default JSON message serializer and related services with the dependency injection container.
	/// </summary>
	/// <param name="services"> The service collection to add services to. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddDispatchSerializer(this IServiceCollection services)
	{
		services.TryAddSingleton<IMessageSerializer, JsonMessageSerializer>();
		services.TryAddSingleton<JsonMessageSerializer>();
		services.TryAddSingleton<DispatchJsonContext>();

		_ = services.AddOptions<MessageSerializerOptions>()
			.Configure(static options => options.SerializerMap[0] = typeof(JsonMessageSerializer))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<MessageSerializerFactory>();

		return services;
	}

	/// <summary>
	/// Registers a custom message serializer with the specified version mapping.
	/// </summary>
	/// <typeparam name="TSerializer"> The type of the serializer to register. </typeparam>
	/// <param name="services"> The service collection to add services to. </param>
	/// <param name="version"> The version number to associate with this serializer. </param>
	/// <returns> The service collection for method chaining. </returns>
	public static IServiceCollection AddDispatchSerializer<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TSerializer>(
		this IServiceCollection services,
		int version)
		where TSerializer : class, IMessageSerializer
	{
		services.TryAddSingleton<TSerializer>();
		services.TryAddSingleton<IMessageSerializer, TSerializer>();

		_ = services.AddOptions<MessageSerializerOptions>()
			.Configure(options => options.SerializerMap[version] = typeof(TSerializer))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.TryAddSingleton<MessageSerializerFactory>();

		return services;
	}
}

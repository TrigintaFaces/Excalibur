// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extensions for AOT-compatible serialization.
/// </summary>
public static class AotSerializationServiceCollectionExtensions
{
	/// <summary>
	/// Adds AOT-compatible JSON serialization using source generation. This method ensures zero ILLink warnings and full native AOT support.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="context"> Optional custom JsonSerializerContext. Defaults to CoreMessageJsonContext. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddAotJsonSerialization(
		this IServiceCollection services,
		JsonSerializerContext? context = null)
	{
		services.TryAddSingleton(_ =>
			context ?? CoreMessageJsonContext.Default);

		services.TryAddSingleton<IMessageSerializer>(sp =>
		{
			var ctx = sp.GetService<JsonSerializerContext>();
			return new AotJsonSerializer(ctx);
		});

		services.TryAddSingleton(sp =>
			sp.GetRequiredService<IMessageSerializer>() as IBinaryMessageSerializer
			?? throw new InvalidOperationException("Registered IMessageSerializer does not implement IBinaryMessageSerializer"));

		return services;
	}

	/// <summary>
	/// Adds AOT-compatible JSON serialization for CloudEvents.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddCloudEventsAotSerialization(this IServiceCollection services)
	{
		services.TryAddSingleton(CloudEventJsonContext.Default);
		return services.AddAotJsonSerialization(CloudEventJsonContext.Default);
	}

	/// <summary>
	/// Adds a composite AOT serializer that can handle types from multiple contexts.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="contexts"> The contexts to combine. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddCompositeAotSerialization(
		this IServiceCollection services,
		params JsonSerializerContext[] contexts)
	{
		services.TryAddSingleton<IMessageSerializer>(_ =>
			new CompositeAotJsonSerializer(contexts));

		services.TryAddSingleton(sp =>
			sp.GetRequiredService<IMessageSerializer>() as IBinaryMessageSerializer
			?? throw new InvalidOperationException("Registered IMessageSerializer does not implement IBinaryMessageSerializer"));

		return services;
	}

	/// <summary>
	/// Adds core message types AOT serialization with CloudEvents support.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <returns> The service collection for chaining. </returns>
	public static IServiceCollection AddCoreAotSerialization(this IServiceCollection services)
	{
		var contexts = new JsonSerializerContext[] { CoreMessageJsonContext.Default, CloudEventJsonContext.Default };

		return services.AddCompositeAotSerialization(contexts);
	}
}

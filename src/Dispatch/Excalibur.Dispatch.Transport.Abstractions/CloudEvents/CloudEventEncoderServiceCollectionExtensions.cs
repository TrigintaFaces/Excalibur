// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helpers that wire a CloudEvent transport encoder together with the trimming/AOT-safe
/// adapter the envelope/CloudEvent bridge uses to dispatch by transport-message type.
/// </summary>
public static class CloudEventEncoderServiceCollectionExtensions
{
	/// <summary>
	/// Registers an <see cref="ICloudEventEncoder{TOutbound}"/> implementation and its bridge adapter.
	/// The adapter closes the encoder's generic at this compile-time call site, so the bridge resolves it by
	/// transport-message type without <see cref="System.Type.MakeGenericType(System.Type[])"/> or reflection.
	/// </summary>
	/// <typeparam name="TOutbound">The transport message type the encoder produces.</typeparam>
	/// <typeparam name="TEncoder">The encoder implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCloudEventEncoder<
		TOutbound,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TEncoder>(
		this IServiceCollection services)
		where TEncoder : class, ICloudEventEncoder<TOutbound>
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ICloudEventEncoder<TOutbound>, TEncoder>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<ICloudEventEncoderAdapter, CloudEventEncoderAdapter<TOutbound>>());

		return services;
	}

	/// <summary>
	/// Registers an <see cref="ICloudEventEncoder{TOutbound}"/> produced by a factory and its bridge
	/// adapter. Use this overload when the encoder needs custom construction (e.g. a captured serializer).
	/// </summary>
	/// <typeparam name="TOutbound">The transport message type the encoder produces.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="factory">Factory that produces the encoder instance.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCloudEventEncoder<TOutbound>(
		this IServiceCollection services,
		Func<IServiceProvider, ICloudEventEncoder<TOutbound>> factory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(factory);

		services.TryAddSingleton(factory);
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<ICloudEventEncoderAdapter, CloudEventEncoderAdapter<TOutbound>>());

		return services;
	}
}

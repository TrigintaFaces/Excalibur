// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helpers that wire a CloudEvent transport mapper together with the trimming/AOT-safe
/// adapter the envelope/CloudEvent bridge uses to dispatch by transport-message type.
/// </summary>
public static class CloudEventMapperServiceCollectionExtensions
{
	/// <summary>
	/// Registers an <see cref="ICloudEventMapper{TTransportMessage}"/> implementation and its bridge adapter.
	/// The adapter closes the mapper's generic at this compile-time call site, so the bridge resolves it by
	/// transport-message type without <see cref="System.Type.MakeGenericType(System.Type[])"/> or reflection.
	/// </summary>
	/// <typeparam name="TTransportMessage">The transport message type the mapper handles.</typeparam>
	/// <typeparam name="TMapper">The mapper implementation type.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCloudEventMapper<
		TTransportMessage,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMapper>(
		this IServiceCollection services)
		where TMapper : class, ICloudEventMapper<TTransportMessage>
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton<ICloudEventMapper<TTransportMessage>, TMapper>();
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<ICloudEventMapperAdapter, CloudEventMapperAdapter<TTransportMessage>>());

		return services;
	}

	/// <summary>
	/// Registers an <see cref="ICloudEventMapper{TTransportMessage}"/> produced by a factory and its bridge
	/// adapter. Use this overload when the mapper needs custom construction (e.g. a captured serializer).
	/// </summary>
	/// <typeparam name="TTransportMessage">The transport message type the mapper handles.</typeparam>
	/// <param name="services">The service collection.</param>
	/// <param name="factory">Factory that produces the mapper instance.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddCloudEventMapper<TTransportMessage>(
		this IServiceCollection services,
		Func<IServiceProvider, ICloudEventMapper<TTransportMessage>> factory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(factory);

		services.TryAddSingleton(factory);
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<ICloudEventMapperAdapter, CloudEventMapperAdapter<TTransportMessage>>());

		return services;
	}
}

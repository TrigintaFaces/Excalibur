// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the serialization prerequisite check shared by every transport.
/// </summary>
public static class TransportSerializationServiceCollectionExtensions
{
	/// <summary>
	/// Makes the host fail at start-up, naming the remedy, when no payload serializer is registered.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// Called by every transport registration. Serialization itself is the application's choice, so a
	/// transport never registers a serializer -- it only states that it needs one, early and by name,
	/// instead of failing later with a container activation error. Registered through
	/// <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>,
	/// so composing several transports adds the check once.
	/// </remarks>
	public static IServiceCollection RequirePayloadSerializer(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IHostedService, PayloadSerializerPrerequisiteValidator>());
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, PayloadSerializerPrerequisiteValidator>());

		return services;
	}
}

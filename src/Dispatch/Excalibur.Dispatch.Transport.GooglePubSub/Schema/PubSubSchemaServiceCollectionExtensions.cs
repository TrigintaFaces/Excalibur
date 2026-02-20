// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Google Pub/Sub schema evolution utilities with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// Schema evolution allows messages to evolve their structure over time while maintaining
/// backward and forward compatibility. The Pub/Sub Schema Registry validates message schemas
/// and supports versioned revisions.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddGooglePubSubSchemaManager&lt;MySchemaManager&gt;();
/// </code>
/// </example>
public static class PubSubSchemaServiceCollectionExtensions
{
	/// <summary>
	/// Adds the specified <see cref="IPubSubSchemaManager"/> implementation to the service collection.
	/// </summary>
	/// <typeparam name="TImplementation">
	/// The concrete type implementing <see cref="IPubSubSchemaManager"/>.
	/// </typeparam>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Registers the schema manager as a singleton in the DI container. Consumers provide
	/// their own implementation of <see cref="IPubSubSchemaManager"/> backed by the
	/// Google Pub/Sub Schema Registry API.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddGooglePubSubSchemaManager<TImplementation>(
		this IServiceCollection services)
		where TImplementation : class, IPubSubSchemaManager
	{
		ArgumentNullException.ThrowIfNull(services);

		services.AddSingleton<IPubSubSchemaManager, TImplementation>();

		return services;
	}

	/// <summary>
	/// Adds the specified <see cref="IPubSubSchemaManager"/> implementation to the service collection
	/// using a factory delegate.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="factory">The factory delegate that creates the schema manager instance.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> or <paramref name="factory"/> is null.
	/// </exception>
	public static IServiceCollection AddGooglePubSubSchemaManager(
		this IServiceCollection services,
		Func<IServiceProvider, IPubSubSchemaManager> factory)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(factory);

		services.AddSingleton(factory);

		return services;
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering InMemory transport with the service collection.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the single entry point for InMemory transport configuration.
/// </para>
/// <para>
/// The InMemory transport is useful for testing, development, and scenarios where
/// external message brokers are not available.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddInMemoryTransport("test");
/// </code>
/// </example>
public static class InMemoryTransportServiceCollectionExtensions
{
	/// <summary>
	/// The default transport name when none is specified.
	/// </summary>
	public const string DefaultTransportName = "inmemory";

	/// <summary>
	/// Adds an InMemory transport with the specified name.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="name">The transport name for multi-transport routing.</param>
	/// <param name="configure">Optional transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary entry point for InMemory transport configuration.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Named transport for testing
	/// services.AddInMemoryTransport("test", options =>
	/// {
	///     options.ChannelCapacity = 1000;
	///     options.ProcessingBatchSize = 10;
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddInMemoryTransport(
		this IServiceCollection services,
		string name,
		Action<InMemoryTransportOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		// Create and configure options
		var options = new InMemoryTransportOptions { Name = name };
		configure?.Invoke(options);

		// Register the transport adapter in DI for direct injection
		_ = services.AddSingleton(sp =>
		{
			var logger = sp.GetRequiredService<ILogger<InMemoryTransportAdapter>>();
			return new InMemoryTransportAdapter(logger, options);
		});

		// Register as a keyed service for multi-transport scenarios
		_ = services.AddKeyedSingleton(name, (sp, _) =>
		{
			var logger = sp.GetRequiredService<ILogger<InMemoryTransportAdapter>>();
			return new InMemoryTransportAdapter(logger, options);
		});

		// Register factory in TransportRegistry for lifecycle management
		var registry = ServiceCollectionTransportExtensions.GetOrCreateTransportRegistry(services);
		registry.RegisterTransportFactory(
			name,
			InMemoryTransportAdapter.TransportTypeName,
			sp => sp.GetRequiredKeyedService<InMemoryTransportAdapter>(name));

		// Ensure hosted service lifecycle manager is registered (idempotent)
		_ = services.AddTransportAdapterLifecycle();

		return services;
	}

	/// <summary>
	/// Adds an InMemory transport with the default name.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional transport configuration action.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This overload uses the default transport name "inmemory".
	/// Use the named overload for multi-transport scenarios.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Single transport scenario with default name
	/// services.AddInMemoryTransport();
	/// </code>
	/// </example>
	public static IServiceCollection AddInMemoryTransport(
		this IServiceCollection services,
		Action<InMemoryTransportOptions>? configure = null)
	{
		return services.AddInMemoryTransport(DefaultTransportName, configure);
	}
}

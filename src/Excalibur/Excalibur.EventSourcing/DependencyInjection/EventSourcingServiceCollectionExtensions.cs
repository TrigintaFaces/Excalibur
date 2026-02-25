// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Snapshots;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Excalibur event sourcing services.
/// </summary>
public static class EventSourcingServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur event sourcing services to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the core event sourcing infrastructure with sensible defaults:
	/// <list type="bullet">
	/// <item><see cref="ISnapshotStrategy"/> - <see cref="NoSnapshotStrategy"/> (no snapshots by default)</item>
	/// </list>
	/// </para>
	/// <para>
	/// Use <see cref="AddExcaliburEventSourcing(IServiceCollection, Action{IEventSourcingBuilder})"/>
	/// to configure repositories, snapshot strategies, and other options.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddExcaliburEventSourcing(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// ADR-078: Register Dispatch primitives first (IDispatcher, IMessageBus, etc.)
		_ = services.AddDispatch();

		// Register default snapshot strategy (no snapshots)
		services.TryAddSingleton<ISnapshotStrategy>(NoSnapshotStrategy.Instance);

		return services;
	}

	/// <summary>
	/// Adds Excalibur event sourcing services with configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the event sourcing builder.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring Excalibur event sourcing. It allows you to
	/// register repositories, configure snapshot strategies, and set up event upcasting.
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddExcaliburEventSourcing(builder =>
	/// {
	///     // Register repositories with explicit factory
	///     builder.AddRepository&lt;OrderAggregate, Guid&gt;(id => new OrderAggregate(id));
	///
	///     // Or use static factory from IAggregateRoot&lt;TAggregate, TKey&gt;
	///     builder.AddRepository&lt;CustomerAggregate, Guid&gt;();
	///
	///     // Configure snapshot strategy
	///     builder.UseIntervalSnapshots(100);
	///
	///     // Or use composite strategy
	///     builder.UseCompositeSnapshotStrategy(s => s
	///         .AddIntervalStrategy(50)
	///         .AddTimeBasedStrategy(TimeSpan.FromMinutes(5))
	///         .RequireAll());
	///
	///     // Configure event upcasting
	///     builder.AddUpcastingPipeline(u => u
	///         .RegisterUpcaster&lt;OrderCreatedV1, OrderCreatedV2&gt;(new OrderCreatedV1ToV2())
	///         .EnableAutoUpcastOnReplay());
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddExcaliburEventSourcing(
		this IServiceCollection services,
		Action<IEventSourcingBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Ensure base services are registered
		_ = services.AddExcaliburEventSourcing();

		// Configure using the builder pattern
		var builder = new ExcaliburEventSourcingBuilder(services);
		configure(builder);

		return services;
	}

	/// <summary>
	/// Checks if Excalibur event sourcing services have been registered.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>True if event sourcing services are registered; otherwise false.</returns>
	public static bool HasExcaliburEventSourcing(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		return services.Any(s => s.ServiceType == typeof(ISnapshotStrategy));
	}
}

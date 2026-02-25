// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

using Excalibur.Outbox;
using Excalibur.Outbox.Outbox;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Excalibur outbox services.
/// </summary>
public static class OutboxServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur outbox services using a fluent builder configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The builder configuration action.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the primary entry point for configuring Excalibur Outbox following
	/// Microsoft-style fluent builder patterns. It provides a single discoverable API
	/// with fluent chaining for all configuration options.
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(connectionString, sql =>
	///     {
	///         sql.SchemaName("Outbox")
	///            .TableName("Messages");
	///     })
	///     .WithProcessing(p => p.BatchSize(100).PollingInterval(TimeSpan.FromSeconds(5)))
	///     .WithCleanup(c => c.EnableAutoCleanup(true).RetentionPeriod(TimeSpan.FromDays(14)))
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Minimal configuration with SQL Server
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(connectionString)
	///           .EnableBackgroundProcessing();
	/// });
	///
	/// // Full configuration with all options
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(connectionString, sql =>
	///     {
	///         sql.SchemaName("Messaging")
	///            .TableName("OutboxMessages")
	///            .CommandTimeout(TimeSpan.FromSeconds(60))
	///            .UseRowLocking(true);
	///     })
	///     .WithProcessing(processing =>
	///     {
	///         processing.BatchSize(200)
	///                   .PollingInterval(TimeSpan.FromSeconds(10))
	///                   .MaxRetryCount(5)
	///                   .RetryDelay(TimeSpan.FromMinutes(1))
	///                   .EnableParallelProcessing(4);
	///     })
	///     .WithCleanup(cleanup =>
	///     {
	///         cleanup.EnableAutoCleanup(true)
	///                .RetentionPeriod(TimeSpan.FromDays(30))
	///                .CleanupInterval(TimeSpan.FromHours(6));
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddExcaliburOutbox(
		this IServiceCollection services,
		Action<IOutboxBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register base infrastructure
		RegisterCoreServices(services);

		// Create mutable configuration and builder
		var config = new OutboxConfiguration();
		var builder = new OutboxBuilder(services, config);

		// Apply configuration
		configure(builder);

		// Build immutable options and register as singleton
		var options = config.ToOptions();
		services.TryAddSingleton(options);

		return services;
	}

	/// <summary>
	/// Adds Excalibur outbox services with preset-based options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="options">The preset-built outbox options.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This overload supports the new preset-based API for OutboxOptions.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Using a preset
	/// services.AddExcaliburOutbox(OutboxOptions.HighThroughput().Build());
	///
	/// // Using a preset with overrides
	/// services.AddExcaliburOutbox(
	///     OutboxOptions.HighThroughput()
	///         .WithBatchSize(2000)
	///         .WithProcessorId("worker-1")
	///         .Build());
	/// </code>
	/// </example>
	public static IServiceCollection AddExcaliburOutbox(
		this IServiceCollection services,
		OutboxOptions options)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(options);

		// Register base infrastructure
		RegisterCoreServices(services);

		// Register immutable options as singleton
		services.TryAddSingleton(options);

		// If background processing is enabled, register the hosted service
		if (options.EnableBackgroundProcessing)
		{
			_ = services.AddHostedService<OutboxBackgroundService>();
		}

		return services;
	}

	/// <summary>
	/// Adds Excalibur outbox services to the service collection with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the core outbox infrastructure with the Balanced preset defaults.
	/// </para>
	/// <para>
	/// <b>Prefer the fluent builder overload or preset-based overload:</b>
	/// <code>
	/// // Preset-based (recommended)
	/// services.AddExcaliburOutbox(OutboxOptions.Balanced().Build());
	///
	/// // Fluent builder
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(connectionString)
	///           .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddExcaliburOutbox(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		RegisterCoreServices(services);

		// Register default (Balanced) options
		var options = OutboxOptions.Balanced().Build();
		services.TryAddSingleton(options);

		return services;
	}

	/// <summary>
	/// Registers core outbox infrastructure services.
	/// </summary>
	private static void RegisterCoreServices(IServiceCollection services)
	{
		// ADR-078: Register Dispatch primitives first (IDispatcher, IMessageBus, etc.)
		_ = services.AddDispatch();
	}

	/// <summary>
	/// Checks if Excalibur outbox services have been registered.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>True if outbox services are registered; otherwise false.</returns>
	public static bool HasExcaliburOutbox(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.Any(s => s.ServiceType == typeof(OutboxOptions));
	}

	/// <summary>
	/// Registers the <see cref="OutboxBackgroundService"/> as a hosted service.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <remarks>
	/// <para>
	/// This registers the outbox background service that periodically polls for pending
	/// messages and publishes them. Use in conjunction with AddExcaliburOutbox() and
	/// an outbox store provider.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddOutboxHostedService(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddHostedService<OutboxBackgroundService>();

		return services;
	}

	/// <summary>
	/// Registers the <see cref="InboxService"/> as a hosted service.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to configure.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
	/// <remarks>
	/// <para>
	/// This registers the inbox background service that continuously processes messages
	/// from the inbox for deduplication. Use in conjunction with an inbox store provider.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddInboxHostedService(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		_ = services.AddHostedService<InboxService>();

		return services;
	}
}

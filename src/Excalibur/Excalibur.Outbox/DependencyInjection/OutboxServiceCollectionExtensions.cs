// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

using Excalibur.Outbox;
using Excalibur.Outbox.DependencyInjection;
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
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
	///            .SchemaName("Outbox")
	///            .TableName("Messages");
	///     })
	///     .WithProcessing(p => p.BatchSize(100).PollingInterval(TimeSpan.FromSeconds(5)))
	///     .WithCleanup(c => c.EnableAutoCleanup(true).RetentionPeriod(TimeSpan.FromDays(14)))
	///     .EnableBackgroundProcessing();
	/// }));
	/// </code>
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Minimal configuration with SQL Server
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql => sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;"))
	///           .EnableBackgroundProcessing();
	/// }));
	///
	/// // Full configuration with all options
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql =>
	///     {
	///         sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;")
	///            .SchemaName("Messaging")
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
	/// }));
	/// </code>
	/// </example>
	internal static IServiceCollection AddExcaliburOutbox(
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
	/// services.AddExcalibur(x => x.AddOutbox(OutboxOptions.HighThroughput().Build()));
	///
	/// // Using a preset with overrides
	/// services.AddExcalibur(x => x.AddOutbox(
	///     OutboxOptions.HighThroughput()
	///         .WithBatchSize(2000)
	///         .WithProcessorId("worker-1")
	///         .Build()));
	/// </code>
	/// </example>
	internal static IServiceCollection AddExcaliburOutbox(
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
	/// services.AddExcalibur(x => x.AddOutbox(OutboxOptions.Balanced().Build()));
	///
	/// // Fluent builder
	/// services.AddExcalibur(x => x.AddOutbox(outbox =>
	/// {
	///     outbox.UseSqlServer(sql => sql.ConnectionString("Server=.;Database=MyDb;Trusted_Connection=True;"))
	///           .EnableBackgroundProcessing();
	/// }));
	/// </code>
	/// </para>
	/// </remarks>
	internal static IServiceCollection AddExcaliburOutbox(this IServiceCollection services)
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

		// bd-x6rg45: fail loud at host start if the consumer forgot to pick an outbox store.
		services.TryAddEnumerable(ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, OutboxPrerequisiteValidator>());

		// Non-keyed IOutboxStore convenience alias: forwards to keyed "default" so consumers
		// can inject IOutboxStore directly without [FromKeyedServices("default")].
		services.TryAddSingleton<Excalibur.Dispatch.IOutboxStore>(sp =>
			sp.GetRequiredKeyedService<Excalibur.Dispatch.IOutboxStore>("default"));

		// Non-keyed IOutboxStoreAdmin convenience alias. Some providers (Elasticsearch) register
		// this as a separate keyed service; others implement it on the same class as IOutboxStore.
		// Try keyed "default" first, then fall back to casting the IOutboxStore.
		services.TryAddSingleton<Excalibur.Dispatch.IOutboxStoreAdmin>(sp =>
			sp.GetKeyedService<Excalibur.Dispatch.IOutboxStoreAdmin>("default")
			?? (Excalibur.Dispatch.IOutboxStoreAdmin)sp.GetRequiredKeyedService<Excalibur.Dispatch.IOutboxStore>("default"));

		// Register the real outbox processor + dispatcher with the outbox subsystem itself, so
		// IOutboxProcessor / IOutboxDispatcher are available whenever AddExcaliburOutbox is used —
		// NOT only when A3 audit (which registers a fail-fast DefaultOutboxDispatcher stub) is added.
		// These registrations were dropped when the implementations moved from Excalibur.Dispatch to
		// Excalibur.Outbox and were never restored, leaving OutboxJob, OutboxBackgroundService and
		// audited dispatch unable to resolve a real dispatcher.
		//
		// Lifetimes: OutboxProcessor is Transient because it carries per-instance state and is keyed
		// via Init(dispatcherId) — each background partition and each dispatcher needs its own. Its
		// dependencies are all root-resolvable, so capture by the Singleton dispatcher is safe.
		services.TryAddTransient<Excalibur.Dispatch.IOutboxProcessor, Excalibur.Dispatch.Delivery.OutboxProcessor>();

		// MessageOutbox (Singleton) must win even when A3 audit registered its fail-fast
		// DefaultOutboxDispatcher stub FIRST — TryAdd is order-sensitive, so the stub could otherwise
		// shadow the real dispatcher when audit is composed before the outbox. Remove only A3's stub
		// (identified by type, leaving any consumer-supplied dispatcher intact) and then TryAdd the
		// real implementation. A consumer override registered before AddOutbox(...) still wins because
		// the TryAdd below is a no-op when a non-stub IOutboxDispatcher is already present.
		RemoveDefaultOutboxDispatcherStub(services);
		services.TryAddSingleton<Excalibur.Dispatch.IOutboxDispatcher, Excalibur.Dispatch.Delivery.MessageOutbox>();

		// 6mygyz: bridge the consumer-facing parallel knob (OutboxOptions in this package) onto the degree
		// the OutboxProcessor actually reads (core OutboxDeliveryOptions.BatchProcessing.ParallelProcessingDegree).
		// They are separate option types in separate packages with no prior translation, so the advertised
		// EnableParallelProcessing(N) builder was inert. Options-composition resolves OutboxOptions at
		// configure-time (no stale snapshot); Excalibur.Outbox -> Excalibur.Dispatch core is a downward
		// reference (SA 18403). No-op unless the consumer enabled parallel processing on the outbox builder.
		_ = services.AddOptions<Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions>()
			.Configure<OutboxOptions>((delivery, outbox) =>
			{
				if (outbox.EnableParallelProcessing)
				{
					delivery.BatchProcessing.ParallelProcessingDegree = outbox.MaxDegreeOfParallelism;
				}
			});
	}

	// A3.Audit registers a fail-fast DefaultOutboxDispatcher via TryAdd so the audit pipeline is
	// composable without a concrete outbox. It is a placeholder that throws on dispatch. Referenced by
	// full name to avoid an Excalibur.Outbox -> Excalibur.A3 dependency; the contract is guarded by the
	// OutboxDispatcherRegistrationShould regression tests.
	private const string A3DefaultOutboxDispatcherFullName = "Excalibur.A3.Audit.Internal.DefaultOutboxDispatcher";

	private static void RemoveDefaultOutboxDispatcherStub(IServiceCollection services)
	{
		for (var i = services.Count - 1; i >= 0; i--)
		{
			var descriptor = services[i];
			if (descriptor.ServiceType == typeof(Excalibur.Dispatch.IOutboxDispatcher)
				&& descriptor.GetImplementationType()?.FullName == A3DefaultOutboxDispatcherFullName)
			{
				services.RemoveAt(i);
			}
		}
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

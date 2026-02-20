// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.Processing;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring Excalibur CDC processor services.
/// </summary>
public static class CdcServiceCollectionExtensions
{
	/// <summary>
	/// Adds Excalibur CDC processor services using a fluent builder configuration.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The builder configuration action.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This is the primary entry point for configuring Excalibur CDC following
	/// Microsoft-style fluent builder patterns. It provides a single discoverable API
	/// with fluent chaining for all configuration options.
	/// </para>
	/// <para>
	/// <b>Usage:</b>
	/// <code>
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseSqlServer(connectionString, sql =&gt;
	///     {
	///         sql.SchemaName("cdc")
	///            .StateTableName("CdcProcessingState")
	///            .PollingInterval(TimeSpan.FromSeconds(5))
	///            .BatchSize(100);
	///     })
	///     .TrackTable("dbo.Orders", table =&gt;
	///     {
	///         table.MapInsert&lt;OrderCreatedEvent&gt;()
	///              .MapUpdate&lt;OrderUpdatedEvent&gt;()
	///              .MapDelete&lt;OrderDeletedEvent&gt;();
	///     })
	///     .WithRecovery(r =&gt; r.MaxAttempts(5))
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Minimal configuration with SQL Server
	/// services.AddCdcProcessor(cdc =>
	/// {
	///     cdc.UseSqlServer(connectionString)
	///        .TrackTable("dbo.Orders", t => t.MapAll&lt;OrderChangedEvent&gt;())
	///        .EnableBackgroundProcessing();
	/// });
	///
	/// // Full configuration with recovery options
	/// services.AddCdcProcessor(cdc =>
	/// {
	///     cdc.UseSqlServer(connectionString, sql =>
	///     {
	///         sql.SchemaName("audit")
	///            .PollingInterval(TimeSpan.FromSeconds(10))
	///            .BatchSize(200);
	///     })
	///     .TrackTable("dbo.Orders", table =>
	///     {
	///         table.MapInsert&lt;OrderCreatedEvent&gt;()
	///              .MapUpdate&lt;OrderUpdatedEvent&gt;()
	///              .MapDelete&lt;OrderDeletedEvent&gt;();
	///     })
	///     .TrackTable("dbo.Customers", table =>
	///     {
	///         table.MapAll&lt;CustomerChangedEvent&gt;();
	///     })
	///     .WithRecovery(recovery =>
	///     {
	///         recovery.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest)
	///                 .MaxAttempts(5)
	///                 .AttemptDelay(TimeSpan.FromSeconds(30));
	///     })
	///     .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddCdcProcessor(
		this IServiceCollection services,
		Action<ICdcBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Register base infrastructure
		RegisterCoreServices(services);

		// Create options and builder
		var options = new CdcOptions();
		var builder = new CdcBuilder(services, options);

		// Apply configuration
		configure(builder);

		// Validate options
		options.Validate();

		// Register the configured options
		_ = services.AddOptions<CdcOptions>()
			.Configure(opt =>
			{
				opt.RecoveryStrategy = options.RecoveryStrategy;
				opt.OnPositionReset = options.OnPositionReset;
				opt.MaxRecoveryAttempts = options.MaxRecoveryAttempts;
				opt.RecoveryAttemptDelay = options.RecoveryAttemptDelay;
				opt.EnableStructuredLogging = options.EnableStructuredLogging;
				opt.EnableBackgroundProcessing = options.EnableBackgroundProcessing;

				foreach (var table in options.TrackedTables)
				{
					opt.TrackedTables.Add(table);
				}
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register the hosted service when background processing is enabled.
		// The ICdcBackgroundProcessor must be registered by the provider-specific extension
		// (e.g., UseSqlServer, UsePostgres) — the hosted service resolves it at runtime.
		if (options.EnableBackgroundProcessing)
		{
			_ = services.AddOptions<CdcProcessingOptions>()
				.ValidateDataAnnotations()
				.ValidateOnStart();
			services.TryAddEnumerable(
				ServiceDescriptor.Singleton<IValidateOptions<CdcProcessingOptions>, CdcProcessingOptionsValidator>());
			services.TryAddEnumerable(
				ServiceDescriptor.Singleton<Hosting.IHostedService, CdcProcessingHostedService>());
		}

		return services;
	}

	/// <summary>
	/// Adds Excalibur CDC processor services with default options.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// This method registers the core CDC infrastructure with sensible defaults.
	/// </para>
	/// <para>
	/// <b>Prefer the fluent builder overload:</b>
	/// <code>
	/// services.AddCdcProcessor(cdc =>
	/// {
	///     cdc.UseSqlServer(connectionString)
	///        .TrackTable("dbo.Orders", t => t.MapAll&lt;OrderChangedEvent&gt;())
	///        .EnableBackgroundProcessing();
	/// });
	/// </code>
	/// </para>
	/// </remarks>
	public static IServiceCollection AddCdcProcessor(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		RegisterCoreServices(services);

		return services;
	}

	/// <summary>
	/// Checks if Excalibur CDC processor services have been registered.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>True if CDC services are registered; otherwise false.</returns>
	public static bool HasCdcProcessor(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		return services.Any(s =>
			s.ServiceType == typeof(IConfigureOptions<CdcOptions>) ||
			s.ImplementationType == typeof(DefaultCdcOptionsSetup));
	}

	/// <summary>
	/// Registers core CDC infrastructure services.
	/// </summary>
	private static void RegisterCoreServices(IServiceCollection services)
	{
		_ = services.AddOptions<CdcOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<CdcOptions>, DefaultCdcOptionsSetup>());
	}

	/// <summary>
	/// Default options setup for CdcOptions.
	/// </summary>
	[System.Diagnostics.CodeAnalysis.SuppressMessage(
		"Performance",
		"CA1812:AvoidUninstantiatedInternalClasses",
		Justification = "Instantiated by the options infrastructure.")]
	private sealed class DefaultCdcOptionsSetup : IConfigureOptions<CdcOptions>
	{
		public void Configure(CdcOptions options)
		{
			// Defaults are already set in CdcOptions constructor
			// This class exists to enable proper options pattern integration
		}
	}
}

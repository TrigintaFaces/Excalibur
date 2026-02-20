// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc.InMemory;

/// <summary>
/// Extension methods for configuring in-memory CDC provider on <see cref="ICdcBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="ICdcBuilder"/> interface.
/// </para>
/// </remarks>
public static class CdcBuilderInMemoryExtensions
{
	/// <summary>
	/// Configures the CDC processor to use an in-memory provider for testing.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="configure">Optional action to configure in-memory-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This provider is designed for testing scenarios where a real database
	/// connection is not available or not desired. It registers the
	/// <see cref="InMemoryCdcProcessor"/> and <see cref="InMemoryCdcStore"/>.
	/// </para>
	/// <para>
	/// The in-memory store is registered as a singleton to allow test code to
	/// inject simulated changes via <see cref="IInMemoryCdcStore"/>.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Basic usage for testing
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseInMemory()
	///        .TrackTable("dbo.Orders", table =&gt;
	///        {
	///            table.MapInsert&lt;OrderCreatedEvent&gt;()
	///                 .MapUpdate&lt;OrderUpdatedEvent&gt;()
	///                 .MapDelete&lt;OrderDeletedEvent&gt;();
	///        });
	/// });
	///
	/// // Later in test code, inject changes:
	/// var store = services.GetRequiredService&lt;IInMemoryCdcStore&gt;();
	/// store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", new CdcDataChange
	/// {
	///     ColumnName = "Id",
	///     NewValue = 1
	/// }));
	/// </code>
	/// </example>
	public static ICdcBuilder UseInMemory(
		this ICdcBuilder builder,
		Action<IInMemoryCdcBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Create and configure in-memory options
		var memOptions = new InMemoryCdcOptions();

		if (configure is not null)
		{
			var memBuilder = new InMemoryCdcBuilder(memOptions);
			configure(memBuilder);
		}

		// Validate options
		memOptions.Validate();

		// Register in-memory CDC options
		_ = builder.Services.Configure<InMemoryCdcOptions>(opt =>
		{
			opt.ProcessorId = memOptions.ProcessorId;
			opt.BatchSize = memOptions.BatchSize;
			opt.AutoFlush = memOptions.AutoFlush;
			opt.PreserveHistory = memOptions.PreserveHistory;
		});

		// Register in-memory CDC store as singleton (so tests can inject changes)
		builder.Services.TryAddSingleton<IInMemoryCdcStore>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<InMemoryCdcOptions>>();
			return new InMemoryCdcStore(options);
		});

		// Register in-memory CDC processor
		builder.Services.TryAddSingleton<IInMemoryCdcProcessor>(sp =>
		{
			var store = sp.GetRequiredService<IInMemoryCdcStore>();
			var options = sp.GetRequiredService<IOptions<InMemoryCdcOptions>>();
			var logger = sp.GetRequiredService<ILogger<InMemoryCdcProcessor>>();
			return new InMemoryCdcProcessor(store, options, logger);
		});

		return builder;
	}

	/// <summary>
	/// Configures the CDC processor to use an in-memory provider with a pre-configured store.
	/// </summary>
	/// <param name="builder">The CDC builder.</param>
	/// <param name="store">The pre-configured in-memory store instance.</param>
	/// <param name="configure">Optional action to configure in-memory-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> or <paramref name="store"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this overload when you want to share a pre-configured store instance
	/// across multiple tests or provide a store with pre-populated changes.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// var store = new InMemoryCdcStore();
	/// store.AddChange(InMemoryCdcChange.Insert("dbo.Orders", ...));
	///
	/// services.AddCdcProcessor(cdc =&gt;
	/// {
	///     cdc.UseInMemory(store, mem =&gt;
	///     {
	///         mem.BatchSize(10);
	///     });
	/// });
	/// </code>
	/// </example>
	public static ICdcBuilder UseInMemory(
		this ICdcBuilder builder,
		IInMemoryCdcStore store,
		Action<IInMemoryCdcBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(store);

		// Create and configure in-memory options
		var memOptions = new InMemoryCdcOptions();

		if (configure is not null)
		{
			var memBuilder = new InMemoryCdcBuilder(memOptions);
			configure(memBuilder);
		}

		// Validate options
		memOptions.Validate();

		// Register in-memory CDC options
		_ = builder.Services.Configure<InMemoryCdcOptions>(opt =>
		{
			opt.ProcessorId = memOptions.ProcessorId;
			opt.BatchSize = memOptions.BatchSize;
			opt.AutoFlush = memOptions.AutoFlush;
			opt.PreserveHistory = memOptions.PreserveHistory;
		});

		// Register the provided store instance
		builder.Services.TryAddSingleton(store);

		// Register in-memory CDC processor
		builder.Services.TryAddSingleton<IInMemoryCdcProcessor>(sp =>
		{
			var options = sp.GetRequiredService<IOptions<InMemoryCdcOptions>>();
			var logger = sp.GetRequiredService<ILogger<InMemoryCdcProcessor>>();
			return new InMemoryCdcProcessor(store, options, logger);
		});

		return builder;
	}
}

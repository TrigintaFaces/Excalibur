// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Outbox;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Data.InMemory;

/// <summary>
/// Extension methods for configuring in-memory provider on <see cref="IOutboxBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide fluent provider selection by adding
/// provider-specific configuration to the core <see cref="IOutboxBuilder"/> interface.
/// </para>
/// <para>
/// The in-memory provider is intended for testing and development scenarios only.
/// Data is not persisted and is lost when the application restarts.
/// </para>
/// </remarks>
public static class OutboxBuilderInMemoryExtensions
{
	/// <summary>
	/// Configures the outbox to use in-memory storage.
	/// </summary>
	/// <param name="builder">The outbox builder.</param>
	/// <param name="configure">Optional action to configure in-memory-specific options.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="builder"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This is the primary method for configuring in-memory as the outbox storage provider.
	/// It registers the <see cref="InMemoryOutboxStore"/> and related services.
	/// </para>
	/// <para>
	/// <strong>Warning:</strong> The in-memory store is not suitable for production use.
	/// Use it only for unit tests, integration tests, or local development.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // Simple test configuration
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UseInMemory()
	///           .WithProcessing(p => p.BatchSize(10));
	/// });
	///
	/// // With custom limits
	/// services.AddExcaliburOutbox(outbox =>
	/// {
	///     outbox.UseInMemory(inmemory =>
	///     {
	///         inmemory.MaxMessages(100)
	///                 .RetentionPeriod(TimeSpan.FromMinutes(5));
	///     });
	/// });
	/// </code>
	/// </example>
	public static IOutboxBuilder UseInMemory(
		this IOutboxBuilder builder,
		Action<IInMemoryOutboxBuilder>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Create and configure in-memory options
		var inmemoryOptions = new InMemoryOutboxOptions();

		if (configure is not null)
		{
			var inmemoryBuilder = new InMemoryOutboxBuilder(inmemoryOptions);
			configure(inmemoryBuilder);
		}

		// Register in-memory options
		_ = builder.Services.AddOptions<InMemoryOutboxOptions>()
			.Configure(opt =>
			{
				opt.MaxMessages = inmemoryOptions.MaxMessages;
				opt.DefaultRetentionPeriod = inmemoryOptions.DefaultRetentionPeriod;
			})
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// Register in-memory outbox store
		builder.Services.TryAddSingleton<InMemoryOutboxStore>();
		builder.Services.TryAddSingleton<IOutboxStore>(sp => sp.GetRequiredService<InMemoryOutboxStore>());

		return builder;
	}
}

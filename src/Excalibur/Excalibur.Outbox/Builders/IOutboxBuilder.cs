// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Fluent builder interface for configuring Excalibur Outbox.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Microsoft-style fluent builder pattern.
/// It provides the single entry point for Outbox configuration.
/// </para>
/// <para>
/// All methods return <c>this</c> for method chaining, enabling a fluent configuration experience.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddExcaliburOutbox(outbox =>
/// {
///     outbox.UseSqlServer(connectionString, sql =>
///     {
///         sql.SchemaName("Outbox")
///            .TableName("Messages")
///            .CommandTimeout(TimeSpan.FromSeconds(30))
///            .UseRowLocking(true);
///     })
///     .WithProcessing(processing =>
///     {
///         processing.BatchSize(100)
///                   .PollingInterval(TimeSpan.FromSeconds(5))
///                   .MaxRetryCount(5)
///                   .RetryDelay(TimeSpan.FromMinutes(1));
///     })
///     .WithCleanup(cleanup =>
///     {
///         cleanup.EnableAutoCleanup(true)
///                .RetentionPeriod(TimeSpan.FromDays(14));
///     })
///     .EnableBackgroundProcessing();
/// });
/// </code>
/// </example>
public interface IOutboxBuilder
{
	/// <summary>
	/// Gets the service collection being configured.
	/// </summary>
	/// <value>The <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>.</value>
	Microsoft.Extensions.DependencyInjection.IServiceCollection Services { get; }

	/// <summary>
	/// Configures outbox processing settings using a fluent builder.
	/// </summary>
	/// <param name="configure">The processing configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure how messages are processed from the outbox,
	/// including batch sizes, polling intervals, and retry behavior.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// outbox.WithProcessing(processing =>
	/// {
	///     processing.BatchSize(100)
	///               .PollingInterval(TimeSpan.FromSeconds(5))
	///               .MaxRetryCount(5)
	///               .RetryDelay(TimeSpan.FromMinutes(1))
	///               .ProcessorId("instance-1")
	///               .EnableParallelProcessing(4);
	/// });
	/// </code>
	/// </example>
	IOutboxBuilder WithProcessing(Action<IOutboxProcessingBuilder> configure);

	/// <summary>
	/// Configures outbox cleanup settings using a fluent builder.
	/// </summary>
	/// <param name="configure">The cleanup configuration action.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="configure"/> is null.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to configure automatic cleanup of processed messages,
	/// including retention periods and cleanup intervals.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// outbox.WithCleanup(cleanup =>
	/// {
	///     cleanup.EnableAutoCleanup(true)
	///            .RetentionPeriod(TimeSpan.FromDays(14))
	///            .CleanupInterval(TimeSpan.FromHours(1));
	/// });
	/// </code>
	/// </example>
	IOutboxBuilder WithCleanup(Action<IOutboxCleanupBuilder> configure);

	/// <summary>
	/// Enables background processing of outbox messages.
	/// </summary>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, a hosted service will be registered that periodically
	/// polls the outbox and publishes pending messages to their configured transports.
	/// </para>
	/// <para>
	/// For serverless scenarios where background services are not suitable,
	/// omit this call and use <see cref="Outbox.IOutboxProcessor"/> directly.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// outbox.UseSqlServer(connectionString)
	///       .EnableBackgroundProcessing();
	/// </code>
	/// </example>
	IOutboxBuilder EnableBackgroundProcessing();
}

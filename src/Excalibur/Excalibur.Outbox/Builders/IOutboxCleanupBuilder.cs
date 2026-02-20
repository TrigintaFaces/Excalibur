// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox;

/// <summary>
/// Fluent builder interface for configuring outbox cleanup settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures how processed messages are cleaned up from the outbox store.
/// All methods return <c>this</c> for method chaining.
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
public interface IOutboxCleanupBuilder
{
	/// <summary>
	/// Enables or disables automatic cleanup of processed messages.
	/// </summary>
	/// <param name="enable">True to enable automatic cleanup; false to disable.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// When enabled, successfully sent messages older than the retention period
	/// will be automatically removed. Default is true.
	/// </para>
	/// </remarks>
	IOutboxCleanupBuilder EnableAutoCleanup(bool enable = true);

	/// <summary>
	/// Sets the retention period for successfully sent messages.
	/// </summary>
	/// <param name="period">The retention period. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="period"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Messages older than this period will be removed during cleanup cycles.
	/// Default is 7 days.
	/// </para>
	/// </remarks>
	IOutboxCleanupBuilder RetentionPeriod(TimeSpan period);

	/// <summary>
	/// Sets the interval between cleanup cycles.
	/// </summary>
	/// <param name="interval">The cleanup interval. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="interval"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Shorter intervals keep the table size smaller but increase database load.
	/// Default is 1 hour.
	/// </para>
	/// </remarks>
	IOutboxCleanupBuilder CleanupInterval(TimeSpan interval);
}

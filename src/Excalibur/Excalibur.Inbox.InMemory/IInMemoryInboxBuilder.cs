// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Inbox.InMemory;

/// <summary>
/// Fluent builder interface for configuring in-memory inbox settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures in-memory-specific options such as capacity limits,
/// cleanup behavior, and retention periods for testing and development scenarios.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// inbox.UseInMemory(inmemory =>
/// {
///     inmemory.MaxEntries(5000)
///             .RetentionPeriod(TimeSpan.FromHours(1))
///             .EnableAutomaticCleanup(true)
///             .CleanupInterval(TimeSpan.FromMinutes(2));
/// });
/// </code>
/// </example>
public interface IInMemoryInboxBuilder
{
	/// <summary>
	/// Sets the maximum number of entries to keep in the in-memory store.
	/// </summary>
	/// <param name="count">The maximum entry count. Zero means unlimited.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 10000. When the limit is reached, older processed entries are evicted first.
	/// </para>
	/// </remarks>
	IInMemoryInboxBuilder MaxEntries(int count);

	/// <summary>
	/// Sets the retention period for processed entries.
	/// </summary>
	/// <param name="period">The retention period. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="period"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 7 days. After this period, processed entries are eligible for cleanup.
	/// </para>
	/// </remarks>
	IInMemoryInboxBuilder RetentionPeriod(TimeSpan period);

	/// <summary>
	/// Enables or disables automatic cleanup of old entries.
	/// </summary>
	/// <param name="enable">
	/// <see langword="true"/> to enable automatic cleanup; <see langword="false"/> to disable.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is <see langword="true"/>. When enabled, a background task periodically
	/// removes entries older than <see cref="RetentionPeriod"/>.
	/// </para>
	/// </remarks>
	IInMemoryInboxBuilder EnableAutomaticCleanup(bool enable = true);

	/// <summary>
	/// Sets the interval between automatic cleanup runs.
	/// </summary>
	/// <param name="interval">The cleanup interval. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="interval"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 5 minutes. Only applies when automatic cleanup is enabled.
	/// </para>
	/// </remarks>
	IInMemoryInboxBuilder CleanupInterval(TimeSpan interval);
}

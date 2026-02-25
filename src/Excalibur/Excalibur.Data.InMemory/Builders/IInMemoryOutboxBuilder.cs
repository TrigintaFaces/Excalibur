// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.InMemory;

/// <summary>
/// Fluent builder interface for configuring in-memory outbox settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures in-memory-specific options such as capacity limits
/// and retention periods for testing and development scenarios.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// outbox.UseInMemory(inmemory =>
/// {
///     inmemory.MaxMessages(1000)
///             .RetentionPeriod(TimeSpan.FromHours(1));
/// });
/// </code>
/// </example>
public interface IInMemoryOutboxBuilder
{
	/// <summary>
	/// Sets the maximum number of messages to retain in the in-memory store.
	/// </summary>
	/// <param name="count">The maximum message count. Zero means unlimited.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is negative.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 10000. When the limit is reached, older sent messages are evicted first.
	/// </para>
	/// </remarks>
	IInMemoryOutboxBuilder MaxMessages(int count);

	/// <summary>
	/// Sets the default retention period for sent messages.
	/// </summary>
	/// <param name="period">The retention period. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="period"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Default is 7 days. After this period, sent messages are eligible for cleanup.
	/// </para>
	/// </remarks>
	IInMemoryOutboxBuilder RetentionPeriod(TimeSpan period);
}

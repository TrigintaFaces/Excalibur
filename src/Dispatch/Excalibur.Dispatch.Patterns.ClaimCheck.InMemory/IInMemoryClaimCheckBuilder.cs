// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

/// <summary>
/// Fluent builder interface for configuring in-memory claim check settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures in-memory-specific options such as payload thresholds,
/// cleanup behavior, and compression for testing and development scenarios.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddInMemoryClaimCheck(inmemory =>
/// {
///     inmemory.PayloadThreshold(128 * 1024)
///             .EnableCompression(true)
///             .DefaultTtl(TimeSpan.FromDays(3))
///             .EnableCleanup(true);
/// });
/// </code>
/// </example>
public interface IInMemoryClaimCheckBuilder
{
	/// <summary>
	/// Sets the threshold in bytes above which payloads use claim check storage.
	/// </summary>
	/// <param name="thresholdBytes">The threshold in bytes. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="thresholdBytes"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>Default is 256KB (262144 bytes).</para>
	/// </remarks>
	IInMemoryClaimCheckBuilder PayloadThreshold(long thresholdBytes);

	/// <summary>
	/// Sets the default time-to-live for stored payloads.
	/// </summary>
	/// <param name="ttl">The time-to-live. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="ttl"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>Default is 7 days.</para>
	/// </remarks>
	IInMemoryClaimCheckBuilder DefaultTtl(TimeSpan ttl);

	/// <summary>
	/// Enables or disables compression for stored payloads.
	/// </summary>
	/// <param name="enable">
	/// <see langword="true"/> to enable compression; <see langword="false"/> to disable.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>Default is <see langword="false"/>.</para>
	/// </remarks>
	IInMemoryClaimCheckBuilder EnableCompression(bool enable = true);

	/// <summary>
	/// Enables or disables automatic cleanup of expired payloads via background service.
	/// </summary>
	/// <param name="enable">
	/// <see langword="true"/> to enable cleanup; <see langword="false"/> to disable.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Default is <see langword="true"/>. Set to <see langword="false"/> for testing
	/// or when cleanup is managed externally.
	/// </para>
	/// </remarks>
	IInMemoryClaimCheckBuilder EnableCleanup(bool enable = true);

	/// <summary>
	/// Enables or disables checksum validation for payload integrity.
	/// </summary>
	/// <param name="enable">
	/// <see langword="true"/> to enable checksum validation; <see langword="false"/> to disable.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>Default is <see langword="true"/>.</para>
	/// </remarks>
	IInMemoryClaimCheckBuilder ValidateChecksum(bool enable = true);
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.InMemory;

/// <summary>
/// Fluent builder interface for configuring in-memory persistence settings.
/// </summary>
/// <remarks>
/// <para>
/// This builder configures in-memory-specific options such as capacity limits,
/// storage behavior, and observability for testing and development scenarios.
/// All methods return <c>this</c> for method chaining.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// services.AddExcaliburInMemory(inmemory =>
/// {
///     inmemory.MaxItemsPerCollection(5000)
///             .EnableDetailedLogging(true)
///             .PersistToDisk("./data/test-store.json");
/// });
/// </code>
/// </example>
public interface IInMemoryDataBuilder
{
	/// <summary>
	/// Sets the maximum number of items allowed per collection.
	/// </summary>
	/// <param name="count">The maximum item count. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="count"/> is not positive.
	/// </exception>
	/// <remarks>
	/// <para>Default is 10000.</para>
	/// </remarks>
	IInMemoryDataBuilder MaxItemsPerCollection(int count);

	/// <summary>
	/// Enables or disables detailed logging of operations.
	/// </summary>
	/// <param name="enable">
	/// <see langword="true"/> to enable detailed logging; <see langword="false"/> to disable.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>Default is <see langword="false"/>.</para>
	/// </remarks>
	IInMemoryDataBuilder EnableDetailedLogging(bool enable = true);

	/// <summary>
	/// Enables or disables metrics collection.
	/// </summary>
	/// <param name="enable">
	/// <see langword="true"/> to enable metrics; <see langword="false"/> to disable.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>Default is <see langword="false"/>.</para>
	/// </remarks>
	IInMemoryDataBuilder EnableMetrics(bool enable = true);

	/// <summary>
	/// Enables persistence to disk with the specified file path.
	/// </summary>
	/// <param name="filePath">The file path for persistence. Must not be null or whitespace.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="filePath"/> is null or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// When enabled, data is persisted to disk on dispose and loaded on initialization.
	/// Useful for maintaining state across restarts during development.
	/// </para>
	/// </remarks>
	IInMemoryDataBuilder PersistToDisk(string filePath);

	/// <summary>
	/// Sets the provider as read-only.
	/// </summary>
	/// <param name="readOnly">
	/// <see langword="true"/> to make the provider read-only; <see langword="false"/> for read-write.
	/// </param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>Default is <see langword="false"/>.</para>
	/// </remarks>
	IInMemoryDataBuilder ReadOnly(bool readOnly = true);
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Defines cache configuration methods for a Schema Registry builder.
/// </summary>
public interface ISchemaRegistryCacheBuilder
{
	/// <summary>
	/// Enables or disables local schema caching.
	/// </summary>
	/// <param name="enable">Whether to cache schemas. Default is true.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Caching improves performance by avoiding repeated network calls to the registry.
	/// </para>
	/// </remarks>
	IConfluentSchemaRegistryBuilder CacheSchemas(bool enable = true);

	/// <summary>
	/// Sets the maximum number of schemas to cache locally.
	/// </summary>
	/// <param name="capacity">The cache capacity. Default is 1000.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="capacity"/> is not positive.
	/// </exception>
	IConfluentSchemaRegistryBuilder CacheCapacity(int capacity);

	/// <summary>
	/// Sets the timeout for Schema Registry HTTP requests.
	/// </summary>
	/// <param name="timeout">The request timeout. Default is 30 seconds.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="timeout"/> is not positive.
	/// </exception>
	IConfluentSchemaRegistryBuilder RequestTimeout(TimeSpan timeout);
}

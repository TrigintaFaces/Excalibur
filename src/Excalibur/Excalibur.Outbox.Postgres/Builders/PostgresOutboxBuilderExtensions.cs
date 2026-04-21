// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Extension methods for <see cref="IPostgresOutboxBuilder"/>.
/// </summary>
public static class PostgresOutboxBuilderExtensions
{
	/// <summary>
	/// Sets the command timeout for Postgres operations.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="timeout">The command timeout. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IPostgresOutboxBuilder CommandTimeout(this IPostgresOutboxBuilder builder, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((PostgresOutboxBuilder)builder).CommandTimeout(timeout);
	}

	/// <summary>
	/// Sets the reservation timeout for message processing.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="timeout">The reservation timeout. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IPostgresOutboxBuilder ReservationTimeout(this IPostgresOutboxBuilder builder, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((PostgresOutboxBuilder)builder).ReservationTimeout(timeout);
	}

	/// <summary>
	/// Sets the maximum number of delivery attempts before moving to dead letter.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="maxAttempts">The maximum attempts. Must be greater than 0.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IPostgresOutboxBuilder MaxAttempts(this IPostgresOutboxBuilder builder, int maxAttempts)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((PostgresOutboxBuilder)builder).MaxAttempts(maxAttempts);
	}
}

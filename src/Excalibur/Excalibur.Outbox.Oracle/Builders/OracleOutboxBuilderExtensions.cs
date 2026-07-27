// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Extension methods for <see cref="IOracleOutboxBuilder"/>.
/// </summary>
public static class OracleOutboxBuilderExtensions
{
	/// <summary>
	/// Sets the command timeout for Oracle operations.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="timeout">The command timeout. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IOracleOutboxBuilder CommandTimeout(this IOracleOutboxBuilder builder, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((OracleOutboxBuilder)builder).CommandTimeout(timeout);
	}

	/// <summary>
	/// Sets the reservation timeout for message processing.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="timeout">The reservation timeout. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IOracleOutboxBuilder ReservationTimeout(this IOracleOutboxBuilder builder, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((OracleOutboxBuilder)builder).ReservationTimeout(timeout);
	}

	/// <summary>
	/// Sets the maximum number of delivery attempts before moving to dead letter.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="maxAttempts">The maximum attempts. Must be greater than 0.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static IOracleOutboxBuilder MaxAttempts(this IOracleOutboxBuilder builder, int maxAttempts)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((OracleOutboxBuilder)builder).MaxAttempts(maxAttempts);
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Outbox.SqlServer;

/// <summary>
/// Extension methods for <see cref="ISqlServerOutboxBuilder"/>.
/// </summary>
public static class SqlServerOutboxBuilderExtensions
{
	/// <summary>
	/// Sets the command timeout for SQL operations.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="timeout">The command timeout. Must be positive.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ISqlServerOutboxBuilder CommandTimeout(this ISqlServerOutboxBuilder builder, TimeSpan timeout)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((SqlServerOutboxBuilder)builder).CommandTimeout(timeout);
	}

	/// <summary>
	/// Enables or disables row-level locking for concurrent access.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="enable">True to enable row-level locking.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ISqlServerOutboxBuilder UseRowLocking(this ISqlServerOutboxBuilder builder, bool enable = true)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((SqlServerOutboxBuilder)builder).UseRowLocking(enable);
	}

	/// <summary>
	/// Sets the default batch size for retrieving messages.
	/// </summary>
	/// <param name="builder">The builder.</param>
	/// <param name="size">The batch size. Must be greater than 0.</param>
	/// <returns>The builder for fluent chaining.</returns>
	public static ISqlServerOutboxBuilder DefaultBatchSize(this ISqlServerOutboxBuilder builder, int size)
	{
		ArgumentNullException.ThrowIfNull(builder);
		return ((SqlServerOutboxBuilder)builder).DefaultBatchSize(size);
	}
}

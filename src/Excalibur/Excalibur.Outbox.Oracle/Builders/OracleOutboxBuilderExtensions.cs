// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Extension methods for <see cref="IOracleOutboxBuilder"/>.
/// </summary>
public static class OracleOutboxBuilderExtensions
{
	/// <summary>Transfers a fresh, closed, exclusive worker connection on every invocation.</summary>
	/// <param name="builder">The Oracle provider builder.</param>
	/// <param name="factory">Creates a new, closed, un-enlisted connection targeting the staging database. The framework owns and disposes returned connections; never return a borrowed, cached or DI-owned connection.</param>
	/// <returns>The builder.</returns>
	/// <remarks>The callback scope survives connection disposal. Callback failures retain ownership of objects it never returned. Explicit worker configuration survives later primary selection and overrides string-derived worker defaults.</remarks>
	public static IOracleOutboxBuilder WorkerConnectionFactory(this IOracleOutboxBuilder builder,
		Func<IServiceProvider, CancellationToken, ValueTask<OracleConnection>> factory)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(factory);
		if (builder is not OracleOutboxBuilder oracleBuilder)
		{
			throw new ArgumentException("WorkerConnectionFactory requires the framework UseOracle provider builder.", nameof(builder));
		}
		oracleBuilder.SetWorkerFactory(factory);
		return builder;
	}

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

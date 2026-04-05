// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Defines the contract for configuring SQL Server CDC database-specific options including
/// database naming, connection identifiers, capture instances, and missing handler behavior.
/// </summary>
public interface ISqlServerCdcDatabaseBuilder
{
	/// <summary>
	/// Sets the database name for CDC processing.
	/// </summary>
	/// <param name="name">The database name.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="name"/> is null or whitespace.
	/// </exception>
	ISqlServerCdcBuilder DatabaseName(string name);

	/// <summary>
	/// Sets the unique identifier for the CDC source database connection.
	/// </summary>
	/// <param name="identifier">The connection identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="identifier"/> is null or whitespace.
	/// </exception>
	ISqlServerCdcBuilder DatabaseConnectionIdentifier(string identifier);

	/// <summary>
	/// Sets the unique identifier for the state store database connection.
	/// </summary>
	/// <param name="identifier">The connection identifier.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="identifier"/> is null or whitespace.
	/// </exception>
	ISqlServerCdcBuilder StateConnectionIdentifier(string identifier);

	/// <summary>
	/// Sets the CDC capture instances to process.
	/// </summary>
	/// <param name="captureInstances">The capture instance names.</param>
	/// <returns>The builder for fluent chaining.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="captureInstances"/> is null.
	/// </exception>
	ISqlServerCdcBuilder CaptureInstances(params string[] captureInstances);

	/// <summary>
	/// Sets whether processing should stop when a table handler is missing.
	/// </summary>
	/// <param name="stop"><see langword="true"/> to stop on missing handlers; <see langword="false"/> to skip.</param>
	/// <returns>The builder for fluent chaining.</returns>
	ISqlServerCdcBuilder StopOnMissingTableHandler(bool stop);
}

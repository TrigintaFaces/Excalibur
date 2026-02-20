// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

namespace Excalibur.EventSourcing.Postgres;

/// <summary>
/// Configuration options for the Postgres projection store.
/// </summary>
/// <remarks>
/// <para>
/// Supports connection string, <see cref="Npgsql.NpgsqlDataSource"/>, or DI-resolved data source patterns.
/// The projection store uses JSONB for efficient JSON querying with Postgres-native operators.
/// </para>
/// </remarks>
public sealed class PostgresProjectionStoreOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the table name for projections.
	/// </summary>
	/// <remarks>
	/// When <see langword="null"/>, defaults to the projection type name converted to snake_case.
	/// </remarks>
	public string? TableName { get; set; }

	/// <summary>
	/// Gets or sets the JSON serializer options for projection data.
	/// </summary>
	/// <remarks>
	/// When <see langword="null"/>, defaults to camelCase naming with no indentation.
	/// </remarks>
	public JsonSerializerOptions? JsonSerializerOptions { get; set; }

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException(
				"A connection string is required for the Postgres projection store. " +
				"Set the ConnectionString property or use the overload that accepts a connection string directly.");
		}
	}
}

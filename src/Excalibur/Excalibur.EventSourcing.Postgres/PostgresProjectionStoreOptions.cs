// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;
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
	[Required]
	public string? ConnectionString { get; set; }

	/// <summary>
	/// Gets or sets the table name for projections.
	/// </summary>
	/// <remarks>
	/// When <see langword="null"/>, defaults to the projection type name converted to snake_case.
	/// </remarks>
	public string? TableName { get; set; }

	/// <summary>
	/// Gets or sets the schema holding the projection table.
	/// </summary>
	/// <value>The schema name. Defaults to <c>public</c>.</value>
	/// <remarks>
	/// <para>
	/// <b>Honoured by every statement the store runs, which is the only form of this setting worth
	/// having.</b> The store previously addressed the table unqualified, so it resolved through the
	/// connection's <c>search_path</c> — a setting the store neither configured nor read, which made two
	/// deployments with the same table name under different search paths address different tables while
	/// being indistinguishable to this framework.
	/// </para>
	/// <para>
	/// <b>Case is significant.</b> The store quotes both identifiers, and Postgres preserves the case
	/// inside double quotes, so <c>public</c> and <c>Public</c> name two different schemas. The value is
	/// used exactly as written: only letters, digits and underscores are accepted.
	/// </para>
	/// </remarks>
	public string SchemaName { get; set; } = "public";

	/// <summary>Gets or sets which apply path owns this destination.</summary>
	/// <value>
	/// <see langword="null"/> by default, meaning NOT STATED -- the kind is then derived from the
	/// projection's registered mode, and falls back to
	/// <see cref="ProjectionDestinationKind.Positioned"/> only when nothing is registered. Set it
	/// explicitly to override the derivation; an explicit value always wins.
	/// </value>
	/// <remarks>
	/// NULLABLE DELIBERATELY. A non-nullable value here could not distinguish "the consumer chose
	/// Positioned" from "nobody said", so every producer was forced to assert something it had not been
	/// told -- and the apply path was then declarable twice, here and on the projection's mode, with
	/// nothing reconciling the two. The third state is what lets the mode be the single declaration.
	/// </remarks>
	public ProjectionDestinationKind? DestinationKind { get; set; }

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

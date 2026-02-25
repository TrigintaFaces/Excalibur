// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Represents information about a Pub/Sub schema in the Schema Registry.
/// </summary>
/// <remarks>
/// <para>
/// Schema information includes the schema identifier, definition, type, revision,
/// and creation timestamp. Schemas are immutable once created; updates produce
/// new revisions.
/// </para>
/// </remarks>
public sealed class PubSubSchemaInfo
{
	/// <summary>
	/// Gets or sets the fully qualified schema name.
	/// </summary>
	/// <value>The schema name in the format <c>projects/{project}/schemas/{schema}</c>.</value>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema type.
	/// </summary>
	/// <value>The schema type (e.g., "PROTOCOL_BUFFER" or "AVRO").</value>
	public string SchemaType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema definition content.
	/// </summary>
	/// <value>The schema definition in Protocol Buffers or Avro format.</value>
	public string Definition { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema revision identifier.
	/// </summary>
	/// <value>The revision ID. Each update to a schema produces a new revision.</value>
	public string RevisionId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the timestamp when this schema revision was created.
	/// </summary>
	/// <value>The creation timestamp.</value>
	public DateTimeOffset CreatedAt { get; set; }
}

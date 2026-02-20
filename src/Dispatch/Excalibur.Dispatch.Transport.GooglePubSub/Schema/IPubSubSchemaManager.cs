// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Manages schema evolution for Google Pub/Sub message schemas.
/// </summary>
/// <remarks>
/// <para>
/// Schema evolution enables forward and backward compatible changes to message schemas
/// over time. This interface provides operations to create, update, and validate schemas
/// against the Google Pub/Sub Schema Registry.
/// </para>
/// <para>
/// Google Pub/Sub supports two schema types:
/// <list type="bullet">
///   <item><description>Protocol Buffers (recommended for performance and type safety)</description></item>
///   <item><description>Apache Avro (recommended for schema evolution flexibility)</description></item>
/// </list>
/// </para>
/// <para>
/// Schemas are project-level resources identified by name. When a topic is bound to a schema,
/// all messages published to that topic must conform to the schema definition.
/// </para>
/// </remarks>
public interface IPubSubSchemaManager
{
	/// <summary>
	/// Creates a new schema in the Pub/Sub Schema Registry.
	/// </summary>
	/// <param name="schemaId">The unique identifier for the schema within the project.</param>
	/// <param name="definition">The schema definition (Protocol Buffers or Avro format).</param>
	/// <param name="schemaType">The type of schema (e.g., "PROTOCOL_BUFFER" or "AVRO").</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="Task{TResult}"/> representing the asynchronous operation,
	/// containing the created <see cref="PubSubSchemaInfo"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schemaId"/> or <paramref name="definition"/> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when a schema with the specified ID already exists.
	/// </exception>
	Task<PubSubSchemaInfo> CreateSchemaAsync(
		string schemaId,
		string definition,
		string schemaType,
		CancellationToken cancellationToken);

	/// <summary>
	/// Updates an existing schema by creating a new revision.
	/// </summary>
	/// <param name="schemaId">The unique identifier of the schema to update.</param>
	/// <param name="definition">The updated schema definition.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="Task{TResult}"/> representing the asynchronous operation,
	/// containing the updated <see cref="PubSubSchemaInfo"/> with the new revision.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="schemaId"/> or <paramref name="definition"/> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the schema does not exist or the update is not compatible.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Pub/Sub schema revisions are immutable. Updating a schema creates a new revision
	/// while preserving the previous revision for backward compatibility.
	/// </para>
	/// </remarks>
	Task<PubSubSchemaInfo> UpdateSchemaAsync(
		string schemaId,
		string definition,
		CancellationToken cancellationToken);

	/// <summary>
	/// Validates a schema definition against the Pub/Sub Schema Registry without creating it.
	/// </summary>
	/// <param name="definition">The schema definition to validate.</param>
	/// <param name="schemaType">The type of schema (e.g., "PROTOCOL_BUFFER" or "AVRO").</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="Task{TResult}"/> representing the asynchronous operation,
	/// containing the <see cref="SchemaValidationResult"/>.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="definition"/> is null, empty, or whitespace.
	/// </exception>
	/// <remarks>
	/// <para>
	/// Use this method to validate a schema definition before creating or updating it.
	/// This performs syntax and semantic validation without persisting the schema.
	/// </para>
	/// </remarks>
	Task<SchemaValidationResult> ValidateSchemaAsync(
		string definition,
		string schemaType,
		CancellationToken cancellationToken);
}

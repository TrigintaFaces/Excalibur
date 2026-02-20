// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Provides schema registry operations for schema management in message serialization.
/// </summary>
/// <remarks>
/// <para>
/// This interface is transport-agnostic to support multiple schema registry implementations:
/// </para>
/// <list type="bullet">
///   <item><description>Confluent Schema Registry (implemented)</description></item>
///   <item><description>AWS Glue Schema Registry (future)</description></item>
///   <item><description>Azure Schema Registry (future)</description></item>
/// </list>
/// <para>
/// Implementations should be registered as singletons and are expected to be thread-safe.
/// </para>
/// </remarks>
public interface ISchemaRegistryClient
{
	/// <summary>
	/// Gets the schema ID for a given subject and schema definition.
	/// </summary>
	/// <param name="subject">The schema subject (typically topic-key or topic-value).</param>
	/// <param name="schema">The schema definition (JSON, Avro, Protobuf).</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The schema ID registered in the schema registry.</returns>
	/// <exception cref="SchemaRegistryException">
	/// Thrown when the schema cannot be found or the registry is unavailable.
	/// </exception>
	Task<int> GetSchemaIdAsync(
		string subject,
		string schema,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the schema definition for a given schema ID.
	/// </summary>
	/// <param name="schemaId">The schema ID to retrieve.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The schema definition string.</returns>
	/// <exception cref="SchemaRegistryException">
	/// Thrown when the schema ID is not found or the registry is unavailable.
	/// </exception>
	Task<string> GetSchemaByIdAsync(
		int schemaId,
		CancellationToken cancellationToken);

	/// <summary>
	/// Registers a new schema under the specified subject.
	/// </summary>
	/// <param name="subject">The schema subject (typically topic-key or topic-value).</param>
	/// <param name="schema">The schema definition to register.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>The schema ID assigned by the registry.</returns>
	/// <exception cref="SchemaRegistryException">
	/// Thrown when the schema registration fails or the registry is unavailable.
	/// </exception>
	Task<int> RegisterSchemaAsync(
		string subject,
		string schema,
		CancellationToken cancellationToken);

	/// <summary>
	/// Checks if a schema is compatible with the latest version under the specified subject.
	/// </summary>
	/// <param name="subject">The schema subject to check compatibility against.</param>
	/// <param name="schema">The schema definition to validate.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// <see langword="true"/> if the schema is compatible; otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="SchemaRegistryException">
	/// Thrown when the compatibility check fails or the registry is unavailable.
	/// </exception>
	Task<bool> IsCompatibleAsync(
		string subject,
		string schema,
		CancellationToken cancellationToken);
}

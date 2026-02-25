// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

namespace Excalibur.Data.CosmosDb.Saga;

/// <summary>
/// Cosmos DB document representation of a stored saga state.
/// </summary>
/// <remarks>
/// <para>
/// Uses sagaId as the document ID and sagaType as the partition key.
/// This ensures one saga state per saga instance and enables efficient queries within saga type boundaries.
/// </para>
/// <para>
/// The read-check-upsert pattern ensures createdUtc is preserved on updates,
/// maintaining accurate audit information for saga lifecycle tracking.
/// </para>
/// </remarks>
internal sealed class CosmosDbSagaDocument
{
	/// <summary>
	/// Gets or sets the document ID (sagaId as string).
	/// </summary>
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the saga identifier.
	/// </summary>
	[JsonPropertyName("sagaId")]
	public Guid SagaId { get; set; }

	/// <summary>
	/// Gets or sets the saga type name (partition key).
	/// </summary>
	[JsonPropertyName("sagaType")]
	public string SagaType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the serialized saga state as JSON.
	/// </summary>
	[JsonPropertyName("stateJson")]
	public string StateJson { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the saga has completed.
	/// </summary>
	[JsonPropertyName("isCompleted")]
	public bool IsCompleted { get; set; }

	/// <summary>
	/// Gets or sets when the saga was created (UTC).
	/// </summary>
	[JsonPropertyName("createdUtc")]
	public DateTimeOffset CreatedUtc { get; set; }

	/// <summary>
	/// Gets or sets when the saga was last updated (UTC).
	/// </summary>
	[JsonPropertyName("updatedUtc")]
	public DateTimeOffset UpdatedUtc { get; set; }

	/// <summary>
	/// Creates the document ID from saga ID.
	/// </summary>
	/// <param name="sagaId">The saga identifier.</param>
	/// <returns>The document ID string.</returns>
	public static string CreateId(Guid sagaId) => sagaId.ToString();
}

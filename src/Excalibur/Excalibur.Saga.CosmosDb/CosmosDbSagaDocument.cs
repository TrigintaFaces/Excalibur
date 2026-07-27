// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

namespace Excalibur.Saga.CosmosDb;

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
	[Newtonsoft.Json.JsonProperty("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the saga identifier.
	/// </summary>
	[JsonPropertyName("sagaId")]
	[Newtonsoft.Json.JsonProperty("sagaId")]
	public Guid SagaId { get; set; }

	/// <summary>
	/// Gets or sets the saga type name (partition key).
	/// </summary>
	[JsonPropertyName("sagaType")]
	[Newtonsoft.Json.JsonProperty("sagaType")]
	public string SagaType { get; set; } = string.Empty;

	/// <summary>
	/// The tenant that owns this saga, or <see langword="null"/> for the untenanted partition.
	/// </summary>
	/// <remarks>
	/// Carries BOTH serializer attributes deliberately. The Cosmos v3 SDK's default serializer is Newtonsoft,
	/// and System.Text.Json is opt-in, so a property annotated only for STJ round-trips as PascalCase on the
	/// most common client configuration — the tenant would silently vanish from the document on exactly the
	/// setup most consumers run. Every persisted property on this type carries the pair for that reason.
	/// </remarks>
	[JsonPropertyName("tenantId")]
	[Newtonsoft.Json.JsonProperty("tenantId")]
	public string? TenantId { get; set; }

	/// <summary>
	/// Gets or sets the serialized saga state as JSON.
	/// </summary>
	[JsonPropertyName("stateJson")]
	[Newtonsoft.Json.JsonProperty("stateJson")]
	public string StateJson { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the saga has completed.
	/// </summary>
	[JsonPropertyName("isCompleted")]
	[Newtonsoft.Json.JsonProperty("isCompleted")]
	public bool IsCompleted { get; set; }

	/// <summary>
	/// Gets or sets when the saga completed (UTC), or <see langword="null"/> while it is still running.
	/// </summary>
	/// <remarks>
	/// Persisted as a dedicated document field (not read out of <see cref="StateJson"/>) so retention
	/// cleanup can query completed-before-threshold sagas directly. Dual STJ + Newtonsoft attributes so the
	/// lowercase <c>completedAt</c> key is emitted regardless of which serializer the Cosmos client uses.
	/// </remarks>
	[JsonPropertyName("completedAt")]
	[Newtonsoft.Json.JsonProperty("completedAt")]
	public DateTime? CompletedAt { get; set; }

	/// <summary>
	/// Gets or sets the optimistic-concurrency version of the persisted saga state.
	/// </summary>
	[JsonPropertyName("version")]
	[Newtonsoft.Json.JsonProperty("version")]
	public long Version { get; set; }

	/// <summary>
	/// Gets or sets when the saga was created (UTC).
	/// </summary>
	[JsonPropertyName("createdUtc")]
	[Newtonsoft.Json.JsonProperty("createdUtc")]
	public DateTimeOffset CreatedUtc { get; set; }

	/// <summary>
	/// Gets or sets when the saga was last updated (UTC).
	/// </summary>
	[JsonPropertyName("updatedUtc")]
	[Newtonsoft.Json.JsonProperty("updatedUtc")]
	public DateTimeOffset UpdatedUtc { get; set; }

	/// <summary>
	/// Creates the document ID from saga ID.
	/// </summary>
	/// <param name="sagaId">The saga identifier.</param>
	/// <returns>The document ID string.</returns>
	public static string CreateId(Guid sagaId) => sagaId.ToString();
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.Json.Serialization;

namespace Excalibur.Saga.CosmosDb;

/// <summary>
/// Cosmos DB document representation of a stored saga state.
/// </summary>
/// <remarks>
/// <para>
/// Uses the owning tenant composed with the saga identifier as the document ID, and sagaType as the
/// partition key. This gives one saga state per saga instance PER TENANT and enables efficient queries
/// within saga type boundaries.
/// </para>
/// <para>
/// The read-check-upsert pattern ensures createdUtc is preserved on updates,
/// maintaining accurate audit information for saga lifecycle tracking.
/// </para>
/// </remarks>
internal sealed class CosmosDbSagaDocument
{
	/// <summary>
	/// Gets or sets the document ID: the owning tenant composed with the saga identifier.
	/// </summary>
	/// <remarks>
	/// The tenant belongs to the document's IDENTITY, which is a different property from the ownership check
	/// the store applies after reading. The check decides whether this scope may use a document; the identity
	/// decides which documents can exist at all. Keyed on the saga identifier alone, two tenants running a
	/// saga at the same business key are ONE document, so refusing the cross-tenant write also refuses the
	/// second tenant its own saga.
	/// </remarks>
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
	/// The leading segment every document ID this store writes carries, ahead of the owning tenant.
	/// </summary>
	/// <remarks>
	/// Declared once and consumed by both the key builder and the store's legacy-document probe, so the shape
	/// the store writes and the shape it refuses to read cannot drift apart.
	/// </remarks>
	public const string TenantKeyPrefix = "t:";

	/// <summary>
	/// Exclusive upper bound of the tenant-prefixed key range, used by the store's legacy-document probe.
	/// </summary>
	/// <remarks>
	/// <c>':'</c> is U+003A and <c>';'</c> is U+003B, so every identifier beginning with
	/// <see cref="TenantKeyPrefix"/> sorts inside <c>["t:", "t;")</c> and every identifier outside that range
	/// lacks the prefix.
	/// </remarks>
	public const string TenantKeyPrefixUpperBound = "t;";

	/// <summary>
	/// Creates the document ID from the owning tenant and the saga ID.
	/// </summary>
	/// <remarks>
	/// The tenant term is total (never null, never empty): a host with no tenancy resolves the framework
	/// single-tenant default and a genuinely untenanted saga resolves the reserved untenanted sentinel, so
	/// every document ID carries a tenant segment and none can be produced without one.
	/// </remarks>
	/// <param name="tenantId">The owning tenant term, as resolved from the store's scope.</param>
	/// <param name="sagaId">The saga identifier.</param>
	/// <returns>The document ID string.</returns>
	public static string CreateId(string tenantId, Guid sagaId) => $"{TenantKeyPrefix}{tenantId}:{sagaId}";
}

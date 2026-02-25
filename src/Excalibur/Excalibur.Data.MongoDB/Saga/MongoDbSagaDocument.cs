// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Data.MongoDB.Saga;

/// <summary>
/// MongoDB document representation of saga state.
/// </summary>
/// <remarks>
/// <para>
/// Uses the saga ID as the document ID for efficient lookups and atomic upserts.
/// The Guid is stored with string representation for readability and portability.
/// </para>
/// <para>
/// Following MongoDB naming conventions with camelCase element names.
/// </para>
/// </remarks>
internal sealed class MongoDbSagaDocument
{
	/// <summary>
	/// Gets or sets the saga identifier (used as _id).
	/// </summary>
	[BsonId]
	[BsonRepresentation(BsonType.String)]
	public Guid SagaId { get; set; }

	/// <summary>
	/// Gets or sets the saga type name.
	/// </summary>
	[BsonElement("sagaType")]
	public string SagaType { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the JSON-serialized saga state.
	/// </summary>
	[BsonElement("stateJson")]
	public string StateJson { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets whether the saga is completed.
	/// </summary>
	[BsonElement("isCompleted")]
	public bool IsCompleted { get; set; }

	/// <summary>
	/// Gets or sets when the document was created.
	/// </summary>
	[BsonElement("createdUtc")]
	public DateTime CreatedUtc { get; set; }

	/// <summary>
	/// Gets or sets when the document was last updated.
	/// </summary>
	[BsonElement("updatedUtc")]
	public DateTime UpdatedUtc { get; set; }
}

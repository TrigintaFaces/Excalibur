// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.LeaderElection.MongoDB;

/// <summary>
/// MongoDB document representing a leader election lock.
/// </summary>
/// <remarks>
/// Uses the resource name as the document ID. The TTL index on <see cref="ExpiresAt"/>
/// ensures automatic cleanup of stale leadership records by MongoDB.
/// </remarks>
internal sealed class MongoDbLeaderElectionDocument
{
	/// <summary>
	/// Gets or sets the resource name (used as document ID).
	/// </summary>
	[BsonId]
	public string ResourceName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the candidate ID that holds the lease.
	/// </summary>
	[BsonElement("candidateId")]
	public string CandidateId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets when the lease was acquired.
	/// </summary>
	[BsonElement("acquiredAt")]
	[BsonRepresentation(BsonType.DateTime)]
	public DateTime AcquiredAt { get; set; }

	/// <summary>
	/// Gets or sets when the lease expires.
	/// </summary>
	/// <remarks>
	/// This field has a TTL index so MongoDB automatically removes expired documents.
	/// </remarks>
	[BsonElement("expiresAt")]
	[BsonRepresentation(BsonType.DateTime)]
	public DateTime ExpiresAt { get; set; }

	/// <summary>
	/// Gets or sets when the lease was last renewed.
	/// </summary>
	[BsonElement("lastRenewedAt")]
	[BsonRepresentation(BsonType.DateTime)]
	public DateTime LastRenewedAt { get; set; }

	/// <summary>
	/// Gets or sets the store-arbitrated fencing token for this lock's current holder.
	/// </summary>
	/// <remarks>
	/// Strictly increases each time leadership passes to a different candidate (computed
	/// server-side inside the atomic takeover pipeline); unchanged when the same candidate
	/// renews its own lease. Starts at 1 on first acquisition.
	/// </remarks>
	[BsonElement("fencingToken")]
	public long FencingToken { get; set; }
}

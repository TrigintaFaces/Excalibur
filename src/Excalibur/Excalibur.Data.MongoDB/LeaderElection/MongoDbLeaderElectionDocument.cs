// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using MongoDB.Bson.Serialization.Attributes;

namespace Excalibur.Data.MongoDB.LeaderElection;

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
	public DateTime AcquiredAt { get; set; }

	/// <summary>
	/// Gets or sets when the lease expires.
	/// </summary>
	/// <remarks>
	/// This field has a TTL index so MongoDB automatically removes expired documents.
	/// </remarks>
	[BsonElement("expiresAt")]
	public DateTime ExpiresAt { get; set; }

	/// <summary>
	/// Gets or sets when the lease was last renewed.
	/// </summary>
	[BsonElement("lastRenewedAt")]
	public DateTime LastRenewedAt { get; set; }
}

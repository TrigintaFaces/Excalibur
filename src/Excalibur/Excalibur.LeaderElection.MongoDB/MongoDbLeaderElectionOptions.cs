// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.LeaderElection.MongoDB;

/// <summary>
/// Configuration options for the MongoDB leader election provider.
/// </summary>
public sealed class MongoDbLeaderElectionOptions
{
	/// <summary>
	/// Gets or sets the MongoDB connection string.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the database name for storing leader election documents.
	/// </summary>
	[Required]
	public string DatabaseName { get; set; } = "excalibur";

	/// <summary>
	/// Gets or sets the collection name for leader election locks.
	/// </summary>
	[Required]
	public string CollectionName { get; set; } = "leader_elections";

	/// <summary>
	/// Gets or sets the lease duration for leadership in seconds.
	/// </summary>
	/// <value>Defaults to 15 seconds.</value>
	[Range(1, int.MaxValue)]
	public int LeaseDurationSeconds { get; set; } = 15;

	/// <summary>
	/// Gets or sets the renewal interval in seconds. Should be less than lease duration.
	/// </summary>
	/// <value>Defaults to 5 seconds.</value>
	[Range(1, int.MaxValue)]
	public int RenewIntervalSeconds { get; set; } = 5;

	/// <summary>
	/// Gets or sets the timeout for MongoDB operations in seconds.
	/// </summary>
	[Range(1, int.MaxValue)]
	public int TimeoutInSeconds { get; set; } = 10;

	/// <summary>
	/// Gets or sets the additional grace window, in seconds, that must elapse after a lease's
	/// recorded expiry before another candidate may take over the lock.
	/// </summary>
	/// <value>Defaults to 3 seconds.</value>
	/// <remarks>
	/// The takeover decision is evaluated on the MongoDB server's clock, never the candidate's
	/// local clock, so this window protects against the incumbent's own lease-renewal jitter and
	/// network delay rather than inter-node clock skew. A wider window makes takeover slower but
	/// more conservative; a narrower window makes takeover faster but more prone to contested
	/// handoffs while the incumbent's own renewal is merely delayed rather than truly lost.
	/// </remarks>
	[Range(0, int.MaxValue)]
	public int TakeoverGraceSeconds { get; set; } = 3;

	/// <summary>
	/// Validates the options and throws if invalid.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when required options are missing.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException("ConnectionString is required.");
		}

		if (string.IsNullOrWhiteSpace(DatabaseName))
		{
			throw new InvalidOperationException("DatabaseName is required.");
		}

		if (RenewIntervalSeconds >= LeaseDurationSeconds)
		{
			throw new InvalidOperationException(
				"RenewIntervalSeconds must be less than LeaseDurationSeconds.");
		}
	}
}

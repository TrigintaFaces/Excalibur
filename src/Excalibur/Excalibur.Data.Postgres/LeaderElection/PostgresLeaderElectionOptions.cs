// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Data.Postgres.LeaderElection;

/// <summary>
/// Configuration options for Postgres leader election.
/// </summary>
public sealed class PostgresLeaderElectionOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the advisory lock key.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Postgres advisory locks are identified by a 64-bit integer key.
	/// Multiple candidates compete for the same key. Choose a value
	/// unique to your application's leader election resource.
	/// </para>
	/// </remarks>
	/// <value>Defaults to <c>1</c>.</value>
	[Range(1, long.MaxValue)]
	public long LockKey { get; set; } = 1;

	/// <summary>
	/// Gets or sets the command timeout in seconds for lock operations.
	/// </summary>
	/// <value>Defaults to <c>5</c> seconds.</value>
	[Range(1, 300)]
	public int CommandTimeoutSeconds { get; set; } = 5;

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
	}
}

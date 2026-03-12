// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Postgres;

/// <summary>
/// Factory for creating Postgres leader election instances.
/// </summary>
/// <remarks>
/// Use this factory when you need multiple leader elections with different lock keys
/// within the same application. Each call to <see cref="CreateElection"/> or
/// <see cref="CreateHealthBasedElection"/> creates an independent leader election
/// instance with its own connection and advisory lock.
/// </remarks>
public sealed class PostgresLeaderElectionFactory : ILeaderElectionFactory
{
	private readonly PostgresLeaderElectionOptions _pgOptions;
	private readonly ILoggerFactory _loggerFactory;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresLeaderElectionFactory"/> class.
	/// </summary>
	/// <param name="pgOptions">The Postgres leader election options.</param>
	/// <param name="loggerFactory">The logger factory.</param>
	public PostgresLeaderElectionFactory(
		IOptions<PostgresLeaderElectionOptions> pgOptions,
		ILoggerFactory loggerFactory)
	{
		ArgumentNullException.ThrowIfNull(pgOptions);
		ArgumentNullException.ThrowIfNull(loggerFactory);

		_pgOptions = pgOptions.Value;
		_loggerFactory = loggerFactory;
	}

	/// <inheritdoc/>
	public ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

		var lockKey = ComputeLockKey(resourceName);

		var pgOptions = new PostgresLeaderElectionOptions
		{
			ConnectionString = _pgOptions.ConnectionString,
			LockKey = lockKey,
			CommandTimeoutSeconds = _pgOptions.CommandTimeoutSeconds,
		};

		var electionOptions = new LeaderElectionOptions
		{
			InstanceId = candidateId ?? new LeaderElectionOptions().InstanceId,
		};

		var logger = _loggerFactory.CreateLogger<PostgresLeaderElection>();
		return new PostgresLeaderElection(Options.Create(pgOptions), Options.Create(electionOptions), logger);
	}

	/// <inheritdoc/>
	public IHealthBasedLeaderElection CreateHealthBasedElection(string resourceName, string? candidateId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

		var lockKey = ComputeLockKey(resourceName);

		var pgOptions = new PostgresLeaderElectionOptions
		{
			ConnectionString = _pgOptions.ConnectionString,
			LockKey = lockKey,
			CommandTimeoutSeconds = _pgOptions.CommandTimeoutSeconds,
		};

		var electionOptions = new LeaderElectionOptions
		{
			InstanceId = candidateId ?? new LeaderElectionOptions().InstanceId,
		};

		var healthOptions = new PostgresHealthBasedLeaderElectionOptions();

		var logger = _loggerFactory.CreateLogger<PostgresHealthBasedLeaderElection>();
		var innerLogger = _loggerFactory.CreateLogger<PostgresLeaderElection>();

		return new PostgresHealthBasedLeaderElection(
			Options.Create(pgOptions),
			Options.Create(electionOptions),
			Options.Create(healthOptions),
			logger,
			innerLogger);
	}

	/// <summary>
	/// Computes a stable lock key from a resource name using a hash.
	/// </summary>
	private static long ComputeLockKey(string resourceName)
	{
		// Use a simple stable hash to convert string resource name to long lock key
		// This ensures the same resource name always maps to the same lock key
		unchecked
		{
			long hash = 17;
			foreach (var c in resourceName)
			{
				hash = (hash * 31) + c;
			}

			// Ensure positive value for pg_try_advisory_lock
			return Math.Abs(hash);
		}
	}
}

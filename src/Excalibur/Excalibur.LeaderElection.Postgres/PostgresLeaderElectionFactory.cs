// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;

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
	private readonly IOptions<LeaderElectionOptions> _electionOptions;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IFencingTokenProvider? _fencingTokenProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresLeaderElectionFactory"/> class.
	/// </summary>
	/// <param name="pgOptions">The Postgres leader election options.</param>
	/// <param name="electionOptions">
	/// The base leader-election options (lease duration, renewal interval, grace period, instance id) configured by
	/// the consumer. These are honored by every election this factory creates.
	/// </param>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="fencingTokenProvider">
	/// An optional <see cref="IFencingTokenProvider"/> propagated to every election this
	/// factory creates, enabling fail-closed fencing-token issuance on leadership acquisition. Defaults to
	/// <see langword="null"/> (no fencing).
	/// </param>
	public PostgresLeaderElectionFactory(
		IOptions<PostgresLeaderElectionOptions> pgOptions,
		IOptions<LeaderElectionOptions> electionOptions,
		ILoggerFactory loggerFactory,
		IFencingTokenProvider? fencingTokenProvider = null)
	{
		ArgumentNullException.ThrowIfNull(pgOptions);
		ArgumentNullException.ThrowIfNull(electionOptions);
		ArgumentNullException.ThrowIfNull(loggerFactory);

		_pgOptions = pgOptions.Value;
		_electionOptions = electionOptions;
		_loggerFactory = loggerFactory;
		_fencingTokenProvider = fencingTokenProvider;
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

		var electionOptions = ResolveOptions(_electionOptions, candidateId);

		var logger = _loggerFactory.CreateLogger<PostgresLeaderElection>();
		return new PostgresLeaderElection(Options.Create(pgOptions), electionOptions, logger, _fencingTokenProvider);
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

		var electionOptions = ResolveOptions(_electionOptions, candidateId);

		var healthOptions = new PostgresHealthBasedLeaderElectionOptions();

		var logger = _loggerFactory.CreateLogger<PostgresHealthBasedLeaderElection>();
		var innerLogger = _loggerFactory.CreateLogger<PostgresLeaderElection>();

		return new PostgresHealthBasedLeaderElection(
			Options.Create(pgOptions),
			electionOptions,
			Options.Create(healthOptions),
			logger,
			innerLogger,
			_fencingTokenProvider);
	}

	/// <summary>
	/// Builds the per-election options, preserving every consumer-configured value
	/// (<see cref="LeaderElectionOptions.LeaseDuration"/>, <see cref="LeaderElectionOptions.RenewInterval"/>,
	/// <see cref="LeaderElectionOptions.GracePeriod"/>, etc.) and overriding only the instance identifier when an
	/// explicit <paramref name="candidateId"/> is supplied. When no candidate id is given, the registered options
	/// are passed through unchanged so the configured identity and timing are honored.
	/// </summary>
	private static IOptions<LeaderElectionOptions> ResolveOptions(IOptions<LeaderElectionOptions> configured, string? candidateId)
	{
		if (string.IsNullOrWhiteSpace(candidateId))
		{
			return configured;
		}

		var source = configured.Value;
		var election = new LeaderElectionOptions
		{
			InstanceId = candidateId,
			LeaseDuration = source.LeaseDuration,
			RenewInterval = source.RenewInterval,
			RetryInterval = source.RetryInterval,
			GracePeriod = source.GracePeriod,
			EnableHealthChecks = source.EnableHealthChecks,
			MinimumHealthScore = source.MinimumHealthScore,
			StepDownWhenUnhealthy = source.StepDownWhenUnhealthy,
		};

		foreach (var kvp in source.CandidateMetadata)
		{
			election.CandidateMetadata[kvp.Key] = kvp.Value;
		}

		return Options.Create(election);
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

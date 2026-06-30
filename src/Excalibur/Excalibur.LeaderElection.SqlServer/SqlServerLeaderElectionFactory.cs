// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.SqlServer;

/// <summary>
/// Factory for creating SQL Server leader election instances.
/// </summary>
public sealed class SqlServerLeaderElectionFactory : ILeaderElectionFactory
{
	private readonly string _connectionString;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IOptions<LeaderElectionOptions> _options;
	private readonly IMessageFailureClassifier? _failureClassifier;
	private readonly IFencingTokenProvider? _fencingTokenProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerLeaderElectionFactory"/> class.
	/// </summary>
	/// <param name="connectionString">The SQL Server connection string.</param>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="options">
	/// The base leader-election options (lease duration, renewal interval, grace period, instance id) configured by
	/// the consumer. These are honored by every election this factory creates.
	/// </param>
	/// <param name="failureClassifier">
	/// An optional <see cref="IMessageFailureClassifier"/> (ot72w3) propagated to every election this
	/// factory creates, enabling accelerated self-demotion on definitively-permanent renewal faults.
	/// Defaults to <see langword="null"/> (grace-only behavior).
	/// </param>
	/// <param name="fencingTokenProvider">
	/// An optional <see cref="IFencingTokenProvider"/> propagated to every election this
	/// factory creates, enabling fail-closed fencing-token issuance on leadership acquisition. Defaults to
	/// <see langword="null"/> (no fencing).
	/// </param>
	public SqlServerLeaderElectionFactory(
		string connectionString,
		ILoggerFactory loggerFactory,
		IOptions<LeaderElectionOptions> options,
		IMessageFailureClassifier? failureClassifier = null,
		IFencingTokenProvider? fencingTokenProvider = null)
	{
		_connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
		_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_failureClassifier = failureClassifier;
		_fencingTokenProvider = fencingTokenProvider;
	}

	/// <inheritdoc/>
	public ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

		var logger = _loggerFactory.CreateLogger<SqlServerLeaderElection>();
		return new SqlServerLeaderElection(_connectionString, resourceName, ResolveOptions(_options, candidateId), logger, _failureClassifier, _fencingTokenProvider);
	}

	/// <inheritdoc/>
	public IHealthBasedLeaderElection CreateHealthBasedElection(string resourceName, string? candidateId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

		var electionOptions = ResolveOptions(_options, candidateId);

		var healthOptions = new SqlServerHealthBasedLeaderElectionOptions();

		var logger = _loggerFactory.CreateLogger<SqlServerHealthBasedLeaderElection>();
		var innerLogger = _loggerFactory.CreateLogger<SqlServerLeaderElection>();

		return new SqlServerHealthBasedLeaderElection(
			_connectionString,
			resourceName,
			electionOptions,
			Options.Create(healthOptions),
			logger,
			innerLogger,
			_failureClassifier,
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
}

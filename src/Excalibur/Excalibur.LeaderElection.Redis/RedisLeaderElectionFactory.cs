// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Dispatch.LeaderElection.Fencing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Excalibur.LeaderElection.Redis;

/// <summary>
/// Factory for creating Redis leader election instances.
/// </summary>
public sealed class RedisLeaderElectionFactory : ILeaderElectionFactory
{
	private readonly IConnectionMultiplexer _redis;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IMessageFailureClassifier? _failureClassifier;
	private readonly IFencingTokenProvider? _fencingTokenProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisLeaderElectionFactory"/> class.
	/// </summary>
	/// <param name="redis">The Redis connection multiplexer.</param>
	/// <param name="loggerFactory">The logger factory.</param>
	/// <param name="failureClassifier">
	/// An optional <see cref="IMessageFailureClassifier"/> (ot72w3) propagated to every election this
	/// factory creates, enabling accelerated self-demotion on definitively-permanent renewal faults.
	/// Defaults to <see langword="null"/> (grace-only behavior).
	/// </param>
	/// <param name="fencingTokenProvider">
	/// An optional <see cref="IFencingTokenProvider"/> (umemwa/ADR-339) propagated to every election this
	/// factory creates, enabling monotonic fencing-token issuance at leadership acquisition. Defaults to
	/// <see langword="null"/> (no token issued).
	/// </param>
	public RedisLeaderElectionFactory(
		IConnectionMultiplexer redis,
		ILoggerFactory loggerFactory,
		IMessageFailureClassifier? failureClassifier = null,
		IFencingTokenProvider? fencingTokenProvider = null)
	{
		_redis = redis ?? throw new ArgumentNullException(nameof(redis));
		_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
		_failureClassifier = failureClassifier;
		_fencingTokenProvider = fencingTokenProvider;
	}

	/// <inheritdoc/>
	public ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

		var options = new LeaderElectionOptions
		{
			InstanceId = candidateId ?? new LeaderElectionOptions().InstanceId
		};

		var logger = _loggerFactory.CreateLogger<RedisLeaderElection>();
		return new RedisLeaderElection(_redis, resourceName, Options.Create(options), logger, _failureClassifier, _fencingTokenProvider);
	}

	/// <inheritdoc/>
	public IHealthBasedLeaderElection CreateHealthBasedElection(string resourceName, string? candidateId)
	{
		throw new NotSupportedException(
				Resources.RedisLeaderElectionFactory_HealthBasedElectionNotSupported);
	}
}

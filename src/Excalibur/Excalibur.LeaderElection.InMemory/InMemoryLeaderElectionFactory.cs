// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.LeaderElection;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.InMemory;

/// <summary>
/// Factory for creating in-memory leader election instances.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="InMemoryLeaderElectionFactory" /> class. </remarks>
/// <param name="options"> The leader election options. </param>
/// <param name="loggerFactory"> The logger factory. </param>
/// <param name="sharedState"> Optional shared state for test isolation. Uses <see cref="InMemoryLeaderElectionSharedState.Default"/> if not provided. </param>
public sealed class InMemoryLeaderElectionFactory(
	IOptions<LeaderElectionOptions> options,
	ILoggerFactory? loggerFactory = null,
	InMemoryLeaderElectionSharedState? sharedState = null) : ILeaderElectionFactory
{
	private readonly IOptions<LeaderElectionOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
	private readonly InMemoryLeaderElectionSharedState _sharedState = sharedState ?? InMemoryLeaderElectionSharedState.Default;

	/// <inheritdoc />
	public ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		var electionOptions = new LeaderElectionOptions
		{
			InstanceId = candidateId ?? _options.Value.InstanceId,
			LeaseDuration = _options.Value.LeaseDuration,
			RenewInterval = _options.Value.RenewInterval,
			RetryInterval = _options.Value.RetryInterval,
			GracePeriod = _options.Value.GracePeriod,
			EnableHealthChecks = false,
		};

		foreach (var kvp in _options.Value.CandidateMetadata)
		{
			electionOptions.CandidateMetadata[kvp.Key] = kvp.Value;
		}

		var optionsCopy = Options.Create(electionOptions);

		return new InMemoryLeaderElection(
			resourceName,
			optionsCopy,
			loggerFactory?.CreateLogger<InMemoryLeaderElection>(),
			_sharedState);
	}

	/// <inheritdoc />
	public IHealthBasedLeaderElection CreateHealthBasedElection(string resourceName, string? candidateId)
	{
		var electionOptions = new LeaderElectionOptions
		{
			InstanceId = candidateId ?? _options.Value.InstanceId,
			LeaseDuration = _options.Value.LeaseDuration,
			RenewInterval = _options.Value.RenewInterval,
			RetryInterval = _options.Value.RetryInterval,
			GracePeriod = _options.Value.GracePeriod,
			EnableHealthChecks = true,
			MinimumHealthScore = _options.Value.MinimumHealthScore,
			StepDownWhenUnhealthy = _options.Value.StepDownWhenUnhealthy,
		};

		foreach (var kvp in _options.Value.CandidateMetadata)
		{
			electionOptions.CandidateMetadata[kvp.Key] = kvp.Value;
		}

		var optionsCopy = Options.Create(electionOptions);

		return new InMemoryLeaderElection(
			resourceName,
			optionsCopy,
			loggerFactory?.CreateLogger<InMemoryLeaderElection>(),
			_sharedState);
	}
}

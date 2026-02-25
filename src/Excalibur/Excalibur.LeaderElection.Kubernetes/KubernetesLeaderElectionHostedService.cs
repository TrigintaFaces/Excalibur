// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.LeaderElection;

using Excalibur.LeaderElection.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.LeaderElection.Kubernetes;

/// <summary>
/// Hosted service for managing Kubernetes leader election lifecycle.
/// </summary>
public sealed partial class KubernetesLeaderElectionHostedService : IHostedService, IDisposable
{
	private readonly ILeaderElection _leaderElection;
	private readonly ILogger<KubernetesLeaderElectionHostedService> _logger;
	private readonly string _resourceName;

	/// <summary>
	/// Initializes a new instance of the <see cref="KubernetesLeaderElectionHostedService" /> class.
	/// </summary>
	/// <param name="factory"> The leader election factory. </param>
	/// <param name="resourceName"> The resource name for leader election. </param>
	/// <param name="options"> The Kubernetes leader election options. </param>
	/// <param name="logger"> The logger. </param>
	public KubernetesLeaderElectionHostedService(
		ILeaderElectionFactory factory,
		string resourceName,
		IOptions<KubernetesLeaderElectionOptions> options,
		ILogger<KubernetesLeaderElectionHostedService>? logger)
	{
		ArgumentNullException.ThrowIfNull(factory);
		ArgumentNullException.ThrowIfNull(options);
		_resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
		_logger = logger ?? NullLogger<KubernetesLeaderElectionHostedService>.Instance;

		// Create the leader election instance
		_leaderElection = options.Value.EnableHealthChecks
			? factory.CreateHealthBasedElection(resourceName, candidateId: null)
			: factory.CreateElection(resourceName, candidateId: null);

		// Subscribe to events
		_leaderElection.BecameLeader += BecameLeader;
		_leaderElection.LostLeadership += LostLeadership;
		_leaderElection.LeaderChanged += LeaderChanged;
	}

	/// <summary>
	/// Gets a value indicating whether this instance is currently the leader.
	/// </summary>
	/// <value><see langword="true"/> if this instance is currently the leader; otherwise, <see langword="false"/>.</value>
	public bool IsLeader => _leaderElection.IsLeader;

	/// <summary>
	/// Gets the current leader's identifier.
	/// </summary>
	/// <value>The current leader's identifier, or <see langword="null"/> if no leader is elected.</value>
	public string? CurrentLeaderId => _leaderElection.CurrentLeaderId;

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		LogStartingService(_resourceName);
		await _leaderElection.StartAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		LogStoppingService(_resourceName);
		await _leaderElection.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		// Unsubscribe from events
		_leaderElection.BecameLeader -= BecameLeader;
		_leaderElection.LostLeadership -= LostLeadership;
		_leaderElection.LeaderChanged -= LeaderChanged;

		// Dispose the leader election if it's disposable
		if (_leaderElection is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	private void BecameLeader(object? sender, LeaderElectionEventArgs e) =>
		LogBecameLeader(e.ResourceName, e.Timestamp);

	private void LostLeadership(object? sender, LeaderElectionEventArgs e) =>
		LogLostLeadershipEvent(e.ResourceName, e.Timestamp);

	private void LeaderChanged(object? sender, LeaderChangedEventArgs e) =>
		LogLeaderChangedEvent(e.PreviousLeaderId ?? "none", e.NewLeaderId ?? "none", e.ResourceName, e.Timestamp);

	// LoggerMessage delegates
	[LoggerMessage(LeaderElectionEventId.KubernetesServiceStarting, LogLevel.Information, "Starting Kubernetes leader election hosted service for resource '{Resource}'")]
	partial void LogStartingService(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesServiceStopping, LogLevel.Information, "Stopping Kubernetes leader election hosted service for resource '{Resource}'")]
	partial void LogStoppingService(string resource);

	[LoggerMessage(LeaderElectionEventId.KubernetesServiceBecameLeader, LogLevel.Information, "This instance became the leader for resource '{Resource}' at {Timestamp}")]
	partial void LogBecameLeader(string resource, DateTimeOffset timestamp);

	[LoggerMessage(LeaderElectionEventId.KubernetesServiceLostLeadership, LogLevel.Warning, "This instance lost leadership for resource '{Resource}' at {Timestamp}")]
	partial void LogLostLeadershipEvent(string resource, DateTimeOffset timestamp);

	[LoggerMessage(LeaderElectionEventId.KubernetesServiceLeaderChanged, LogLevel.Information, "Leader changed from '{Previous}' to '{New}' for resource '{Resource}' at {Timestamp}")]
	partial void LogLeaderChangedEvent(string previous, string @new, string resource, DateTimeOffset timestamp);
}

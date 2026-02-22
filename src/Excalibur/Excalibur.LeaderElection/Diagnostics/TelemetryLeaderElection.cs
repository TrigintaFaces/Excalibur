// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Diagnostics;

/// <summary>
/// Decorates an <see cref="ILeaderElection"/> with OpenTelemetry metrics and distributed tracing.
/// Records acquisition attempts, lease duration, and current leadership status.
/// </summary>
/// <remarks>
/// <para>
/// Follows the <see cref="Dispatch.Transport.Decorators.TelemetryTransportSender"/> pattern:
/// a wrapping decorator that instruments an existing <see cref="ILeaderElection"/> implementation
/// without requiring inheritance or modification of the underlying provider.
/// </para>
/// <para>
/// Three metrics are recorded:
/// <list type="bullet">
/// <item><description><c>excalibur.leaderelection.acquisitions</c> — Counter of lease acquisition attempts</description></item>
/// <item><description><c>excalibur.leaderelection.lease_duration</c> — Histogram of time the leader holds the lease in seconds</description></item>
/// <item><description><c>excalibur.leaderelection.is_leader</c> — ObservableGauge of current leadership status (0 or 1)</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class TelemetryLeaderElection : ILeaderElection, IAsyncDisposable
{
	private readonly ILeaderElection _inner;
	private readonly Meter _meter;
	private readonly Counter<long> _acquisitionsCounter;
	private readonly Histogram<double> _leaseDurationHistogram;
	private readonly ActivitySource _activitySource;
	private readonly string _providerName;
	private readonly TagCardinalityGuard _instanceGuard;

	private ValueStopwatch? _leaseStopwatch;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryLeaderElection"/> class.
	/// </summary>
	/// <param name="inner">The inner leader election implementation to decorate.</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="activitySource">The activity source for distributed tracing.</param>
	/// <param name="providerName">The provider name for tagging (e.g., "sqlserver", "redis").</param>
	public TelemetryLeaderElection(
		ILeaderElection inner,
		Meter meter,
		ActivitySource activitySource,
		string providerName)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_meter = meter ?? throw new ArgumentNullException(nameof(meter));
		_activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
		_providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));
		_instanceGuard = new TagCardinalityGuard(maxCardinality: 100);

		_acquisitionsCounter = meter.CreateCounter<long>(
			LeaderElectionTelemetryConstants.MetricNames.Acquisitions,
			"attempts",
			"Lease acquisition attempts");

		_leaseDurationHistogram = meter.CreateHistogram<double>(
			LeaderElectionTelemetryConstants.MetricNames.LeaseDuration,
			"s",
			"Duration leader holds lease in seconds");

		_ = meter.CreateObservableGauge(
			LeaderElectionTelemetryConstants.MetricNames.IsLeader,
			observeValue: () =>
			{
				var guardedInstance = _instanceGuard.Guard(_inner.CandidateId);
				var tags = new TagList
				{
					{ LeaderElectionTelemetryConstants.Tags.Instance, guardedInstance },
					{ LeaderElectionTelemetryConstants.Tags.Provider, _providerName },
				};
				return new Measurement<int>(_inner.IsLeader ? 1 : 0, tags);
			},
			unit: "{status}",
			description: "Current leadership status (0=follower, 1=leader)");

		// Subscribe to inner events
		_inner.BecameLeader += HandleBecameLeader;
		_inner.LostLeadership += HandleLostLeadership;
		_inner.LeaderChanged += HandleLeaderChanged;
	}

	/// <inheritdoc />
	public event EventHandler<LeaderElectionEventArgs>? BecameLeader;

	/// <inheritdoc />
	public event EventHandler<LeaderElectionEventArgs>? LostLeadership;

	/// <inheritdoc />
	public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

	/// <inheritdoc />
	public string CandidateId => _inner.CandidateId;

	/// <inheritdoc />
	public bool IsLeader => _inner.IsLeader;

	/// <inheritdoc />
	public string? CurrentLeaderId => _inner.CurrentLeaderId;

	/// <inheritdoc />
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("leader_election.start");
		activity?.SetTag(LeaderElectionTelemetryConstants.Tags.Instance, _instanceGuard.Guard(_inner.CandidateId));
		activity?.SetTag(LeaderElectionTelemetryConstants.Tags.Provider, _providerName);

		try
		{
			await _inner.StartAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		using var activity = _activitySource.StartActivity("leader_election.stop");
		activity?.SetTag(LeaderElectionTelemetryConstants.Tags.Instance, _instanceGuard.Guard(_inner.CandidateId));
		activity?.SetTag(LeaderElectionTelemetryConstants.Tags.Provider, _providerName);

		try
		{
			await _inner.StopAsync(cancellationToken).ConfigureAwait(false);

			// Record final lease duration if we were leader
			RecordLeaseDurationIfActive();
		}
		catch (Exception ex)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			throw;
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		_inner.BecameLeader -= HandleBecameLeader;
		_inner.LostLeadership -= HandleLostLeadership;
		_inner.LeaderChanged -= HandleLeaderChanged;

		_meter.Dispose();
		_activitySource.Dispose();

		if (_inner is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if (_inner is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	private void HandleBecameLeader(object? sender, LeaderElectionEventArgs e)
	{
		var guardedInstance = _instanceGuard.Guard(e.CandidateId);
		var tags = new TagList
		{
			{ LeaderElectionTelemetryConstants.Tags.Instance, guardedInstance },
			{ LeaderElectionTelemetryConstants.Tags.Provider, _providerName },
			{ LeaderElectionTelemetryConstants.Tags.Result, "acquired" },
		};
		_acquisitionsCounter.Add(1, tags);

		// Start tracking lease duration
		_leaseStopwatch = ValueStopwatch.StartNew();

		BecameLeader?.Invoke(this, e);
	}

	private void HandleLostLeadership(object? sender, LeaderElectionEventArgs e)
	{
		var guardedInstance = _instanceGuard.Guard(e.CandidateId);

		// Record lease duration
		RecordLeaseDurationIfActive();

		var tags = new TagList
		{
			{ LeaderElectionTelemetryConstants.Tags.Instance, guardedInstance },
			{ LeaderElectionTelemetryConstants.Tags.Provider, _providerName },
			{ LeaderElectionTelemetryConstants.Tags.Result, "lost" },
		};
		_acquisitionsCounter.Add(1, tags);

		LostLeadership?.Invoke(this, e);
	}

	private void HandleLeaderChanged(object? sender, LeaderChangedEventArgs e)
	{
		LeaderChanged?.Invoke(this, e);
	}

	private void RecordLeaseDurationIfActive()
	{
		if (_leaseStopwatch is { } leaseStopwatch)
		{
			var guardedInstance = _instanceGuard.Guard(_inner.CandidateId);
			var tags = new TagList
			{
				{ LeaderElectionTelemetryConstants.Tags.Instance, guardedInstance },
				{ LeaderElectionTelemetryConstants.Tags.Provider, _providerName },
			};
			_leaseDurationHistogram.Record(leaseStopwatch.Elapsed.TotalSeconds, tags);

			_leaseStopwatch = null;
		}
	}
}

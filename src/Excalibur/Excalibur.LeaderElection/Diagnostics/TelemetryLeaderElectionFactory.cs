// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.LeaderElection.Diagnostics;

/// <summary>
/// Decorates an <see cref="ILeaderElectionFactory"/> to automatically wrap created
/// instances with <see cref="TelemetryLeaderElection"/> instrumentation.
/// </summary>
public sealed class TelemetryLeaderElectionFactory : ILeaderElectionFactory, IDisposable
{
	private volatile bool _disposed;
	private readonly ILeaderElectionFactory _inner;
	private readonly Meter _meter;
	private readonly ActivitySource _activitySource;
	private readonly string _providerName;

	/// <summary>
	/// Initializes a new instance of the <see cref="TelemetryLeaderElectionFactory"/> class.
	/// </summary>
	/// <param name="inner">The inner factory to decorate.</param>
	/// <param name="meter">The meter for recording metrics.</param>
	/// <param name="activitySource">The activity source for distributed tracing.</param>
	/// <param name="providerName">The provider name tag (e.g., "SqlServer", "Redis").</param>
	public TelemetryLeaderElectionFactory(
		ILeaderElectionFactory inner,
		Meter meter,
		ActivitySource activitySource,
		string providerName)
	{
		ArgumentNullException.ThrowIfNull(inner);
		ArgumentNullException.ThrowIfNull(meter);
		ArgumentNullException.ThrowIfNull(activitySource);
		ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

		_inner = inner;
		_meter = meter;
		_activitySource = activitySource;
		_providerName = providerName;
	}

	/// <inheritdoc/>
	public ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		var inner = _inner.CreateElection(resourceName, candidateId);
		return new TelemetryLeaderElection(inner, _meter, _activitySource, _providerName);
	}

	/// <inheritdoc/>
	public IHealthBasedLeaderElection CreateHealthBasedElection(string resourceName, string? candidateId)
	{
		// Health-based elections are provider-specific; delegate without wrapping.
		// TelemetryLeaderElection only wraps ILeaderElection, not the health-based variant.
		return _inner.CreateHealthBasedElection(resourceName, candidateId);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		_meter.Dispose();
		_activitySource.Dispose();
	}
}

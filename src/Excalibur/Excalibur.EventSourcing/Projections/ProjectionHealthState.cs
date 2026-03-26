// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Shared state for projection health monitoring. Updated by projection
/// processors, read by <see cref="Health.ProjectionHealthCheck"/>.
/// </summary>
/// <remarks>
/// Registered as a singleton in DI. Uses Interlocked operations for
/// thread-safe reads/writes from the health check without locking.
/// </remarks>
internal sealed class ProjectionHealthState
{
	private long _lastInlineErrorTicks;
	private volatile string? _lastErrorProjectionType;
	private long _asyncLag;

	/// <summary>
	/// Gets the timestamp of the most recent inline projection error, or null.
	/// </summary>
	internal DateTimeOffset? LastInlineError
	{
		get
		{
			var ticks = Interlocked.Read(ref _lastInlineErrorTicks);
			return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
		}
	}

	/// <summary>
	/// Gets the projection type that caused the last error.
	/// </summary>
	internal string? LastErrorProjectionType => _lastErrorProjectionType;

	/// <summary>
	/// Gets or sets the current async projection lag in events.
	/// </summary>
	internal long AsyncLag
	{
		get => Interlocked.Read(ref _asyncLag);
		set => Interlocked.Exchange(ref _asyncLag, value);
	}

	/// <summary>
	/// Records an inline projection error.
	/// </summary>
	internal void RecordInlineError(string projectionType)
	{
		_lastErrorProjectionType = projectionType;
		Interlocked.Exchange(ref _lastInlineErrorTicks, DateTimeOffset.UtcNow.UtcTicks);
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Extension methods for <see cref="IGooglePubSubMetrics"/>.
/// </summary>
public static class GooglePubSubMetricsExtensions
{
	/// <summary>Records batch completion.</summary>
	public static void BatchCompleted(this IGooglePubSubMetrics metrics, int size, TimeSpan duration)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		if (metrics is IGooglePubSubMetricsAdmin admin)
		{
			admin.BatchCompleted(size, duration);
		}
	}

	/// <summary>Records connection creation.</summary>
	public static void ConnectionCreated(this IGooglePubSubMetrics metrics)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		if (metrics is IGooglePubSubMetricsAdmin admin)
		{
			admin.ConnectionCreated();
		}
	}

	/// <summary>Records connection closure.</summary>
	public static void ConnectionClosed(this IGooglePubSubMetrics metrics)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		if (metrics is IGooglePubSubMetricsAdmin admin)
		{
			admin.ConnectionClosed();
		}
	}

	/// <summary>Records flow control state.</summary>
	public static void RecordFlowControl(this IGooglePubSubMetrics metrics, int permits, int bytes)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		if (metrics is IGooglePubSubMetricsAdmin admin)
		{
			admin.RecordFlowControl(permits, bytes);
		}
	}
}

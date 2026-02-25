// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Statistics for long polling strategy performance.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct LongPollingStatistics
{
	/// <summary>
	/// Gets the total number of receive operations.
	/// </summary>
	/// <value>The current <see cref="TotalReceives"/> value.</value>
	public long TotalReceives { get; init; }

	/// <summary>
	/// Gets the total number of messages received.
	/// </summary>
	/// <value>The current <see cref="TotalMessages"/> value.</value>
	public long TotalMessages { get; init; }

	/// <summary>
	/// Gets the number of empty receives (no messages).
	/// </summary>
	/// <value>The current <see cref="EmptyReceives"/> value.</value>
	public long EmptyReceives { get; init; }

	/// <summary>
	/// Gets the average messages per receive.
	/// </summary>
	/// <value>
	/// The average messages per receive.
	/// </value>
	public double AverageMessagesPerReceive => TotalReceives > 0 ? (double)TotalMessages / TotalReceives : 0;

	/// <summary>
	/// Gets the empty receive rate.
	/// </summary>
	/// <value>
	/// The empty receive rate.
	/// </value>
	public double EmptyReceiveRate => TotalReceives > 0 ? (double)EmptyReceives / TotalReceives : 0;

	/// <summary>
	/// Gets the current load factor.
	/// </summary>
	/// <value>The current <see cref="CurrentLoadFactor"/> value.</value>
	public double CurrentLoadFactor { get; init; }

	/// <summary>
	/// Gets the current recommended wait time.
	/// </summary>
	/// <value>The current <see cref="CurrentWaitTime"/> value.</value>
	public TimeSpan CurrentWaitTime { get; init; }

	/// <summary>
	/// Gets the total API calls saved through long polling.
	/// </summary>
	/// <value>The current <see cref="ApiCallsSaved"/> value.</value>
	public long ApiCallsSaved { get; init; }

	/// <summary>
	/// Gets the timestamp of the last receive operation.
	/// </summary>
	/// <value>The current <see cref="LastReceiveTime"/> value.</value>
	public DateTimeOffset LastReceiveTime { get; init; }
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Messaging;

/// <summary>
/// Dispatcher performance statistics.
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly struct DispatcherStatistics : IEquatable<DispatcherStatistics>
{
	/// <summary>
	/// Gets the total number of messages processed by the dispatcher.
	/// </summary>
	/// <value>The current <see cref="TotalMessages"/> value.</value>
	public long TotalMessages { get; init; }

	/// <summary>
	/// Gets the total number of message batches processed by the dispatcher.
	/// </summary>
	/// <value>The current <see cref="TotalBatches"/> value.</value>
	public long TotalBatches { get; init; }

	/// <summary>
	/// Gets the average number of messages per batch processed.
	/// </summary>
	/// <value>The current <see cref="AverageBatchSize"/> value.</value>
	public double AverageBatchSize { get; init; }

	/// <summary>
	/// Gets the message processing throughput in messages per second.
	/// </summary>
	/// <value>The current <see cref="Throughput"/> value.</value>
	public double Throughput { get; init; }

	/// <summary>
	/// Gets the current depth of the message queue.
	/// </summary>
	/// <value>The current <see cref="QueueDepth"/> value.</value>
	public int QueueDepth { get; init; }

	/// <summary>
	/// Gets the total elapsed time in milliseconds for the measured period.
	/// </summary>
	/// <value>The current <see cref="ElapsedMilliseconds"/> value.</value>
	public double ElapsedMilliseconds { get; init; }

	/// <summary>
	/// Determines whether two statistics instances are equal.
	/// </summary>
	public static bool operator ==(DispatcherStatistics left, DispatcherStatistics right) => left.Equals(right);

	/// <summary>
	/// Determines whether two statistics instances are not equal.
	/// </summary>
	public static bool operator !=(DispatcherStatistics left, DispatcherStatistics right) => !left.Equals(right);

	/// <summary>
	/// Determines whether the specified statistics is equal to the current statistics.
	/// </summary>
	public bool Equals(DispatcherStatistics other) =>
		TotalMessages == other.TotalMessages &&
		TotalBatches == other.TotalBatches &&
		AverageBatchSize.Equals(other.AverageBatchSize) &&
		Throughput.Equals(other.Throughput) &&
		QueueDepth == other.QueueDepth &&
		ElapsedMilliseconds.Equals(other.ElapsedMilliseconds);

	/// <summary>
	/// Determines whether the specified object is equal to the current statistics.
	/// </summary>
	public override bool Equals(object? obj) => obj is DispatcherStatistics other && Equals(other);

	/// <summary>
	/// Returns the hash code for this statistics.
	/// </summary>
	public override int GetHashCode() =>
		HashCode.Combine(TotalMessages, TotalBatches, AverageBatchSize, Throughput, QueueDepth, ElapsedMilliseconds);
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Timer for measuring durations and recording to a histogram. Zero-allocation timer using ValueStopwatch for optimal performance.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Value-Type Disposal Warning:</strong> This is a <c>readonly struct</c> implementing
/// <see cref="IDisposable"/>. Value-type semantics apply:
/// </para>
/// <list type="bullet">
/// <item><description>Copying this struct creates a shallow copy sharing the same underlying histogram reference.</description></item>
/// <item><description>Disposing any copy records the elapsed time to the histogram.</description></item>
/// <item><description>Multiple disposals of copies will record multiple times, potentially corrupting histogram data.</description></item>
/// </list>
/// <para>
/// <strong>Best Practice:</strong> Use with <c>using</c> statement and avoid copying:
/// <code>
/// using var timer = histogram.StartTimer();
/// // Perform operation to measure
/// // Timer automatically records duration on dispose
/// </code>
/// </para>
/// </remarks>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct HistogramTimer(ValueHistogram histogram) : IDisposable, IEquatable<HistogramTimer>
{
	private readonly ValueHistogram _histogram = histogram;
	private readonly long _startTicks = ValueStopwatch.GetTimestamp();

	/// <summary>
	/// Determines whether two histogram timers are equal.
	/// </summary>
	/// <param name="left"> The first histogram timer to compare. </param>
	/// <param name="right"> The second histogram timer to compare. </param>
	/// <returns> true if the histogram timers are equal; otherwise, false. </returns>
	public static bool operator ==(HistogramTimer left, HistogramTimer right) => left.Equals(right);

	/// <summary>
	/// Determines whether two histogram timers are not equal.
	/// </summary>
	/// <param name="left"> The first histogram timer to compare. </param>
	/// <param name="right"> The second histogram timer to compare. </param>
	/// <returns> true if the histogram timers are not equal; otherwise, false. </returns>
	public static bool operator !=(HistogramTimer left, HistogramTimer right) => !left.Equals(right);

	/// <summary>
	/// Stops the timer and records the elapsed time to the histogram. The elapsed time is measured in microseconds and automatically
	/// recorded when the timer is disposed, enabling convenient using statement patterns.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Dispose()
	{
		var elapsedTicks = ValueStopwatch.GetTimestamp() - _startTicks;
		var elapsedMicroseconds = elapsedTicks * 1_000_000 / ValueStopwatch.GetFrequency();
		_histogram.Record(elapsedMicroseconds);
	}

	/// <summary>
	/// Determines whether the specified histogram timer is equal to the current histogram timer.
	/// </summary>
	/// <param name="other"> The histogram timer to compare with the current histogram timer. </param>
	/// <returns> true if the specified histogram timer is equal to the current histogram timer; otherwise, false. </returns>
	public bool Equals(HistogramTimer other) => ReferenceEquals(_histogram, other._histogram) && _startTicks == other._startTicks;

	/// <summary>
	/// Determines whether the specified object is equal to the current histogram timer.
	/// </summary>
	/// <param name="obj"> The object to compare with the current histogram timer. </param>
	/// <returns> true if the specified object is equal to the current histogram timer; otherwise, false. </returns>
	public override bool Equals(object? obj) => obj is HistogramTimer other && Equals(other);

	/// <summary>
	/// Returns the hash code for this histogram timer.
	/// </summary>
	/// <returns> A hash code for the current histogram timer. </returns>
	public override int GetHashCode() => HashCode.Combine(_histogram?.GetHashCode() ?? 0, _startTicks);
}

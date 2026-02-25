// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Extension methods for histogram timing.
/// </summary>
public static class HistogramExtensions
{
	/// <summary>
	/// Starts a timer that will record the duration when disposed.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static HistogramTimer StartTimer(this ValueHistogram histogram) => new(histogram);

	/// <summary>
	/// Records the execution time of an action.
	/// </summary>
	public static void Time(this ValueHistogram histogram, Action action)
	{
		using (histogram.StartTimer())
		{
			action();
		}
	}

	/// <summary>
	/// Records the execution time of a function.
	/// </summary>
	public static T Time<T>(this ValueHistogram histogram, Func<T> func)
	{
		using (histogram.StartTimer())
		{
			return func();
		}
	}

	/// <summary>
	/// Records the execution time of an async operation.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public static async Task TimeAsync(this ValueHistogram histogram, Func<Task> func)
	{
		using (histogram.StartTimer())
		{
			await func().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Records the execution time of an async function.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public static async Task<T> TimeAsync<T>(this ValueHistogram histogram, Func<Task<T>> func)
	{
		using (histogram.StartTimer())
		{
			return await func().ConfigureAwait(false);
		}
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Abstractions.Diagnostics;

/// <summary>
/// A lightweight, high-performance value type stopwatch that measures elapsed time with high precision.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="Stopwatch" />, <see cref="ValueStopwatch" /> is immutable and does not require calling Stop(). It calculates elapsed
/// time based on the high-resolution performance counter provided by <see cref="Stopwatch.GetTimestamp" /> and the .NET 9 helper <see cref="Stopwatch.GetElapsedTime(long)" />.
/// </para>
/// <para>
/// This implementation includes additional features such as comparison operators, convenient elapsed time properties, and improved error
/// handling for uninitialized instances.
/// </para>
/// </remarks>
public readonly struct ValueStopwatch : IEquatable<ValueStopwatch>, IComparable<ValueStopwatch>, IComparable
{
	/// <summary>
	/// A sentinel value used to indicate an uninitialized stopwatch.
	/// </summary>
	private const long Uninitialized = long.MinValue;

	/// <summary>
	/// The timestamp when the stopwatch was started, as returned by <see cref="Stopwatch.GetTimestamp" />.
	/// </summary>
	private readonly long _start;

	/// <summary>
	/// Initializes a new instance of the <see cref="ValueStopwatch" /> struct.
	/// </summary>
	/// <param name="start"> The timestamp at which the stopwatch started. </param>
	private ValueStopwatch(long start) => _start = start;

	/// <summary>
	/// Gets an empty <see cref="ValueStopwatch" /> instance that has not been started.
	/// </summary>
	/// <value> An uninitialized stopwatch instance. </value>
	public static ValueStopwatch Empty { get; } = new(Uninitialized);

	/// <summary>
	/// Gets a value indicating whether this stopwatch instance has been started.
	/// </summary>
	/// <value> <c> true </c> if the stopwatch has been started; otherwise, <c> false </c>. </value>
	public bool IsActive => _start != Uninitialized;

	/// <summary>
	/// Gets the elapsed time since the stopwatch was started.
	/// </summary>
	/// <remarks> The elapsed time is calculated dynamically each time this property is accessed. </remarks>
	/// <value> A <see cref="TimeSpan" /> representing the time elapsed since the stopwatch was started. </value>
	/// <exception cref="InvalidOperationException"> The stopwatch has not been started. </exception>
	public TimeSpan Elapsed
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			ThrowIfNotStarted();
			return Stopwatch.GetElapsedTime(_start);
		}
	}

	/// <summary>
	/// Gets the elapsed time in ticks since the stopwatch was started.
	/// </summary>
	/// <value> The elapsed time in ticks. </value>
	/// <exception cref="InvalidOperationException"> The stopwatch has not been started. </exception>
	public long ElapsedTicks
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			ThrowIfNotStarted();
			return Elapsed.Ticks;
		}
	}

	/// <summary>
	/// Gets elapsed time in microseconds.
	/// </summary>
	public long ElapsedMicroseconds
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			ThrowIfNotStarted();

			// 1 tick = 100 ns â†’ 10 ticks = 1 Âµs
			return Elapsed.Ticks / 10;
		}
	}

	/// <summary>
	/// Gets the elapsed time in milliseconds since the stopwatch was started.
	/// </summary>
	/// <value> The elapsed time in milliseconds. </value>
	/// <exception cref="InvalidOperationException"> The stopwatch has not been started. </exception>
	public double ElapsedMilliseconds
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			ThrowIfNotStarted();
			return Elapsed.TotalMilliseconds;
		}
	}

	/// <summary>
	/// Determines whether two <see cref="ValueStopwatch" /> instances are equal.
	/// </summary>
	/// <param name="left"> The first instance. </param>
	/// <param name="right"> The second instance. </param>
	/// <returns> <c> true </c> if the instances are equal; otherwise, <c> false </c>. </returns>
	public static bool operator ==(ValueStopwatch left, ValueStopwatch right) => left.Equals(right);

	/// <summary>
	/// Determines whether two <see cref="ValueStopwatch" /> instances are not equal.
	/// </summary>
	/// <param name="left"> The first instance. </param>
	/// <param name="right"> The second instance. </param>
	/// <returns> <c> true </c> if the instances are not equal; otherwise, <c> false </c>. </returns>
	public static bool operator !=(ValueStopwatch left, ValueStopwatch right) => !left.Equals(right);

	/// <summary>
	/// Determines whether the left instance started before the right instance.
	/// </summary>
	/// <param name="left"> The first instance. </param>
	/// <param name="right"> The second instance. </param>
	/// <returns> <c> true </c> if left started before right; otherwise, <c> false </c>. </returns>
	public static bool operator <(ValueStopwatch left, ValueStopwatch right) => left.CompareTo(right) < 0;

	/// <summary>
	/// Determines whether the left instance started after the right instance.
	/// </summary>
	/// <param name="left"> The first instance. </param>
	/// <param name="right"> The second instance. </param>
	/// <returns> <c> true </c> if left started after right; otherwise, <c> false </c>. </returns>
	public static bool operator >(ValueStopwatch left, ValueStopwatch right) => left.CompareTo(right) > 0;

	/// <summary>
	/// Determines whether the left instance started before or at the same time as the right instance.
	/// </summary>
	/// <param name="left"> The first instance. </param>
	/// <param name="right"> The second instance. </param>
	/// <returns> <c> true </c> if left started before or at the same time as right; otherwise, <c> false </c>. </returns>
	public static bool operator <=(ValueStopwatch left, ValueStopwatch right) => left.CompareTo(right) <= 0;

	/// <summary>
	/// Determines whether the left instance started after or at the same time as the right instance.
	/// </summary>
	/// <param name="left"> The first instance. </param>
	/// <param name="right"> The second instance. </param>
	/// <returns> <c> true </c> if left started after or at the same time as right; otherwise, <c> false </c>. </returns>
	public static bool operator >=(ValueStopwatch left, ValueStopwatch right) => left.CompareTo(right) >= 0;

	/// <summary>
	/// Creates and starts a new instance of the <see cref="ValueStopwatch" />.
	/// </summary>
	/// <returns> A new <see cref="ValueStopwatch" /> instance with the current timestamp as the starting point. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());

	/// <summary>
	/// Restarts the stopwatch with a new timestamp.
	/// </summary>
	/// <returns> A new <see cref="ValueStopwatch" /> instance started at the current time. </returns>
	/// <remarks> Since <see cref="ValueStopwatch" /> is immutable, this method returns a new instance. </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ValueStopwatch Restart() => StartNew();

	/// <summary>
	/// Creates a new <see cref="ValueStopwatch" /> instance from a specific starting timestamp.
	/// </summary>
	/// <param name="timestamp"> The starting timestamp. </param>
	/// <returns> A new <see cref="ValueStopwatch" /> instance. </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timestamp" /> is negative and not <see cref="Uninitialized" />. </exception>
	public static ValueStopwatch FromTimestamp(long timestamp)
	{
		if (timestamp is < 0 and not Uninitialized)
		{
			throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, ErrorMessages.StartTimestampCannotBeNegative);
		}

		return new ValueStopwatch(timestamp);
	}

	/// <summary>
	/// Measures the execution time of an action.
	/// </summary>
	/// <param name="action"> The action to measure. </param>
	/// <returns> The elapsed time for the action execution. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="action" /> is <c> null </c>. </exception>
	public static TimeSpan Time(Action action)
	{
		ArgumentNullException.ThrowIfNull(action);

		var sw = StartNew();
		action();
		return sw.Elapsed;
	}

	/// <summary>
	/// Measures the execution time of an asynchronous operation.
	/// </summary>
	/// <param name="operation"> The asynchronous operation to measure. </param>
	/// <returns> The elapsed time for the operation execution. </returns>
	/// <exception cref="ArgumentNullException"> <paramref name="operation" /> is <c> null </c>. </exception>
	public static async Task<TimeSpan> TimeAsync(Func<Task> operation)
	{
		ArgumentNullException.ThrowIfNull(operation);

		var sw = StartNew();
		await operation().ConfigureAwait(false);
		return sw.Elapsed;
	}

	/// <summary>
	/// Gets the current timestamp from the high-resolution performance counter.
	/// </summary>
	/// <returns> The current timestamp. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static long GetTimestamp() => Stopwatch.GetTimestamp();

	/// <summary>
	/// Gets the frequency of the performance counter.
	/// </summary>
	/// <returns> The frequency of the performance counter in ticks per second. </returns>
	public static long GetFrequency() => Stopwatch.Frequency;

	/// <summary>
	/// Returns the elapsed time since start. Provided for source compatibility with callers that used an instance method.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TimeSpan GetElapsedTime() => Elapsed;

	/// <summary>
	/// Gets a string representation of the elapsed time.
	/// </summary>
	/// <returns> A string representation of the elapsed time, or "[Not Started]" if the stopwatch hasn't been started. </returns>
	public override string ToString()
	{
		if (!IsActive)
		{
			return "[Not Started]";
		}

		try
		{
			var elapsed = Elapsed;
			if (elapsed.TotalDays >= 1)
			{
				return $"{elapsed.TotalDays:F2} days";
			}

			if (elapsed.TotalHours >= 1)
			{
				return $"{elapsed.TotalHours:F2} hours";
			}

			if (elapsed.TotalMinutes >= 1)
			{
				return $"{elapsed.TotalMinutes:F2} minutes";
			}

			if (elapsed.TotalSeconds >= 1)
			{
				return $"{elapsed.TotalSeconds:F3} seconds";
			}

			return $"{elapsed.TotalMilliseconds:F3} ms";
		}
		catch (OverflowException)
		{
			// Elapsed time calculation overflowed - return a fallback value
			return "[Invalid]";
		}
		catch (InvalidOperationException)
		{
			// Uninitialized stopwatch - return a fallback value
			return "[Invalid]";
		}
	}

	// Equality & comparison

	/// <summary>
	/// Determines whether the specified <see cref="ValueStopwatch" /> is equal to the current instance.
	/// </summary>
	/// <param name="other"> The instance to compare with. </param>
	/// <returns> <c> true </c> if equal; otherwise, <c> false </c>. </returns>
	public bool Equals(ValueStopwatch other) => _start == other._start;

	/// <summary>
	/// Determines whether the specified object is equal to the current instance.
	/// </summary>
	/// <param name="obj"> The object to compare with. </param>
	/// <returns> <c> true </c> if equal; otherwise, <c> false </c>. </returns>
	public override bool Equals([NotNullWhen(true)] object? obj) => obj is ValueStopwatch other && Equals(other);

	/// <summary>
	/// Returns the hash code for the current instance.
	/// </summary>
	/// <returns> The hash code. </returns>
	public override int GetHashCode() => _start.GetHashCode();

	/// <summary>
	/// Compares the current instance with another <see cref="ValueStopwatch" />.
	/// </summary>
	/// <param name="other"> The instance to compare with. </param>
	/// <returns> A value indicating the relative order of the instances based on their start times. </returns>
	public int CompareTo(ValueStopwatch other) => _start.CompareTo(other._start);

	/// <summary>
	/// Compares the current instance with another object.
	/// </summary>
	/// <param name="obj"> The object to compare with. </param>
	/// <returns> A value indicating the relative order of the instances based on their start times. </returns>
	/// <exception cref="ArgumentException"> <paramref name="obj" /> is not a <see cref="ValueStopwatch" />. </exception>
	public int CompareTo(object? obj)
	{
		if (obj is null)
		{
			return 1;
		}

		if (obj is not ValueStopwatch other)
		{
			throw new ArgumentException(ErrorMessages.ObjectMustBeOfTypeValueStopwatch, nameof(obj));
		}

		return CompareTo(other);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ThrowIfNotStarted()
	{
		if (!IsActive)
		{
			throw new InvalidOperationException(ErrorMessages.ValueStopwatchNotStarted);
		}
	}
}

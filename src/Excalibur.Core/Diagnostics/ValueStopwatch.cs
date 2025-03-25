using System.Diagnostics;

namespace Excalibur.Core.Diagnostics;

/// <summary>
///     A lightweight high-performance value type stopwatch that measures elapsed time with high precision.
/// </summary>
/// <remarks>
///     Unlike <see cref="Stopwatch" />, <see cref="ValueStopwatch" /> is immutable and does not require calling <c> Stop() </c>. It
///     calculates elapsed time based on the high-resolution performance counter provided by <see cref="Stopwatch.GetTimestamp" />.
/// </remarks>
public readonly struct ValueStopwatch : IEquatable<ValueStopwatch>
{
	/// <summary>
	///     The factor to convert ticks from the performance counter frequency to <see cref="TimeSpan.Ticks" />.
	/// </summary>
	private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

	/// <summary>
	///     The timestamp when the stopwatch was started, as returned by <see cref="Stopwatch.GetTimestamp" />.
	/// </summary>
	private readonly long _startTimestamp;

	/// <summary>
	///     Initializes a new instance of the <see cref="ValueStopwatch" /> struct.
	/// </summary>
	/// <param name="startTimestamp"> The timestamp at which the stopwatch started. </param>
	private ValueStopwatch(long startTimestamp) => _startTimestamp = startTimestamp;

	/// <summary>
	///     Gets the elapsed time since the stopwatch was started.
	/// </summary>
	/// <remarks>
	///     The elapsed time is calculated dynamically each time this property is accessed, based on the current timestamp and the starting timestamp.
	/// </remarks>
	/// <value> A <see cref="TimeSpan" /> representing the time elapsed since the stopwatch was started. </value>
	public TimeSpan Elapsed => TimeSpan.FromTicks(GetElapsedTicks());

	/// <summary>
	///     Creates and starts a new instance of the <see cref="ValueStopwatch" />.
	/// </summary>
	/// <returns> A new <see cref="ValueStopwatch" /> instance with the current timestamp as the starting point. </returns>
	public static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());

	/// <summary>
	///     Determines whether two <see cref="ValueStopwatch" /> instances are equal.
	/// </summary>
	/// <param name="left"> The first <see cref="ValueStopwatch" /> instance. </param>
	/// <param name="right"> The second <see cref="ValueStopwatch" /> instance. </param>
	/// <returns> <c> true </c> if the instances are equal; otherwise, <c> false </c>. </returns>
	public static bool operator ==(ValueStopwatch left, ValueStopwatch right) => left.Equals(right);

	/// <summary>
	///     Determines whether two <see cref="ValueStopwatch" /> instances are not equal.
	/// </summary>
	/// <param name="left"> The first <see cref="ValueStopwatch" /> instance. </param>
	/// <param name="right"> The second <see cref="ValueStopwatch" /> instance. </param>
	/// <returns> <c> true </c> if the instances are not equal; otherwise, <c> false </c>. </returns>
	public static bool operator !=(ValueStopwatch left, ValueStopwatch right) => !left.Equals(right);

	/// <summary>
	///     Determines whether the specified <see cref="ValueStopwatch" /> is equal to the current instance.
	/// </summary>
	/// <param name="other"> The <see cref="ValueStopwatch" /> to compare with the current instance. </param>
	/// <returns>
	///     <c> true </c> if the specified <see cref="ValueStopwatch" /> is equal to the current instance; otherwise, <c> false </c>.
	/// </returns>
	public bool Equals(ValueStopwatch other) => _startTimestamp == other._startTimestamp;

	/// <summary>
	///     Determines whether the specified object is equal to the current instance.
	/// </summary>
	/// <param name="obj"> The object to compare with the current instance. </param>
	/// <returns> <c> true </c> if the specified object is equal to the current instance; otherwise, <c> false </c>. </returns>
	public override bool Equals(object? obj) => obj is ValueStopwatch other && Equals(other);

	/// <summary>
	///     Returns the hash code for the current <see cref="ValueStopwatch" /> instance.
	/// </summary>
	/// <returns> The hash code for the current instance. </returns>
	public override int GetHashCode() => _startTimestamp.GetHashCode();

	/// <summary>
	///     Calculates the number of ticks that have elapsed since the stopwatch was started.
	/// </summary>
	/// <returns> The number of elapsed ticks. </returns>
	private long GetElapsedTicks()
	{
		var delta = Stopwatch.GetTimestamp() - _startTimestamp;
		return (long)(delta * TimestampToTicks);
	}
}

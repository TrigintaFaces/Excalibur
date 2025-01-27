using System.Diagnostics;

namespace Excalibur.Core.Diagnostics;

/// <summary>
///     A lightweight structure for measuring elapsed time.
/// </summary>
/// <remarks>
///     Unlike <see cref="Stopwatch" />, <see cref="ValueStopwatch" /> is immutable and does not require calling <c> Stop() </c>. It
///     calculates elapsed time based on the high-resolution performance counter provided by <see cref="Stopwatch.GetTimestamp" />.
/// </remarks>
public readonly struct ValueStopwatch
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
	///     Calculates the number of ticks that have elapsed since the stopwatch was started.
	/// </summary>
	/// <returns> The number of elapsed ticks. </returns>
	private long GetElapsedTicks()
	{
		var delta = Stopwatch.GetTimestamp() - _startTimestamp;
		return (long)(delta * TimestampToTicks);
	}
}

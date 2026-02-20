// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Represents units of measurement for metrics.
/// </summary>
public enum MetricUnit
{
	/// <summary>
	/// No unit specified.
	/// </summary>
	None = 0,

	/// <summary>
	/// Count.
	/// </summary>
	Count = 1,

	/// <summary>
	/// Bytes.
	/// </summary>
	Bytes = 2,

	/// <summary>
	/// Kilobytes.
	/// </summary>
	Kilobytes = 3,

	/// <summary>
	/// Megabytes.
	/// </summary>
	Megabytes = 4,

	/// <summary>
	/// Gigabytes.
	/// </summary>
	Gigabytes = 5,

	/// <summary>
	/// Terabytes.
	/// </summary>
	Terabytes = 6,

	/// <summary>
	/// Bits.
	/// </summary>
	Bits = 7,

	/// <summary>
	/// Kilobits.
	/// </summary>
	Kilobits = 8,

	/// <summary>
	/// Megabits.
	/// </summary>
	Megabits = 9,

	/// <summary>
	/// Gigabits.
	/// </summary>
	Gigabits = 10,

	/// <summary>
	/// Terabits.
	/// </summary>
	Terabits = 11,

	/// <summary>
	/// Percent.
	/// </summary>
	Percent = 12,

	/// <summary>
	/// Seconds.
	/// </summary>
	Seconds = 13,

	/// <summary>
	/// Microseconds.
	/// </summary>
	Microseconds = 14,

	/// <summary>
	/// Milliseconds.
	/// </summary>
	Milliseconds = 15,

	/// <summary>
	/// Bytes per second.
	/// </summary>
	BytesPerSecond = 16,

	/// <summary>
	/// Kilobytes per second.
	/// </summary>
	KilobytesPerSecond = 17,

	/// <summary>
	/// Megabytes per second.
	/// </summary>
	MegabytesPerSecond = 18,

	/// <summary>
	/// Gigabytes per second.
	/// </summary>
	GigabytesPerSecond = 19,

	/// <summary>
	/// Terabytes per second.
	/// </summary>
	TerabytesPerSecond = 20,

	/// <summary>
	/// Bits per second.
	/// </summary>
	BitsPerSecond = 21,

	/// <summary>
	/// Kilobits per second.
	/// </summary>
	KilobitsPerSecond = 22,

	/// <summary>
	/// Megabits per second.
	/// </summary>
	MegabitsPerSecond = 23,

	/// <summary>
	/// Gigabits per second.
	/// </summary>
	GigabitsPerSecond = 24,

	/// <summary>
	/// Terabits per second.
	/// </summary>
	TerabitsPerSecond = 25,

	/// <summary>
	/// Count per second.
	/// </summary>
	CountPerSecond = 26,
}

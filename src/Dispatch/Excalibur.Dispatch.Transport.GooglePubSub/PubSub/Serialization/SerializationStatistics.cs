// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Statistics about serialization operations.
/// </summary>
public sealed class SerializationStatistics
{
	/// <summary>
	/// Gets or sets the amount of memory in use by array pools.
	/// </summary>
	/// <value>
	/// The amount of memory in use by array pools.
	/// </value>
	public long ArrayPoolInUse { get; set; }

	/// <summary>
	/// Gets or sets the total messages serialized.
	/// </summary>
	/// <value>
	/// The total messages serialized.
	/// </value>
	public long TotalMessagesSerialized { get; set; }

	/// <summary>
	/// Gets or sets the total bytes serialized.
	/// </summary>
	/// <value>
	/// The total bytes serialized.
	/// </value>
	public long TotalBytesSerialized { get; set; }

	/// <summary>
	/// Gets or sets the average serialization time in microseconds.
	/// </summary>
	/// <value>
	/// The average serialization time in microseconds.
	/// </value>
	public double AverageSerializationTimeMicros { get; set; }
}

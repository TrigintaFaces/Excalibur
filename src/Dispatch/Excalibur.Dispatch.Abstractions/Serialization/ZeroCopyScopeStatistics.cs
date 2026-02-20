// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Serialization;

/// <summary>
/// Statistics for a zero-copy deserializer scope.
/// </summary>
public sealed class ZeroCopyScopeStatistics
{
	/// <summary>
	/// Gets total number of deserializations performed in this scope.
	/// </summary>
	/// <value> The current <see cref="DeserializationCount" /> value. </value>
	public long DeserializationCount { get; init; }

	/// <summary>
	/// Gets total bytes processed in this scope.
	/// </summary>
	/// <value> The current <see cref="BytesProcessed" /> value. </value>
	public long BytesProcessed { get; init; }

	/// <summary>
	/// Gets number of buffer allocations avoided through reuse.
	/// </summary>
	/// <value> The current <see cref="BufferReuses" /> value. </value>
	public long BufferReuses { get; init; }

	/// <summary>
	/// Gets current buffer size being used.
	/// </summary>
	/// <value> The current <see cref="CurrentBufferSize" /> value. </value>
	public int CurrentBufferSize { get; init; }
}

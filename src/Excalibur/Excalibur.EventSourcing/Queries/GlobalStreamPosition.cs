// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing.Queries;

/// <summary>
/// Represents a position in the global event stream, combining an ordinal position
/// with a timestamp for correlation.
/// </summary>
/// <param name="Position">The ordinal position in the global stream.</param>
/// <param name="Timestamp">The timestamp associated with this position.</param>
/// <remarks>
/// <para>
/// The position value is provider-specific. For SQL-based stores, it typically maps
/// to a global sequence number. For cloud-native stores, it may map to a change feed token.
/// </para>
/// </remarks>
public sealed record GlobalStreamPosition(long Position, DateTimeOffset Timestamp)
{
	/// <summary>
	/// Gets a position representing the start of the global stream.
	/// </summary>
	public static GlobalStreamPosition Start { get; } = new(0, DateTimeOffset.MinValue);
}

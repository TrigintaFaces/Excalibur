// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Queries;

/// <summary>
/// Represents a position in the global event stream, combining an ordinal position
/// with a timestamp for correlation.
/// </summary>
/// <param name="Position">
/// The LAST POSITION ALREADY DELIVERED -- not the next one to read. A read from this position is
/// EXCLUSIVE of it, so a consumer stores the position of the last event it handled and passes that back
/// verbatim.
/// <para>
/// This is stated because the alternative convention (store last-delivered, read from last-delivered
/// PLUS ONE) was in use at six call sites and got it wrong in two different ways within one file. There
/// is now exactly one rule: the cursor IS the last delivered position, every query is exclusive, and no
/// caller adds or subtracts anything.
/// </para>
/// </param>
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
	/// Gets a position representing the start of the global stream: before any event has been delivered.
	/// </summary>
	/// <remarks>
	/// Position 0 is the correct "nothing delivered yet" value because allocated positions begin at 1, so
	/// an exclusive read from 0 returns the first event rather than skipping it.
	/// </remarks>
	public static GlobalStreamPosition Start { get; } = new(0, DateTimeOffset.MinValue);
}

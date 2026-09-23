// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// The single-row result of the mark-sent mutation: the effective fence high-water after the
/// compare-and-swap, the guarded update's own rowcount, and whether the row exists at all.
/// </summary>
/// <remarks>
/// <para>
/// All three values are produced by ONE statement inside the mutating transaction, under the fence
/// row lock. That is the whole point of the type: classifying a zero-rowcount result against a
/// high-water read in an earlier, untransacted round trip cannot distinguish a fencing refusal from
/// a not-found, because a fresher tenure can advance the fence between the two round trips.
/// </para>
/// <para>
/// This mirrors <see cref="FencedClaimMutationResult"/>, which the fenced failure and dead-letter
/// paths already use. It differs in one respect only: <see cref="HighWaterToken"/> is nullable,
/// because mark-sent has an UNFENCED overload where no token is presented and no high-water exists.
/// A non-nullable field would force a sentinel, and a sentinel in a fencing comparison is how a
/// refusal becomes indistinguishable from a success.
/// </para>
/// </remarks>
internal sealed class MarkSentMutationResult
{
	/// <summary>
	/// Gets or sets the fence high-water after the compare-and-swap, or <see langword="null"/> when no
	/// fencing token was presented.
	/// </summary>
	public long? HighWaterToken { get; set; }

	/// <summary>
	/// Gets or sets the number of rows the guarded update affected.
	/// </summary>
	public int UpdatedCount { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the message row exists, evaluated inside the same
	/// transaction as the mutation rather than in a follow-up probe.
	/// </summary>
	public bool RowExists { get; set; }
}

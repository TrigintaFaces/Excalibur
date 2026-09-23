// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Outbox;

/// <summary>
/// Maps what a fenced completion statement observed onto the outcome its caller acts on.
/// </summary>
/// <remarks>
/// <para>
/// One classifier for every relational outbox store. Each store used to carry its own copy of the same
/// ternary, and all of them answered <see cref="OutboxCompletionOutcome.ClaimLost"/> for a message that was
/// already terminal, because none of them was given the fact that separates the two.
/// </para>
/// <para>
/// <b>The order is load-bearing.</b> A refused fence is judged first, because it is the only outcome that
/// means "stop draining entirely"; a superseded tenure also fails every other term, so judging it later
/// reports a tenure-wide refusal as a single lost message. Then applied, then absent, then terminal, and
/// only then a lost claim: a terminal message is owned by nobody, so reporting it as re-claimed describes a
/// decision nobody made.
/// </para>
/// </remarks>
internal static class FencedCompletionClassifier
{
	/// <summary>
	/// Classifies one fenced completion.
	/// </summary>
	/// <param name="highWaterToken">The scope's high-water token after the fence compare-and-swap.</param>
	/// <param name="presentedToken">The token the caller presented.</param>
	/// <param name="updatedCount">The rows the guarded mutation applied to.</param>
	/// <param name="rowExists">Whether the addressed row was present when the statement ran.</param>
	/// <param name="isTerminal">
	/// Whether the addressed row was present in a terminal status. A store whose terminal transitions remove
	/// the row passes <see langword="false"/>: such a row is never both present and terminal, and for that
	/// store an absent row is the accurate report of a terminal message.
	/// </param>
	/// <returns>The outcome the caller acts on.</returns>
	public static OutboxCompletionOutcome Classify(
		long highWaterToken,
		long presentedToken,
		int updatedCount,
		bool rowExists,
		bool isTerminal) =>
		highWaterToken != presentedToken
			? OutboxCompletionOutcome.FenceRefused
			: updatedCount > 0
				? OutboxCompletionOutcome.Applied
				: !rowExists
					? OutboxCompletionOutcome.MessageNotFound
					: isTerminal
						? OutboxCompletionOutcome.AlreadyTerminal
						: OutboxCompletionOutcome.ClaimLost;
}

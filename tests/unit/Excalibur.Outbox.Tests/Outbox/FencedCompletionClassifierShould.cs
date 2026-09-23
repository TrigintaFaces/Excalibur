// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Outbox.Tests.Outbox;

/// <summary>
/// Locks the one classifier every relational outbox store uses to report a fenced completion.
/// </summary>
/// <remarks>
/// The provider arms that exercise this need a real database, so they do not run everywhere. These run on
/// every build, and cover every combination of the four facts a statement reports. The expected outcome for
/// each row is written out by hand rather than derived, so the table cannot agree with the classifier merely
/// by restating it.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class FencedCompletionClassifierShould
{
	private const long Presented = 7;
	private const long Newer = 8;

	/// <summary>
	/// Every combination of fence, update, presence and terminal status maps to the outcome the contract names.
	/// </summary>
	[Theory]
	// A newer tenure outranks everything else the statement saw.
	[InlineData(Newer, 0, false, false, OutboxCompletionOutcome.FenceRefused)]
	[InlineData(Newer, 0, false, true, OutboxCompletionOutcome.FenceRefused)]
	[InlineData(Newer, 0, true, false, OutboxCompletionOutcome.FenceRefused)]
	[InlineData(Newer, 0, true, true, OutboxCompletionOutcome.FenceRefused)]
	[InlineData(Newer, 1, false, false, OutboxCompletionOutcome.FenceRefused)]
	[InlineData(Newer, 1, false, true, OutboxCompletionOutcome.FenceRefused)]
	[InlineData(Newer, 1, true, false, OutboxCompletionOutcome.FenceRefused)]
	[InlineData(Newer, 1, true, true, OutboxCompletionOutcome.FenceRefused)]
	// The current tenure, and the write took effect.
	[InlineData(Presented, 1, false, false, OutboxCompletionOutcome.Applied)]
	[InlineData(Presented, 1, false, true, OutboxCompletionOutcome.Applied)]
	[InlineData(Presented, 1, true, false, OutboxCompletionOutcome.Applied)]
	[InlineData(Presented, 1, true, true, OutboxCompletionOutcome.Applied)]
	// The current tenure, nothing written.
	[InlineData(Presented, 0, false, false, OutboxCompletionOutcome.MessageNotFound)]
	[InlineData(Presented, 0, false, true, OutboxCompletionOutcome.MessageNotFound)]
	[InlineData(Presented, 0, true, true, OutboxCompletionOutcome.AlreadyTerminal)]
	[InlineData(Presented, 0, true, false, OutboxCompletionOutcome.ClaimLost)]
	public void MapEveryObservationToTheOutcomeTheContractNames(
		long highWaterToken,
		int updatedCount,
		bool rowExists,
		bool isTerminal,
		OutboxCompletionOutcome expected) =>
		FencedCompletionClassifier.Classify(highWaterToken, Presented, updatedCount, rowExists, isTerminal)
			.ShouldBe(expected);

	/// <summary>
	/// A message that is present and terminal is reported as terminal, never as a claim someone else holds.
	/// </summary>
	/// <remarks>
	/// Nobody owns a terminal message, so reporting it as lost tells the caller to back off from a conflict
	/// that does not exist, when the work is in fact done.
	/// </remarks>
	[Fact]
	public void ReportATerminalMessageAsAlreadyTerminal_NotAsALostClaim() =>
		FencedCompletionClassifier.Classify(Presented, Presented, updatedCount: 0, rowExists: true, isTerminal: true)
			.ShouldBe(OutboxCompletionOutcome.AlreadyTerminal);

	/// <summary>
	/// LIVENESS: a present, non-terminal row the caller could not write is still a lost claim.
	/// </summary>
	/// <remarks>
	/// Without this, a classifier that reported every refusal as terminal would satisfy the arm above.
	/// </remarks>
	[Fact]
	public void StillReportALostClaimWhenTheRowIsNotTerminal() =>
		FencedCompletionClassifier.Classify(Presented, Presented, updatedCount: 0, rowExists: true, isTerminal: false)
			.ShouldBe(OutboxCompletionOutcome.ClaimLost);
}

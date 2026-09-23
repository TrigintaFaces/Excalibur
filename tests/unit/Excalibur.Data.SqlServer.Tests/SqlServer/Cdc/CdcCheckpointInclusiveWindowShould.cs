// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Binds the checkpoint advancement to the SAME inclusive window the producer loop scans.
/// </summary>
/// <remarks>
/// The producer admits every position satisfying <c>current &lt;= max</c>. Checkpoint advancement
/// retained a table only while its next transaction was strictly BELOW the captured maximum, so a
/// table whose final transaction sat exactly AT that maximum was dropped with work outstanding —
/// and, because no checkpoint is written for a position never processed, every later run resumed
/// from the same durable position, re-derived the same next LSN, and dropped it again. Each arm
/// below states which half it holds: the liveness arms fail if advancement is narrower than the
/// scan, the safety arms fail if it is wider.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Data.SqlServer")]
[Trait(TraitNames.Feature, TestFeatures.CDC)]
public sealed class CdcCheckpointInclusiveWindowShould : UnitTestBase
{
	private const string Table = "dbo_orders";

	private static readonly byte[] Lsn1 = [0x01];
	private static readonly byte[] Lsn2 = [0x02];
	private static readonly byte[] Lsn3 = [0x03];
	private static readonly byte[] Lsn9 = [0x09];

	private static CdcCheckpointManager CreateSutTracking(byte[] startingAt)
	{
		var sut = new CdcCheckpointManager(
			A.Fake<IDatabaseOptions>(),
			A.Fake<ICdcRepository>(),
			A.Fake<ISqlServerCdcStateStore>(),
			NullLogger.Instance);

		sut.UpdateLsnTracking(Table, startingAt, seqVal: null);
		return sut;
	}

	/// <summary>
	/// LIVENESS. The defect's direct expression: the last transaction on a quiet table is exactly at
	/// the captured maximum, and dropping it there is what starved it indefinitely.
	/// </summary>
	[Fact]
	public void RetainATableWhoseNextTransactionSitsExactlyAtTheCapturedMaximum()
	{
		var sut = CreateSutTracking(Lsn1);

		sut.UpdateLsnAfterProcessing(Table, nextLsn: Lsn2, maxLsn: Lsn2);

		var tracking = sut.GetTracking(Table);
		tracking.ShouldNotBeNull(
			"a transaction AT the captured maximum is inside the window the producer scans, so the table "
			+ "still has work and must stay tracked; dropping it here strands that transaction until "
			+ "unrelated activity raises the maximum above it");
		tracking.Lsn.ShouldBe(Lsn2);
	}

	/// <summary>
	/// LIVENESS control. Below the maximum was always retained — this arm passes before and after the
	/// fix, so it is the positive control proving the other arms are reading a live instrument.
	/// </summary>
	[Fact]
	public void RetainATableWhoseNextTransactionIsBelowTheCapturedMaximum()
	{
		var sut = CreateSutTracking(Lsn1);

		sut.UpdateLsnAfterProcessing(Table, nextLsn: Lsn2, maxLsn: Lsn9);

		sut.GetTracking(Table).ShouldNotBeNull();
	}

	/// <summary>
	/// SAFETY. The window must not be WIDENED past the captured maximum: work above it belongs to the
	/// next poll, which captures a fresh maximum.
	/// </summary>
	[Fact]
	public void DropATableWhoseNextTransactionIsAboveTheCapturedMaximum()
	{
		var sut = CreateSutTracking(Lsn1);

		sut.UpdateLsnAfterProcessing(Table, nextLsn: Lsn3, maxLsn: Lsn2);

		sut.GetTracking(Table).ShouldBeNull(
			"a transaction above the captured maximum was not part of this run's scan and must wait for "
			+ "the next poll rather than extending the current one");
	}

	/// <summary>
	/// SAFETY. No next transaction means the table is finished for this run.
	/// </summary>
	[Fact]
	public void DropATableThatHasNoNextTransaction()
	{
		var sut = CreateSutTracking(Lsn1);

		sut.UpdateLsnAfterProcessing(Table, nextLsn: null, maxLsn: Lsn9);

		sut.GetTracking(Table).ShouldBeNull();
	}

	/// <summary>
	/// TERMINATION. Retaining on equality must not make the run unable to finish. The next LSN is the
	/// next ACTUAL transaction and is strictly increasing, so the iteration that follows the retained
	/// one yields a position above the captured maximum and drops the table then.
	/// </summary>
	[Fact]
	public void FinishTheRunAfterRetainingOnEquality()
	{
		var sut = CreateSutTracking(Lsn1);

		sut.UpdateLsnAfterProcessing(Table, nextLsn: Lsn2, maxLsn: Lsn2);
		sut.GetTracking(Table).ShouldNotBeNull();

		sut.UpdateLsnAfterProcessing(Table, nextLsn: Lsn3, maxLsn: Lsn2);

		sut.GetTracking(Table).ShouldBeNull(
			"retaining at the maximum buys exactly one more iteration, not an unbounded one");
		sut.GetNextLsn().ShouldBeNull("the run is finished once no table is tracked");
	}
}

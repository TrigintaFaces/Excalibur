// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Saga.SqlServer.Requests;

namespace Excalibur.Data.Tests.Saga;

// Regression lock for the lock hints on the saga upsert.
//
// WHY THIS IS A STRUCTURAL LOCK AND NOT A BEHAVIOURAL ONE, stated so nobody mistakes it for more than it is.
// The property at stake is that two concurrent upserts of one saga key do not deadlock. That is the SQL
// Server engine's behaviour, so the honest lock would be a concurrent one against a real server. It was
// attempted and it does NOT discriminate. Measured against real SQL Server with the SHARED-lock-only form
// in place: zero deadlocks over 200 real races (8 writers x 25 fresh keys), and zero again over 7,200
// (48 x 150). A concurrent arm passes on the broken code and proves nothing about the hints. The conversion
// window inside a single autocommit MERGE is too narrow to hit
// reliably from one client process; it is production load, not a test loop, that finds it. Rather than ship
// a green arm over a race that never raced, the hint pair is locked structurally here and the concurrent
// arm in the integration tier is scoped to what it actually proves.
//
// So: this asserts the SQL we generate, and the reasoning for why that SQL is correct lives with it.
//
//   * HOLDLOCK alone  -> SHARED range lock on the match, converted to exclusive for the write. Two sessions
//                        each hold S and each wait for the other to drop it. The engine breaks the cycle by
//                        killing one as a deadlock victim (1205) -- on a saga, a lost or retried state
//                        transition under exactly the concurrent load a process manager exists to handle.
//   * UPDLOCK alone   -> no range held, so the phantom-insert race is back: two sessions both evaluate
//                        WHEN NOT MATCHED and both INSERT -> key violation.
//   * Both            -> UPDATE lock on the read (not mutually compatible, so the second session blocks
//                        briefly instead of deadlocking) AND the range held (so the phantom stays closed).
//
// Either hint alone is a defect in one direction or the other, which is why both are asserted separately
// rather than as one string match -- a mutation that drops either one is named for what it broke.
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class SagaUpsertLockHintShould
{
	private const string QualifiedTableName = "[dispatch].[sagas]";

	private static string SaveSagaCommandText()
		=> new SaveSagaRequest<TestSagaState>(
			new TestSagaState { SagaId = Guid.NewGuid() },
			new DispatchJsonSerializer(),
			QualifiedTableName,
			TenantScope.Untenanted,
			CancellationToken.None).Command.CommandText;

	private sealed class TestSagaState : SagaState
	{
	}

	[Fact]
	public void TakeAnUpdateLockOnTheMatch_SoConcurrentUpsertsBlockRatherThanDeadlock()
	{
		SaveSagaCommandText().ShouldContain(
			"UPDLOCK",
			Case.Sensitive,
			customMessage: "without UPDLOCK the MERGE reads under a SHARED lock and converts to exclusive for "
				+ "the write, which is the conversion-deadlock (1205) shape under concurrent upsert of one key");
	}

	[Fact]
	public void HoldTheKeyRange_SoConcurrentUpsertsCannotBothInsert()
	{
		SaveSagaCommandText().ShouldContain(
			"HOLDLOCK",
			Case.Sensitive,
			customMessage: "without HOLDLOCK the key range is not held, so two sessions can both evaluate "
				+ "WHEN NOT MATCHED and both INSERT -> primary-key violation");
	}
}

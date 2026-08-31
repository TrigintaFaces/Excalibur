// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.SqlServer.Requests;

namespace Excalibur.Outbox.Tests.SqlServer.Requests;

/// <summary>
/// Locks the retry-visibility gate to a SINGLE clock: the one the claim predicate reads.
/// </summary>
/// <remarks>
/// <para>
/// The claim decides whether a message is due with <c>NextAttemptAt &lt;= SYSUTCDATETIME()</c> — the server's
/// clock. The failure mark used to write that same column from an instant the DISPATCHER computed against its
/// own clock, so one comparison straddled two machines that have no reason to agree.
/// </para>
/// <para>
/// The direction that hurts is the one nobody asks about. Skew was reasoned about as safe because a dispatcher
/// running ahead "can only defer the message further" — but deferring a message whose backoff has genuinely
/// elapsed is not the safe direction. It is a stall bounded by nothing except the size of the skew, and a store
/// that never hands a due message back is perfectly safe and completely useless. These arms are therefore
/// LIVENESS arms; the floor arms below are their safety pair, and neither is sufficient alone.
/// </para>
/// <para>
/// Asserted on the emitted statement rather than against a container because the property is about WHICH CLOCK
/// the value is measured from, and both clocks read the same on a single test machine — a behavioural arm here
/// would pass just as readily on the defect. The end-to-end behaviour is covered by the store's
/// real-infrastructure reclaim and floor-clamp suites.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxBackoffIsDecidedOnTheServerClockShould : UnitTestBase
{
	private const string TableName = "[dbo].[OutboxMessages]";
	private const string MessageId = "msg-12345";
	private const string ErrorMessage = "Connection timeout";
	private const string LeasedBy = "processor-1";

	private static MarkMessageFailedRequest Build(DateTimeOffset? nextAttemptAt, int? floorSeconds) =>
		new(
			TableName,
			MessageId,
			ErrorMessage,
			1,
			LeasedBy,
			30,
			CancellationToken.None,
			nextAttemptAt,
			floorSeconds);

	/// <summary>
	/// LIVENESS. The caller's schedule must never be bound as an absolute instant, on any branch that writes
	/// the column — that is the two-clock comparison itself.
	/// </summary>
	/// <param name="floorSeconds">The configured floor, or <see langword="null"/> for the unfloored branch.</param>
	[Theory]
	[InlineData(null)]
	[InlineData(0)]
	[InlineData(30)]
	public void NeverBindTheDispatchersInstantIntoTheGate(int? floorSeconds)
	{
		var sql = Build(DateTimeOffset.UtcNow.AddSeconds(5), floorSeconds).Command.CommandText;

		sql.ShouldNotContain(
			"@NextAttemptAt",
			Case.Sensitive,
			"binding the dispatcher's absolute instant is the defect: the claim reads this column back on the "
			+ "server clock, so a dispatcher running ahead keeps a message whose backoff has elapsed invisible "
			+ "for the whole skew");

		sql.ShouldContain(
			"NextAttemptAt = TODATETIMEOFFSET(DATEADD(MILLISECOND",
			Case.Sensitive,
			"the gate must be re-anchored to the server clock as a delay added to SYSUTCDATETIME()");
	}

	/// <summary>
	/// LIVENESS. A schedule that has ALREADY elapsed must travel as a negative delay, so re-anchoring cannot
	/// push a due message into the future.
	/// </summary>
	[Fact]
	public void CarryAnElapsedScheduleAsANegativeDelay()
	{
		var request = Build(DateTimeOffset.UtcNow.AddSeconds(-5), floorSeconds: 0);

		var delayMs = (int)request.Parameters.Get<object>("@NextAttemptDelayMs");

		delayMs.ShouldBeLessThan(
			0,
			"an elapsed schedule means the message is due. Carrying it as a non-negative delay would defer a "
			+ "message that should come back now, which is the stall this conversion exists to prevent");
	}

	/// <summary>
	/// SAFETY. The floor still composes as a MAXIMUM, so a caller delay shorter than F cannot pull the next
	/// attempt inside F.
	/// </summary>
	[Fact]
	public void StillTakeTheLaterOfTheCallersDelayAndTheFloor()
	{
		var sql = Build(DateTimeOffset.UtcNow.AddSeconds(1), floorSeconds: 30).Command.CommandText;

		sql.ShouldContain(
			"CASE WHEN @NextAttemptDelayMs > @FloorSeconds * 1000 "
			+ "THEN @NextAttemptDelayMs ELSE @FloorSeconds * 1000 END",
			Case.Sensitive,
			"the floor is a lower bound on the gate. Relaxing it below F must stay a single-token inversion of "
			+ "this comparison rather than something ordinary use can express");
	}

	/// <summary>
	/// SAFETY. The plain failure path — no caller schedule at all — still anchors the floor on the server clock.
	/// </summary>
	[Fact]
	public void StillAnchorThePlainFailureFloorOnTheServerClock()
	{
		var sql = Build(nextAttemptAt: null, floorSeconds: 30).Command.CommandText;

		sql.ShouldContain(
			"NextAttemptAt = TODATETIMEOFFSET(DATEADD(SECOND, @FloorSeconds, SYSUTCDATETIME()), 0)",
			Case.Sensitive,
			"the plain path was already single-clock and must stay that way");
	}
}

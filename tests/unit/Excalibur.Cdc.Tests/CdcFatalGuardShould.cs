// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Dispatch;

namespace Excalibur.Cdc.Tests;

/// <summary>
/// Author≠impl regression lock for bead <c>pxhqri</c> (sprint 855, FR-B2 / ADR-338 / SA ruling 17124):
/// the shared, pure <see cref="CdcFatalGuard.Decide"/> — the single decision point every provider's CDC
/// consume loop gates its durable checkpoint-advance on — MUST return <b>advance only on clean success</b>,
/// <b>never advance on a fault</b>, <b>stop on fatal</b>, and <b>reconnect on transient</b>. Gating every
/// advance on this decision makes "advance the checkpoint past a fatal/transient fault" structurally
/// inexpressible (the safety invariant).
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the impl (<c>issue-remediation-protocol</c>) after reading the seam
/// (<c>pin-the-interface-seam-before-tests</c>) — the prior <c>ProcessBatchAsync</c> drive point had no
/// guard, so a lock there would have been vacuous (Tests 17119). This binds the extracted pure unit
/// directly: deterministic, zero real-infra.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> each arm asserts the exact <see cref="CdcFatalDecision"/> tuple, so mutating any
/// arm of <c>Decide</c> (e.g. advance-on-fatal, or stop-on-transient) flips a bound assertion to RED.
/// Pairs with the providers' structural gating (SA REVIEW_ARCH grep) — the decision is correct here, and
/// the loop physically cannot advance except when <c>AdvanceCheckpoint</c> is true.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class CdcFatalGuardShould
{
	[Fact]
	public void AdvanceCheckpointOnlyOnCleanSuccess()
	{
		var decision = CdcFatalGuard.Decide(exception: null, classifier: null);

		decision.ShouldBe(new CdcFatalDecision(AdvanceCheckpoint: true, Stop: false, Reconnect: false));
	}

	[Theory]
	[InlineData(typeof(UnauthorizedAccessException))]
	[InlineData(typeof(System.Security.Authentication.AuthenticationException))]
	[InlineData(typeof(NotSupportedException))]
	[InlineData(typeof(NotImplementedException))]
	[InlineData(typeof(ArgumentException))]
	[InlineData(typeof(ArgumentNullException))]
	public void StopWithoutAdvancingOnADefinitivelyFatalFault(Type exceptionType)
	{
		var exception = (Exception)Activator.CreateInstance(exceptionType)!;

		var decision = CdcFatalGuard.Decide(exception, classifier: null);

		// Fatal → never advance, stop loudly (no silent reconnect), don't reconnect.
		decision.ShouldBe(new CdcFatalDecision(AdvanceCheckpoint: false, Stop: true, Reconnect: false));
	}

	[Theory]
	[InlineData(typeof(InvalidOperationException))]
	[InlineData(typeof(TimeoutException))]
	[InlineData(typeof(OperationCanceledException))] // cooperative cancellation is NOT fatal (transient)
	public void ReconnectWithoutAdvancingOnATransientFault(Type exceptionType)
	{
		var exception = (Exception)Activator.CreateInstance(exceptionType)!;

		var decision = CdcFatalGuard.Decide(exception, classifier: null);

		// Transient → never advance, don't stop, reconnect and retry from the un-advanced checkpoint.
		decision.ShouldBe(new CdcFatalDecision(AdvanceCheckpoint: false, Stop: false, Reconnect: true));
	}

	[Fact]
	public void StopWithoutAdvancingWhenTheClassifierRulesAFaultPermanent()
	{
		// An exception that the built-in fallback would treat as TRANSIENT becomes FATAL when the shared
		// classifier rules it Permanent — proving Decide honors the classifier (ADR-338 single source of truth).
		var classifier = A.Fake<IMessageFailureClassifier>();
		_ = A.CallTo(() => classifier.Classify(A<Exception>._)).Returns(MessageFailureKind.Permanent);

		var decision = CdcFatalGuard.Decide(new InvalidOperationException("transient without a classifier"), classifier);

		decision.ShouldBe(new CdcFatalDecision(AdvanceCheckpoint: false, Stop: true, Reconnect: false));
	}
}

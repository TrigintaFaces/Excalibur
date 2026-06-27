// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Dispatch;

namespace Excalibur.Cdc.Tests.Cdc;

/// <summary>
/// Author≠impl regression lock for S852 · <c>14z4ao</c> Stage-2 — <see cref="CdcFatalClassifier.IsFatal"/>,
/// the provider-agnostic fatal-vs-transient seam all 5 CDC providers compose to decide
/// terminate-loud (fatal) vs keep-reconnecting (transient). ADR-338: a fatal error is terminal + loud,
/// never an infinite silent reconnect.
/// </summary>
/// <remarks>
/// <para>
/// Authored independently of the implementer (BackendDeveloper). Two regimes:
/// <b>with a classifier</b> → it is the single source of truth (Permanent/Poison ⇒ fatal; Transient ⇒
/// keep reconnecting); <b>without a classifier (null)</b> → a conservative BCL-only fallback where only
/// <em>definitively</em> non-retryable faults are fatal and everything unrecognised stays transient (a
/// genuine transient must never be mistaken for fatal).
/// </para>
/// <para>
/// <b>RED mutants:</b> drop <c>or Poison</c> ⇒ the Poison-fatal fact RED; flip the unrecognised default
/// <c>_ =&gt; false</c> to <c>true</c> ⇒ the transient-stays-transient facts RED (a transient fault would
/// be terminated — the exact ADR-338 violation); drop a definitively-fatal arm ⇒ its fact RED.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcFatalClassifierShould
{
	// ── With a classifier: it is the single source of truth ──

	[Theory]
	[InlineData(MessageFailureKind.Permanent)]
	[InlineData(MessageFailureKind.Poison)]
	public void TreatPermanentAndPoison_AsFatal_WhenClassifierPresent(MessageFailureKind kind)
	{
		var classifier = ClassifierReturning(kind);

		CdcFatalClassifier.IsFatal(new InvalidOperationException("x"), classifier).ShouldBeTrue();
	}

	[Fact]
	public void TreatTransient_AsNotFatal_WhenClassifierPresent()
	{
		// The load-bearing fail-safe: a transient fault keeps reconnecting, never terminated.
		var classifier = ClassifierReturning(MessageFailureKind.Transient);

		CdcFatalClassifier.IsFatal(new TimeoutException("blip"), classifier).ShouldBeFalse();
	}

	// ── Without a classifier: conservative BCL-only fallback ──

	[Theory]
	[MemberData(nameof(DefinitivelyFatalExceptions))]
	public void TreatDefinitivelyNonRetryable_AsFatal_WhenNoClassifier(Exception exception)
	{
		CdcFatalClassifier.IsFatal(exception, classifier: null).ShouldBeTrue();
	}

	[Theory]
	[MemberData(nameof(TransientOrUnrecognisedExceptions))]
	public void TreatTransientOrUnrecognised_AsNotFatal_WhenNoClassifier(Exception exception)
	{
		// Anything not definitively-fatal stays transient → bounded backoff-reconnect, not terminate.
		CdcFatalClassifier.IsFatal(exception, classifier: null).ShouldBeFalse();
	}

	[Fact]
	public void UnwrapSingleInnerAggregate_ToClassifyRealCause_WhenNoClassifier()
	{
		// A single-cause AggregateException is unwrapped to classify the real fault (fatal here)…
		var single = new AggregateException(new UnauthorizedAccessException("denied"));
		CdcFatalClassifier.IsFatal(single, classifier: null).ShouldBeTrue();

		// …but a genuine multi-fault aggregate is NOT unwrapped → stays transient (conservative).
		var multi = new AggregateException(new UnauthorizedAccessException("a"), new UnauthorizedAccessException("b"));
		CdcFatalClassifier.IsFatal(multi, classifier: null).ShouldBeFalse();
	}

	[Fact]
	public void ThrowOnNullException()
	{
		_ = Should.Throw<ArgumentNullException>(() => CdcFatalClassifier.IsFatal(null!, classifier: null));
	}

	public static IEnumerable<object[]> DefinitivelyFatalExceptions =>
	[
		[new System.Security.Authentication.AuthenticationException("auth")],
		[new UnauthorizedAccessException("denied")],
		[new NotSupportedException("nope")],
		[new NotImplementedException("todo")],
		[new ArgumentException("bad arg")],
		[new ArgumentNullException("p")],
		[new ArgumentOutOfRangeException("p")],
	];

	public static IEnumerable<object[]> TransientOrUnrecognisedExceptions =>
	[
		[new TimeoutException("timeout")],
		[new IOException("io blip")],
		[new InvalidOperationException("unrecognised")],
		[new OperationCanceledException("cooperative cancel is not a fault")],
	];

	private static IMessageFailureClassifier ClassifierReturning(MessageFailureKind kind)
	{
		var classifier = A.Fake<IMessageFailureClassifier>();
		_ = A.CallTo(() => classifier.Classify(A<Exception>._)).Returns(kind);
		return classifier;
	}
}

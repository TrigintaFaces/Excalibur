// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json;

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Author≠impl regression lock for S851 Lane 2 · <c>shu41d</c> — the shared failure-classification
/// contract (<see cref="IMessageFailureClassifier"/> / <see cref="DefaultMessageFailureClassifier"/>).
/// </summary>
/// <remarks>
/// <para>
/// Authored by TestsDeveloper independently of the implementer (BackendDeveloper), against the committed
/// seam (<c>IMessageFailureClassifier.Classify(Exception) → MessageFailureKind</c>, GUIDE/SA 15690). Pins
/// MS-2 AC-1 (poison ⇒ <see cref="MessageFailureKind.Poison"/>), AC-2 (transient ⇒
/// <see cref="MessageFailureKind.Transient"/>), the permanent arm, and the edge cases EC-1 (cancellation is
/// never poison), EC-2 (single-inner <see cref="AggregateException"/> unwraps to root), EC-3 (unknown ⇒
/// transient-with-cap default), plus the null-guard.
/// </para>
/// <para>
/// <b>RED on the pre-fix surface:</b> mutate <c>DefaultMessageFailureClassifier.Classify</c> to always
/// <c>return MessageFailureKind.Transient;</c> — the poison and permanent facts go RED (a deserialization
/// failure / validation error would then be retried to the cap instead of dead-lettered). Representative
/// <em>public</em> exceptions are used (not every internal arm) so the lock binds the contract behaviour
/// without re-testing the impl's exhaustive type list.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageFailureClassifierShould
{
	private static readonly IMessageFailureClassifier Classifier = new DefaultMessageFailureClassifier();

	[Fact]
	public void ClassifyDeserializationFailureAsPoison() =>
		// AC-1: a defective message (cannot be deserialized) is dead-lettered immediately, not retried.
		Classifier.Classify(new JsonException("bad payload")).ShouldBe(MessageFailureKind.Poison);

	[Fact]
	public void ClassifyUnauthorizedAccessAsPermanent() =>
		// Permanent: a non-retryable operation failure (authorization), not a defective message.
		Classifier.Classify(new UnauthorizedAccessException("denied")).ShouldBe(MessageFailureKind.Permanent);

	[Fact]
	public void ClassifyDataAnnotationsValidationAsPermanent() =>
		// Permanent: the BCL validation type a consumer throws — must map to Permanent alongside the
		// framework ValidationException (gap this lock surfaced; fixed by Backend in shu41d). Fully
		// qualified to pin the System.ComponentModel.DataAnnotations type, not Dispatch's own.
		Classifier.Classify(new System.ComponentModel.DataAnnotations.ValidationException("invalid"))
			.ShouldBe(MessageFailureKind.Permanent);

	[Fact]
	public void ClassifyArgumentExceptionAsPermanent() =>
		Classifier.Classify(new ArgumentException("bad arg")).ShouldBe(MessageFailureKind.Permanent);

	[Fact]
	public void ClassifyTransientFailureAsTransient() =>
		// AC-2: a transient blip gets the normal backoff retry.
		Classifier.Classify(new TimeoutException()).ShouldBe(MessageFailureKind.Transient);

	[Fact]
	public void ClassifyOperationCanceledAsTransient_NeverPoison() =>
		// EC-1: cancellation is never poison / never dead-lettered.
		Classifier.Classify(new OperationCanceledException()).ShouldBe(MessageFailureKind.Transient);

	[Fact]
	public void ClassifyTaskCanceledAsTransient() =>
		Classifier.Classify(new TaskCanceledException()).ShouldBe(MessageFailureKind.Transient);

	[Fact]
	public void UnwrapSingleInnerAggregate_ToRootClassification() =>
		// EC-2: classify the root cause, not the AggregateException wrapper.
		Classifier.Classify(new AggregateException(new JsonException("bad")))
			.ShouldBe(MessageFailureKind.Poison);

	[Fact]
	public void ClassifyUnknownExceptionAsTransient_BoundedRetryDefault() =>
		// EC-3: anything unrecognised ⇒ transient-with-cap (never infinite loop, never silent drop).
		Classifier.Classify(new InvalidOperationException("unrecognised"))
			.ShouldBe(MessageFailureKind.Transient);

	[Fact]
	public void ThrowOnNullException() =>
		_ = Should.Throw<ArgumentNullException>(() => Classifier.Classify(null!));
}

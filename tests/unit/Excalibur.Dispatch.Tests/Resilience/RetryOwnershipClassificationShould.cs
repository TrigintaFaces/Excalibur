// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Resilience;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class RetryOwnershipClassificationShould
{
	[Fact]
	public void BindTypedEvidenceToTheOriginalResultRatherThanAnEqualCopy()
	{
		using var owner = RetryOwnershipScope.Enter(A.Fake<IDispatchEvent>(), new MessageContext());
		var attempt = owner.BeginAttempt();
		var original = new EqualFailureResult();
		var copy = new EqualFailureResult();
		original.Equals(copy).ShouldBeTrue("this control must exercise value equality rather than only distinct identities");
		var fault = new IOException("typed evidence");
		RetryOwnershipScope.ReportFanout(attempt, original, false, MessageFailureKind.Permanent, fault);
		RetryOwnershipScope.TryGetKnownFailureKind(attempt, original, out var kind, out var savedFault).ShouldBeTrue();
		kind.ShouldBe(MessageFailureKind.Permanent);
		savedFault.ShouldBeSameAs(fault);
		RetryOwnershipScope.TryGetKnownFailureKind(attempt, copy, out _).ShouldBeFalse();
	}

	[Fact]
	public async Task KeepALatePriorAttemptReportOutOfTheCurrentAttempt()
	{
		using var owner = RetryOwnershipScope.Enter(A.Fake<IDispatchEvent>(), new MessageContext());
		var firstAttempt = owner.BeginAttempt();
		var result = new EqualFailureResult();
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var late = Task.Run(async () =>
		{
			await release.Task.ConfigureAwait(false);
			RetryOwnershipScope.ReportFanout(firstAttempt, result, false, MessageFailureKind.Poison);
		});
		var currentAttempt = owner.BeginAttempt();
		release.SetResult();
		await late.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
		RetryOwnershipScope.TryGetKnownFailureKind(firstAttempt, result, out _).ShouldBeTrue();
		RetryOwnershipScope.TryGetKnownFailureKind(currentAttempt, result, out _).ShouldBeFalse();
	}

	[Fact]
	public void KeepAnUnclassifiedFalseRetryDecisionUnknown()
	{
		using var owner = RetryOwnershipScope.Enter(A.Fake<IDispatchEvent>(), new MessageContext());
		var attempt = owner.BeginAttempt();
		var result = new EqualFailureResult();
		RetryOwnershipScope.ReportFanout(attempt, result, false);
		RetryOwnershipScope.AllowsRetryAfterResult(attempt, result).ShouldBeFalse("the established middleware boolean contract remains intact");
		RetryOwnershipScope.TryGetKnownFailureKind(attempt, result, out _).ShouldBeFalse("false alone must not cause persisted retirement");
	}

	[Fact]
	public async Task ClassifyTheActualFaultOnceAndRetainItsIdentity()
	{
		var classifier = A.Fake<IMessageFailureClassifier>();
		var fault = new IOException("original fault");
		A.CallTo(() => classifier.Classify(fault)).Returns(MessageFailureKind.Permanent);
		using var owner = RetryOwnershipScope.EnterClassified(A.Fake<IDispatchEvent>(), new MessageContext(), classifier);
		var outcome = await owner.PublishDestinationAsync(owner.BeginAttempt(), "A", () => Task.FromException(fault), CancellationToken.None)
			.ConfigureAwait(false);
		outcome.FailureKind.ShouldBe(MessageFailureKind.Permanent);
		outcome.Fault.ShouldNotBeNull().SourceException.ShouldBeSameAs(fault);
		outcome.Retryable.ShouldBeFalse();
		A.CallTo(() => classifier.Classify(fault)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RefuseToTreatAnUnknownClassifierEnumAsPermanent()
	{
		var classifier = A.Fake<IMessageFailureClassifier>();
		A.CallTo(() => classifier.Classify(A<Exception>._)).Returns((MessageFailureKind)999);
		using var owner = RetryOwnershipScope.EnterClassified(A.Fake<IDispatchEvent>(), new MessageContext(), classifier);
		var outcome = await owner.PublishDestinationAsync(owner.BeginAttempt(), "A", () => Task.FromException(new IOException()), CancellationToken.None)
			.ConfigureAwait(false);
		outcome.FailureKind.ShouldBeNull();
		var result = new EqualFailureResult();
		RetryOwnershipScope.ReportFanout(owner.CaptureAttempt(), result, false, outcome.FailureKind);
		RetryOwnershipScope.TryGetKnownFailureKind(owner.CaptureAttempt(), result, out _).ShouldBeFalse();
	}

	[Fact]
	public void RefuseContradictoryRetryableOrSuccessfulTerminalEvidence()
	{
		using var owner = RetryOwnershipScope.Enter(A.Fake<IDispatchEvent>(), new MessageContext());
		var attempt = owner.BeginAttempt();
		var mixed = new EqualFailureResult();
		var success = MessageResult.Success();
		RetryOwnershipScope.ReportFanout(attempt, mixed, true, MessageFailureKind.Permanent);
		RetryOwnershipScope.ReportFanout(attempt, success, false, MessageFailureKind.Poison);
		RetryOwnershipScope.TryGetKnownFailureKind(attempt, mixed, out _).ShouldBeFalse();
		RetryOwnershipScope.TryGetKnownFailureKind(attempt, success, out _).ShouldBeFalse();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void ReplaceEarlierTerminalEvidenceWhenTheExactResultIsReportedAgain(bool retryable)
	{
		using var owner = RetryOwnershipScope.Enter(A.Fake<IDispatchEvent>(), new MessageContext());
		var attempt = owner.BeginAttempt();
		var result = new EqualFailureResult();
		RetryOwnershipScope.ReportFanout(attempt, result, false, MessageFailureKind.Permanent, new IOException("first report"));
		RetryOwnershipScope.TryGetKnownFailureKind(attempt, result, out _).ShouldBeTrue();
		RetryOwnershipScope.ReportFanout(attempt, result, retryable);
		RetryOwnershipScope.AllowsRetryAfterResult(attempt, result).ShouldBe(retryable);
		RetryOwnershipScope.TryGetKnownFailureKind(attempt, result, out _, out var fault).ShouldBeFalse();
		fault.ShouldBeNull("an earlier terminal fault must not survive an unknown or retryable replacement report");
	}

	private sealed class EqualFailureResult : IMessageResult
	{
		public bool Succeeded => false;
		public string ErrorMessage => "equal failure";
		public object? ValidationResult => null;
		public object? AuthorizationResult => null;
		public IMessageProblemDetails? ProblemDetails => null;
		public override bool Equals(object? obj) => obj is EqualFailureResult;
		public override int GetHashCode() => 1;
	}
}

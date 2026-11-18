// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Cdc.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
[Trait("Pattern", "Regression")]
public sealed class CdcUnknownFailureShould
{
	private sealed class UnclassifiedFailureException(string message) : Exception(message);

	[Fact]
	public void StopUnknownWithoutAdvancingWithAndWithoutAClassifier()
	{
		var fault = new UnclassifiedFailureException("not known to recover");
		var classifier = A.Fake<IMessageFailureClassifier>();
		A.CallTo(() => classifier.Classify(fault)).Returns(MessageFailureKind.Unknown);
		CdcFatalGuard.Decide(fault, classifier).ShouldBe(new CdcFatalDecision(false, true));
		CdcFatalGuard.Decide(fault, null).ShouldBe(new CdcFatalDecision(false, true));
		CdcFatalClassifier.IsFatal(fault, classifier).ShouldBeTrue();
		CdcFatalClassifier.IsFatal(fault, null).ShouldBeTrue();
	}

	[Theory]
	[InlineData(MessageFailureKind.Transient)]
	[InlineData(MessageFailureKind.Uncertain)]
	public void PreserveRecognizedRecoveryWithoutCheckpointAdvance(MessageFailureKind kind)
	{
		var fault = new TimeoutException();
		var classifier = A.Fake<IMessageFailureClassifier>();
		A.CallTo(() => classifier.Classify(fault)).Returns(kind);
		CdcFatalGuard.Decide(fault, classifier).ShouldBe(new CdcFatalDecision(false, false));
	}

	[Fact]
	public void KeepFallbackTransientAndCancellationRecoverable()
	{
		CdcFatalGuard.Decide(new TimeoutException(), null).ShouldBe(new CdcFatalDecision(false, false));
		CdcFatalGuard.Decide(new IOException(), null).ShouldBe(new CdcFatalDecision(false, false));
		CdcFatalGuard.Decide(new OperationCanceledException(), null).ShouldBe(new CdcFatalDecision(false, false));
		CdcFatalGuard.Decide(null, null).ShouldBe(new CdcFatalDecision(true, false));
	}
}

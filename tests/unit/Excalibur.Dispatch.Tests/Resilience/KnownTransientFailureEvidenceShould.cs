// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
[Trait("Pattern", "Regression")]
public sealed class KnownTransientFailureEvidenceShould
{
	private sealed class UnclassifiedFailureException(string message) : Exception(message);

	[Fact]
	public void PreserveExactObservedFailureAfterItsFlowScopeEnds()
	{
		var classifier = new DefaultMessageFailureClassifier();
		var observed = new UnclassifiedFailureException("same message");
		classifier.Classify(observed).ShouldBe(MessageFailureKind.Unknown);
		using (RetryOwnershipScope.Enter(A.Fake<IDispatchEvent>(), new MessageContext()))
		{
			KnownTransientFailureEvidence.Record(observed);
		}
		classifier.Classify(observed).ShouldBe(MessageFailureKind.Transient);
		classifier.Classify(new AggregateException(observed)).ShouldBe(MessageFailureKind.Transient);
		classifier.Classify(new UnclassifiedFailureException("same message")).ShouldBe(MessageFailureKind.Unknown);
	}

	[Fact]
	public void RejectUntrustedExceptionDataAsClassificationEvidence()
	{
		var fault = new UnclassifiedFailureException("untrusted");
		fault.Data["KnownTransientFailureEvidence"] = MessageFailureKind.Transient;
		new DefaultMessageFailureClassifier().Classify(fault).ShouldBe(MessageFailureKind.Unknown);
	}

	[Fact]
	public void PreserveExplicitPermanentAndUncertainMappings()
	{
		var permanent = new ArgumentException("invalid operation");
		var uncertain = new DispatchDeliveryUncertainException("acceptance unknown");
		KnownTransientFailureEvidence.Record(permanent);
		KnownTransientFailureEvidence.Record(uncertain);
		var classifier = new DefaultMessageFailureClassifier();
		classifier.Classify(permanent).ShouldBe(MessageFailureKind.Permanent);
		classifier.Classify(uncertain).ShouldBe(MessageFailureKind.Uncertain);
	}

	[Fact]
	public async Task RecordDuplicateObservationsConcurrentlyWithoutChangingIdentity()
	{
		var fault = new UnclassifiedFailureException("observed");
		await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => KnownTransientFailureEvidence.Record(fault))));
		new DefaultMessageFailureClassifier().Classify(fault).ShouldBe(MessageFailureKind.Transient);
	}
}

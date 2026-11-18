// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Abstractions;

public sealed class SagaTimeoutHoldOutcomeContractsShould
{
    [Theory]
    [InlineData(SagaTimeoutHoldOutcome.Held)]
    [InlineData(SagaTimeoutHoldOutcome.RetryWithFreshClaim)]
    [InlineData(SagaTimeoutHoldOutcome.ObservationEnded)]
    [InlineData(SagaTimeoutHoldOutcome.Rejected)]
    public void PreserveDistinctOutcomesWithoutInventingReceipt(SagaTimeoutHoldOutcome outcome)
    {
        var receipt = outcome == SagaTimeoutHoldOutcome.Held ? Receipt(SagaTimeoutHoldState.Held) : null;
        var result = new SagaTimeoutHoldAttempt(outcome, receipt);
        result.Outcome.ShouldBe(outcome);
        result.Receipt.ShouldBeSameAs(receipt);
    }

    [Fact]
    public void RefuseHeldWithoutActualCurrentReceipt()
    {
        Should.Throw<ArgumentException>(() => new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.Held));
    }

    [Theory]
    [InlineData(SagaTimeoutHoldOutcome.RetryWithFreshClaim)]
    [InlineData(SagaTimeoutHoldOutcome.ObservationEnded)]
    [InlineData(SagaTimeoutHoldOutcome.Rejected)]
    public void RefuseCurrentHeldCertificateForUnrecordedOrEndedOutcome(SagaTimeoutHoldOutcome outcome)
    {
        Should.Throw<ArgumentException>(() => new SagaTimeoutHoldAttempt(outcome, Receipt(SagaTimeoutHoldState.Held)));
    }

    [Fact]
    public void AllowTruthfulEndedReceiptWithoutDeliveryAcknowledgment()
    {
        var receipt = Receipt(SagaTimeoutHoldState.Resumed);
        var result = new SagaTimeoutHoldAttempt(SagaTimeoutHoldOutcome.ObservationEnded, receipt);
        result.Receipt.ShouldBeSameAs(receipt);
        result.Outcome.ShouldBe(SagaTimeoutHoldOutcome.ObservationEnded);
    }

    [Fact]
    public void RefuseUndefinedOutcome()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SagaTimeoutHoldAttempt((SagaTimeoutHoldOutcome)99));
    }

    private static SagaTimeoutHoldReceipt Receipt(SagaTimeoutHoldState state) => new(new SagaTimeoutHoldToken("provider-binding"), state,
        new SagaTimeout("original", "saga", "Saga", "Timeout", [1, 2], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
        MessageFailureKind.Unknown, "diagnostic");
}

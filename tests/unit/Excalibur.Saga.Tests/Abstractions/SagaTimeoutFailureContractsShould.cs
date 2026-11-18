// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Tests.Abstractions;

public sealed class SagaTimeoutFailureContractsShould
{
    [Theory]
    [InlineData(null, "cycle")]
    [InlineData("", "cycle")]
    [InlineData("source", null)]
    [InlineData("source", "")]
    public void RefuseObservationWithoutBothClaimProjectedEpochs(string? source, string? cycle)
    {
        var claim = NewClaim() with { SourceEpoch = source, DeliveryEpoch = cycle };
        Should.Throw<ArgumentException>(() => new SagaTimeoutFailureObservation(claim, MessageFailureKind.Unknown, "failure"));
    }

    [Fact]
    public void RetainOriginalClaimEpochsAndOwnPayloadAcrossInputAndOutputMutation()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var claim = NewClaim() with { Timeout = NewTimeout(bytes) };
        var observation = new SagaTimeoutFailureObservation(claim, MessageFailureKind.Unknown, "original");
        bytes[0] = 99;
        claim = claim with { ClaimToken = "fresh-token", DeliveryEpoch = "resumed-cycle" };
        var first = observation.OriginalClaim;
        first.Timeout.TimeoutData![1] = 99;
        var second = observation.OriginalClaim;
        second.ClaimToken.ShouldBe("original-token");
        second.SourceEpoch.ShouldBe("source");
        second.DeliveryEpoch.ShouldBe("cycle");
        second.Timeout.TimeoutData.ShouldBe(new byte[] { 1, 2, 3 });
        first.Timeout.TimeoutData.ShouldNotBeSameAs(second.Timeout.TimeoutData);
        claim.DeliveryEpoch.ShouldBe("resumed-cycle");
    }

    [Theory]
    [InlineData(MessageFailureKind.Transient)]
    [InlineData(MessageFailureKind.Uncertain)]
    public void RefuseRetryableClassificationAsHoldObservation(MessageFailureKind kind)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SagaTimeoutFailureObservation(NewClaim(), kind, "failure"));
    }

    [Theory]
    [InlineData(MessageFailureKind.Permanent)]
    [InlineData(MessageFailureKind.Poison)]
    [InlineData(MessageFailureKind.Unknown)]
    public void KeepBoundedReasonSeparateFromOriginalPayload(MessageFailureKind kind)
    {
        var reason = new string('x', 1024);
        var observation = new SagaTimeoutFailureObservation(NewClaim(), kind, reason);
        observation.Reason.ShouldBe(reason);
        observation.Kind.ShouldBe(kind);
        observation.OriginalClaim.Timeout.TimeoutData.ShouldBe(new byte[] { 1, 2, 3 });
        Should.Throw<ArgumentOutOfRangeException>(() => new SagaTimeoutFailureObservation(NewClaim(), kind, reason + "x"));
    }

    [Fact]
    public void OwnReceiptPayloadOnConstructionAndEveryRead()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var timeout = NewTimeout(bytes);
        var receipt = NewReceipt(timeout);
        bytes[0] = 99;
        receipt.Timeout.TimeoutData![1] = 99;
        receipt.Timeout.TimeoutData.ShouldBe(new byte[] { 1, 2, 3 });
        receipt.Timeout.TimeoutId.ShouldBe(timeout.TimeoutId);
        receipt.Timeout.DueAt.ShouldBe(timeout.DueAt);
        receipt.Timeout.TenantId.ShouldBe(timeout.TenantId);
        receipt.State.ShouldBe(SagaTimeoutHoldState.Held);
    }

    [Fact]
    public void SnapshotPageMembershipAndRefuseNonHeldOrOversizedPages()
    {
        var receipt = NewReceipt(NewTimeout([1, 2, 3]));
        var input = new List<SagaTimeoutHoldReceipt> { receipt };
        var page = new SagaTimeoutHoldPage(input, "next");
        input.Clear();
        page.Items.Count.ShouldBe(1);
        page.NextCursor.ShouldBe("next");
        page.Items[0].Timeout.TimeoutData![0] = 99;
        page.Items[0].Timeout.TimeoutData.ShouldBe(new byte[] { 1, 2, 3 });
        var resumed = new SagaTimeoutHoldReceipt(receipt.Token, SagaTimeoutHoldState.Resumed,
            receipt.Timeout, MessageFailureKind.Unknown, "failure");
        Should.Throw<ArgumentException>(() => new SagaTimeoutHoldPage([resumed], null));
        Should.Throw<ArgumentException>(() => new SagaTimeoutHoldPage(Enumerable.Repeat(receipt, 257), null));
        new SagaTimeoutHoldPage(Enumerable.Repeat(receipt, 256), null).Items.Count.ShouldBe(256);
    }

    private static ClaimedSagaTimeout NewClaim() => new(NewTimeout([1, 2, 3]), "original-token")
    { SourceEpoch = "source", DeliveryEpoch = "cycle" };

    private static SagaTimeout NewTimeout(byte[] bytes) => new("opaque-id ", Guid.Empty.ToString(), "Saga", "Timeout",
        bytes, DateTimeOffset.UnixEpoch.AddMinutes(1), DateTimeOffset.UnixEpoch) { TenantId = "tenant " };

    private static SagaTimeoutHoldReceipt NewReceipt(SagaTimeout timeout) => new(new SagaTimeoutHoldToken("opaque-binding"),
        SagaTimeoutHoldState.Held, timeout, MessageFailureKind.Unknown, "failure");
}

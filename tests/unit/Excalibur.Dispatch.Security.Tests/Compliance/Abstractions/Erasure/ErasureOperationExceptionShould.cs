// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Erasure;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ErasureOperationExceptionShould
{
    [Fact]
    public void ConstructWithParameterlessConstructor()
    {
        var ex = new ErasureOperationException();
        ex.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void ConstructWithMessage()
    {
        var ex = new ErasureOperationException("erasure failed");
        ex.Message.ShouldBe("erasure failed");
    }

    [Fact]
    public void ConstructWithMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ErasureOperationException("outer", inner);

        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void CreateValidationFailedException()
    {
        var requestId = Guid.NewGuid();
        var ex = ErasureOperationException.ValidationFailed(requestId, "Invalid request");

        ex.RequestId.ShouldBe(requestId);
        ex.Reason.ShouldBe(ErasureFailureReason.ValidationFailed);
        ex.Message.ShouldBe("Invalid request");
    }

    [Fact]
    public void CreateKeyDeletionFailedException()
    {
        var requestId = Guid.NewGuid();
        var ex = ErasureOperationException.KeyDeletionFailed(requestId, "key-1");

        ex.RequestId.ShouldBe(requestId);
        ex.Reason.ShouldBe(ErasureFailureReason.KeyDeletionFailed);
        ex.Message.ShouldContain("key-1");
    }

    [Fact]
    public void CreateVerificationFailedException()
    {
        var requestId = Guid.NewGuid();
        var ex = ErasureOperationException.VerificationFailed(requestId, "Verification error");

        ex.RequestId.ShouldBe(requestId);
        ex.Reason.ShouldBe(ErasureFailureReason.VerificationFailed);
    }

    [Fact]
    public void CreateBlockedByLegalHoldException()
    {
        var requestId = Guid.NewGuid();
        var holdId = Guid.NewGuid();
        var ex = ErasureOperationException.BlockedByLegalHold(requestId, holdId, "CASE-123");

        ex.RequestId.ShouldBe(requestId);
        ex.Reason.ShouldBe(ErasureFailureReason.BlockedByLegalHold);
        ex.Message.ShouldContain("CASE-123");
    }
}

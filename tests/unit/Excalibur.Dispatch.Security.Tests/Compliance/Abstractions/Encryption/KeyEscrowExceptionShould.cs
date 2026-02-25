// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyEscrowExceptionShould
{
    [Fact]
    public void ConstructWithParameterlessConstructor()
    {
        var ex = new KeyEscrowException();
        ex.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void ConstructWithMessage()
    {
        var ex = new KeyEscrowException("escrow failed");
        ex.Message.ShouldBe("escrow failed");
    }

    [Fact]
    public void ConstructWithMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new KeyEscrowException("outer", inner);

        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void SupportInitProperties()
    {
        var ex = new KeyEscrowException("fail")
        {
            KeyId = "key-1",
            EscrowId = "escrow-1",
            ErrorCode = KeyEscrowErrorCode.EscrowExpired
        };

        ex.KeyId.ShouldBe("key-1");
        ex.EscrowId.ShouldBe("escrow-1");
        ex.ErrorCode.ShouldBe(KeyEscrowErrorCode.EscrowExpired);
    }

    [Theory]
    [InlineData(KeyEscrowErrorCode.Unknown, 0)]
    [InlineData(KeyEscrowErrorCode.KeyNotFound, 1)]
    [InlineData(KeyEscrowErrorCode.IntegrityCheckFailed, 10)]
    public void HaveExpectedEnumValues(KeyEscrowErrorCode code, int expectedValue)
    {
        ((int)code).ShouldBe(expectedValue);
    }
}

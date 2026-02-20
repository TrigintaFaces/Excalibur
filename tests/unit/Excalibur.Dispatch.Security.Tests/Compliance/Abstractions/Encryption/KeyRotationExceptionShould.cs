// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyRotationExceptionShould
{
    [Fact]
    public void ConstructWithParameterlessConstructorAndDefaultMessage()
    {
        var ex = new KeyRotationException();

        ex.ShouldBeAssignableTo<EncryptionException>();
        ex.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ConstructWithMessage()
    {
        var ex = new KeyRotationException("rotation failed");
        ex.Message.ShouldBe("rotation failed");
    }

    [Fact]
    public void ConstructWithMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new KeyRotationException("outer", inner);

        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void ConstructWithMessageAndErrorCode()
    {
        var ex = new KeyRotationException("fail", EncryptionErrorCode.ServiceUnavailable);

        ex.ErrorCode.ShouldBe(EncryptionErrorCode.ServiceUnavailable);
        ex.Message.ShouldBe("fail");
    }
}

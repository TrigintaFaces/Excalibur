// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Compliance;
using Shouldly;
using Xunit;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class DecryptionExceptionShould
{
    [Fact]
    public void SetDefaultErrorCodeWithParameterlessConstructor()
    {
        var ex = new DecryptionException();

        ex.ErrorCode.ShouldBe(EncryptionErrorCode.InvalidCiphertext);
        ex.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void SetDefaultErrorCodeWithMessageConstructor()
    {
        var ex = new DecryptionException("test message");

        ex.Message.ShouldBe("test message");
        ex.ErrorCode.ShouldBe(EncryptionErrorCode.InvalidCiphertext);
    }

    [Fact]
    public void SetDefaultErrorCodeWithMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new DecryptionException("outer", inner);

        ex.Message.ShouldBe("outer");
        ex.InnerException.ShouldBe(inner);
        ex.ErrorCode.ShouldBe(EncryptionErrorCode.InvalidCiphertext);
    }

    [Fact]
    public void AcceptCustomErrorCode()
    {
        var ex = new DecryptionException("msg", EncryptionErrorCode.AuthenticationFailed);

        ex.ErrorCode.ShouldBe(EncryptionErrorCode.AuthenticationFailed);
    }

    [Fact]
    public void AcceptCustomErrorCodeWithInnerException()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new DecryptionException("msg", inner, EncryptionErrorCode.KeyNotFound);

        ex.ErrorCode.ShouldBe(EncryptionErrorCode.KeyNotFound);
        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void DeriveFromEncryptionException()
    {
        var ex = new DecryptionException();
        ex.ShouldBeAssignableTo<EncryptionException>();
    }
}

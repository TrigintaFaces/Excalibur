// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SendErrorShould
{
    [Fact]
    public void Have_Default_Properties()
    {
        var error = new SendError();
        error.Code.ShouldBe(string.Empty);
        error.Message.ShouldBe(string.Empty);
        error.Exception.ShouldBeNull();
        error.IsRetryable.ShouldBeFalse();
    }

    [Fact]
    public void Set_And_Get_All_Properties()
    {
        var ex = new InvalidOperationException("test");
        var error = new SendError
        {
            Code = "InvalidOp",
            Message = "Something went wrong",
            Exception = ex,
            IsRetryable = true,
        };

        error.Code.ShouldBe("InvalidOp");
        error.Message.ShouldBe("Something went wrong");
        error.Exception.ShouldBeSameAs(ex);
        error.IsRetryable.ShouldBeTrue();
    }

    [Fact]
    public void Create_FromException_With_Defaults()
    {
        var ex = new TimeoutException("Connection timed out");
        var error = SendError.FromException(ex);

        error.Code.ShouldBe("TimeoutException");
        error.Message.ShouldBe("Connection timed out");
        error.Exception.ShouldBeSameAs(ex);
        error.IsRetryable.ShouldBeFalse();
    }

    [Fact]
    public void Create_FromException_With_Retryable()
    {
        var ex = new TimeoutException("Transient error");
        var error = SendError.FromException(ex, isRetryable: true);

        error.IsRetryable.ShouldBeTrue();
        error.Code.ShouldBe("TimeoutException");
    }

    [Fact]
    public void Throw_When_Exception_Is_Null()
    {
        Should.Throw<ArgumentNullException>(() => SendError.FromException(null!));
    }
}

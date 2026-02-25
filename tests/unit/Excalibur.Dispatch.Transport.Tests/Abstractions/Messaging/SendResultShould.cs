// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SendResultShould
{
    [Fact]
    public void Create_Success_Result()
    {
        var before = DateTimeOffset.UtcNow;
        var result = SendResult.Success("msg-42");
        var after = DateTimeOffset.UtcNow;

        result.IsSuccess.ShouldBeTrue();
        result.MessageId.ShouldBe("msg-42");
        result.AcceptedAt.ShouldNotBeNull();
        result.AcceptedAt!.Value.ShouldBeGreaterThanOrEqualTo(before);
        result.AcceptedAt!.Value.ShouldBeLessThanOrEqualTo(after);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Create_Failure_Result()
    {
        var error = new SendError
        {
            Code = "TimeoutException",
            Message = "Connection timed out",
            IsRetryable = true,
        };
        var result = SendResult.Failure(error);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldBeSameAs(error);
        result.MessageId.ShouldBeNull();
    }

    [Fact]
    public void Set_And_Get_SequenceNumber()
    {
        var result = new SendResult { IsSuccess = true, SequenceNumber = 42L };
        result.SequenceNumber.ShouldBe(42L);
    }

    [Fact]
    public void Set_And_Get_Partition()
    {
        var result = new SendResult { IsSuccess = true, Partition = "partition-0" };
        result.Partition.ShouldBe("partition-0");
    }

    [Fact]
    public void Have_Default_Properties_As_Null()
    {
        var result = new SendResult();
        result.MessageId.ShouldBeNull();
        result.SequenceNumber.ShouldBeNull();
        result.Partition.ShouldBeNull();
        result.AcceptedAt.ShouldBeNull();
        result.Error.ShouldBeNull();
    }
}

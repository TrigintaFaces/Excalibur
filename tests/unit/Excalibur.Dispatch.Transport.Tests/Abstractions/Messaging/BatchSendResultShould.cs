// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class BatchSendResultShould
{
    [Fact]
    public void Report_IsCompleteSuccess_When_All_Succeed()
    {
        var result = new BatchSendResult
        {
            TotalMessages = 3,
            SuccessCount = 3,
            FailureCount = 0,
        };

        result.IsCompleteSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Report_Not_CompleteSuccess_When_Failures_Exist()
    {
        var result = new BatchSendResult
        {
            TotalMessages = 3,
            SuccessCount = 2,
            FailureCount = 1,
        };

        result.IsCompleteSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Report_Not_CompleteSuccess_When_SuccessCount_Less_Than_Total()
    {
        var result = new BatchSendResult
        {
            TotalMessages = 5,
            SuccessCount = 3,
            FailureCount = 0,
        };

        result.IsCompleteSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Have_Empty_Results_By_Default()
    {
        var result = new BatchSendResult();
        result.Results.ShouldNotBeNull();
        result.Results.Count.ShouldBe(0);
    }

    [Fact]
    public void Set_And_Get_Results()
    {
        var results = new List<SendResult>
        {
            SendResult.Success("msg-1"),
            SendResult.Success("msg-2"),
        };
        var batchResult = new BatchSendResult { Results = results };
        batchResult.Results.Count.ShouldBe(2);
    }

    [Fact]
    public void Set_And_Get_Duration()
    {
        var duration = TimeSpan.FromMilliseconds(150);
        var result = new BatchSendResult { Duration = duration };
        result.Duration.ShouldBe(duration);
    }

    [Fact]
    public void Have_Default_Duration_As_Null()
    {
        var result = new BatchSendResult();
        result.Duration.ShouldBeNull();
    }
}

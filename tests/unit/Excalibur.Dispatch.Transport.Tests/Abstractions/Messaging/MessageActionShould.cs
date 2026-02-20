// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MessageActionShould
{
    [Fact]
    public void Have_Acknowledge_Value()
    {
        MessageAction.Acknowledge.ShouldBe((MessageAction)0);
    }

    [Fact]
    public void Have_Reject_Value()
    {
        MessageAction.Reject.ShouldBe((MessageAction)1);
    }

    [Fact]
    public void Have_Requeue_Value()
    {
        MessageAction.Requeue.ShouldBe((MessageAction)2);
    }

    [Fact]
    public void Have_Three_Members()
    {
        Enum.GetValues<MessageAction>().Length.ShouldBe(3);
    }
}

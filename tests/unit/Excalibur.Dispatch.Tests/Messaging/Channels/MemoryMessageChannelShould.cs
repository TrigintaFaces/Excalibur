// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
public sealed class MemoryMessageChannelShould
{
    [Fact]
    public void CreateBoundedChannelWithCapacity()
    {
        var channel = MemoryMessageChannel.CreateBounded(100);

        channel.ShouldNotBeNull();
        channel.Reader.ShouldNotBeNull();
        channel.Writer.ShouldNotBeNull();
    }

    [Fact]
    public void CreateUnboundedChannel()
    {
        var channel = MemoryMessageChannel.CreateUnbounded();

        channel.ShouldNotBeNull();
        channel.Reader.ShouldNotBeNull();
        channel.Writer.ShouldNotBeNull();
    }

    [Fact]
    public async Task WriteAndReadFromBoundedChannel()
    {
        var channel = MemoryMessageChannel.CreateBounded(10);
        var envelope = new MessageEnvelope();

        await channel.Writer.WriteAsync(envelope);

        var read = await channel.Reader.ReadAsync();
        read.ShouldNotBeNull();
    }

    [Fact]
    public async Task WriteAndReadFromUnboundedChannel()
    {
        var channel = MemoryMessageChannel.CreateUnbounded();
        var envelope = new MessageEnvelope();

        await channel.Writer.WriteAsync(envelope);

        var read = await channel.Reader.ReadAsync();
        read.ShouldNotBeNull();
    }

    [Fact]
    public async Task HandleMultipleMessagesInOrder()
    {
        var channel = MemoryMessageChannel.CreateBounded(100);

        for (var i = 0; i < 5; i++)
        {
            await channel.Writer.WriteAsync(new MessageEnvelope());
        }

        var readCount = 0;
        while (channel.Reader.TryRead(out _))
        {
            readCount++;
        }

        readCount.ShouldBe(5);
    }

    [Fact]
    public void CreateBoundedWithCustomOptions()
    {
        var options = new Excalibur.Dispatch.Options.Channels.BoundedDispatchChannelOptions
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        };

        var channel = MemoryMessageChannel.CreateBounded(50, options);

        channel.ShouldNotBeNull();
    }
}

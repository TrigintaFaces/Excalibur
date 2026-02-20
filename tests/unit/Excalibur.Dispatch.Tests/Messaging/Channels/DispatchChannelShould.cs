// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
public sealed class DispatchChannelShould : IDisposable
{
    private DispatchChannel<string>? _channel;

    [Fact]
    public void CreateUnboundedChannel()
    {
        _channel = DispatchChannel<string>.CreateUnbounded();

        _channel.Reader.ShouldNotBeNull();
        _channel.Writer.ShouldNotBeNull();
    }

    [Fact]
    public void CreateBoundedChannel()
    {
        _channel = DispatchChannel<string>.CreateBounded(100);

        _channel.Reader.ShouldNotBeNull();
        _channel.Writer.ShouldNotBeNull();
    }

    [Fact]
    public async Task WriteAndReadMessages()
    {
        _channel = DispatchChannel<string>.CreateUnbounded();

        await _channel.Writer.WriteAsync("hello", CancellationToken.None);

        var result = await _channel.Reader.ReadAsync(CancellationToken.None);
        result.ShouldBe("hello");
    }

    [Fact]
    public async Task PreserveMessageOrder()
    {
        _channel = DispatchChannel<string>.CreateBounded(100);

        await _channel.Writer.WriteAsync("first", CancellationToken.None);
        await _channel.Writer.WriteAsync("second", CancellationToken.None);
        await _channel.Writer.WriteAsync("third", CancellationToken.None);

        var r1 = await _channel.Reader.ReadAsync(CancellationToken.None);
        var r2 = await _channel.Reader.ReadAsync(CancellationToken.None);
        var r3 = await _channel.Reader.ReadAsync(CancellationToken.None);

        r1.ShouldBe("first");
        r2.ShouldBe("second");
        r3.ShouldBe("third");
    }

    [Fact]
    public void CreateFromOptions()
    {
        var options = new DispatchChannelOptions
        {
            Mode = ChannelMode.Bounded,
            Capacity = 50,
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        };

        _channel = new DispatchChannel<string>(options);

        _channel.Reader.ShouldNotBeNull();
        _channel.Writer.ShouldNotBeNull();
    }

    [Fact]
    public void ThrowForNullOptions()
    {
        Should.Throw<ArgumentNullException>(() => new DispatchChannel<string>(null!));
    }

    [Fact]
    public void ExposeInnerChannelViaAsChannel()
    {
        _channel = DispatchChannel<string>.CreateUnbounded();

        var inner = _channel.AsChannel();

        inner.ShouldNotBeNull();
    }

    [Fact]
    public void DisposeDoesNotThrow()
    {
        _channel = DispatchChannel<string>.CreateUnbounded();

        Should.NotThrow(() => _channel.Dispose());
    }

    [Fact]
    public async Task HandleConcurrentWriters()
    {
        _channel = DispatchChannel<string>.CreateUnbounded();
        var writeCount = 100;

        var tasks = Enumerable.Range(0, writeCount)
            .Select(i => _channel.Writer.WriteAsync($"msg-{i}", CancellationToken.None).AsTask());
        await Task.WhenAll(tasks);

        var readCount = 0;
        while (_channel.Reader.TryRead(out _))
        {
            readCount++;
        }

        readCount.ShouldBe(writeCount);
    }

    [Fact]
    public void CreateBoundedWithDropOldest()
    {
        _channel = DispatchChannel<string>.CreateBounded(
            capacity: 10,
            fullMode: BoundedChannelFullMode.DropOldest);

        _channel.ShouldNotBeNull();
    }

    public void Dispose()
    {
        _channel?.Dispose();
    }
}

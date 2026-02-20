// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores the ValueTask

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Tests for <see cref="DelegatingTransportSubscriber"/> base class.
/// Verifies all virtual methods forward to the inner subscriber correctly.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DelegatingTransportSubscriberShould
{
    private readonly ITransportSubscriber _innerSubscriber = A.Fake<ITransportSubscriber>();

    [Fact]
    public void Forward_Source_To_InnerSubscriber()
    {
        A.CallTo(() => _innerSubscriber.Source).Returns("test-subscription");
        var sut = new TestDelegatingSubscriber(_innerSubscriber);

        sut.Source.ShouldBe("test-subscription");
    }

    [Fact]
    public async Task Forward_SubscribeAsync_To_InnerSubscriber()
    {
        Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler =
            (_, _) => Task.FromResult(MessageAction.Acknowledge);

        var sut = new TestDelegatingSubscriber(_innerSubscriber);
        await sut.SubscribeAsync(handler, CancellationToken.None);

        A.CallTo(() => _innerSubscriber.SubscribeAsync(handler, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Forward_GetService_To_InnerSubscriber()
    {
        var expected = new object();
        A.CallTo(() => _innerSubscriber.GetService(typeof(string))).Returns(expected);

        var sut = new TestDelegatingSubscriber(_innerSubscriber);
        sut.GetService(typeof(string)).ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task Forward_DisposeAsync_To_InnerSubscriber()
    {
        A.CallTo(() => _innerSubscriber.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var sut = new TestDelegatingSubscriber(_innerSubscriber);
        await sut.DisposeAsync();

        A.CallTo(() => _innerSubscriber.DisposeAsync()).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void Throw_When_InnerSubscriber_Is_Null()
    {
        Should.Throw<ArgumentNullException>(() => new TestDelegatingSubscriber(null!));
    }

    [Fact]
    public void Expose_InnerSubscriber_To_Subclass()
    {
        var sut = new TestDelegatingSubscriber(_innerSubscriber);
        sut.GetInner().ShouldBeSameAs(_innerSubscriber);
    }

    private sealed class TestDelegatingSubscriber(ITransportSubscriber innerSubscriber)
        : DelegatingTransportSubscriber(innerSubscriber)
    {
        public ITransportSubscriber GetInner() => InnerSubscriber;
    }
}

using Excalibur.Dispatch.Channels;

using MessageEnvelope = Excalibur.Dispatch.Abstractions.MessageEnvelope;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MemoryMessageEventArgsShould
{
    [Fact]
    public void StoreEnvelopeAndCancellationToken()
    {
        // Arrange
        var envelope = new MessageEnvelope(A.Fake<Excalibur.Dispatch.Abstractions.IDispatchMessage>());
        using var cts = new CancellationTokenSource();

        // Act
        var args = new MemoryMessageEventArgs(envelope, cts.Token);

        // Assert
        args.Envelope.ShouldBeSameAs(envelope);
        args.CancellationToken.ShouldBe(cts.Token);
    }

    [Fact]
    public void AcceptDefaultCancellationToken()
    {
        var envelope = new MessageEnvelope(A.Fake<Excalibur.Dispatch.Abstractions.IDispatchMessage>());

        var args = new MemoryMessageEventArgs(envelope, CancellationToken.None);

        args.CancellationToken.ShouldBe(CancellationToken.None);
    }

    [Fact]
    public void InheritFromEventArgs()
    {
        var envelope = new MessageEnvelope(A.Fake<Excalibur.Dispatch.Abstractions.IDispatchMessage>());
        var args = new MemoryMessageEventArgs(envelope, CancellationToken.None);

        args.ShouldBeAssignableTo<EventArgs>();
    }
}

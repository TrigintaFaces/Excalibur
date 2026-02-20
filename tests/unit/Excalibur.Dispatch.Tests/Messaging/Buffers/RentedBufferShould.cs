using System.Buffers;

using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Tests.Messaging.Buffers;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class RentedBufferShould
{
    [Fact]
    public void ProvideSpanAccessToBuffer()
    {
        var pool = ArrayPool<byte>.Shared;
        var rawBuffer = pool.Rent(16);
        rawBuffer[0] = 42;
        rawBuffer[1] = 43;

        var buffer = new RentedBuffer(rawBuffer, 2, pool);

        buffer.Span.Length.ShouldBe(2);
        buffer.Span[0].ShouldBe((byte)42);
        buffer.Span[1].ShouldBe((byte)43);

        buffer.Dispose();
    }

    [Fact]
    public void ProvideMemoryAccessToBuffer()
    {
        var pool = ArrayPool<byte>.Shared;
        var rawBuffer = pool.Rent(16);
        rawBuffer[0] = 10;

        var buffer = new RentedBuffer(rawBuffer, 1, pool);

        buffer.Memory.Length.ShouldBe(1);
        buffer.Memory.Span[0].ShouldBe((byte)10);

        buffer.Dispose();
    }

    [Fact]
    public void ExposeBufferAndLength()
    {
        var pool = ArrayPool<byte>.Shared;
        var rawBuffer = pool.Rent(32);
        var buffer = new RentedBuffer(rawBuffer, 10, pool);

        buffer.Buffer.ShouldBeSameAs(rawBuffer);
        buffer.Length.ShouldBe(10);

        buffer.Dispose();
    }

    [Fact]
    public void ImplementEqualityByReferenceAndLength()
    {
        var pool = ArrayPool<byte>.Shared;
        var rawBuffer = pool.Rent(16);
        var buffer1 = new RentedBuffer(rawBuffer, 10, pool);
        var buffer2 = new RentedBuffer(rawBuffer, 10, pool);

        buffer1.Equals(buffer2).ShouldBeTrue();
        (buffer1 == buffer2).ShouldBeTrue();
        (buffer1 != buffer2).ShouldBeFalse();

        buffer1.Dispose();
    }

    [Fact]
    public void DetectInequalityWithDifferentLength()
    {
        var pool = ArrayPool<byte>.Shared;
        var rawBuffer = pool.Rent(16);
        var buffer1 = new RentedBuffer(rawBuffer, 10, pool);
        var buffer2 = new RentedBuffer(rawBuffer, 5, pool);

        buffer1.Equals(buffer2).ShouldBeFalse();
        (buffer1 != buffer2).ShouldBeTrue();

        buffer1.Dispose();
        buffer2.Dispose();
    }

    [Fact]
    public void ImplementObjectEquals()
    {
        var pool = ArrayPool<byte>.Shared;
        var rawBuffer = pool.Rent(16);
        var buffer = new RentedBuffer(rawBuffer, 10, pool);

        buffer.Equals((object)buffer).ShouldBeTrue();
        buffer.Equals((object)"not a buffer").ShouldBeFalse();
        buffer.Equals(null).ShouldBeFalse();

        buffer.Dispose();
    }

    [Fact]
    public void ProduceConsistentHashCode()
    {
        var pool = ArrayPool<byte>.Shared;
        var rawBuffer = pool.Rent(16);
        var buffer1 = new RentedBuffer(rawBuffer, 10, pool);
        var buffer2 = new RentedBuffer(rawBuffer, 10, pool);

        buffer1.GetHashCode().ShouldBe(buffer2.GetHashCode());

        buffer1.Dispose();
    }

    [Fact]
    public void HandleDisposeWithNullBuffer()
    {
        // Default struct has null buffer - should not throw
        var buffer = default(RentedBuffer);
        Should.NotThrow(() => buffer.Dispose());
    }
}

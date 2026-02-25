using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Common.Pools;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MessageBufferPoolShould
{
    [Fact]
    public void RentByteBuffer_ReturnsBuffer_WithAtLeastRequestedLength()
    {
        using var pool = new MessageBufferPool();

        var buffer = pool.RentByteBuffer(128);

        buffer.Length.ShouldBeGreaterThanOrEqualTo(128);
        pool.ReturnByteBuffer(buffer);
    }

    [Fact]
    public void RentCharBuffer_ReturnsBuffer_WithAtLeastRequestedLength()
    {
        using var pool = new MessageBufferPool();

        var buffer = pool.RentCharBuffer(64);

        buffer.Length.ShouldBeGreaterThanOrEqualTo(64);
        pool.ReturnCharBuffer(buffer);
    }

    [Fact]
    public void GetMemory_ReturnsSlice_WithRequestedLength()
    {
        using var pool = new MessageBufferPool();

        var memory = pool.GetMemory(42);

        memory.Length.ShouldBe(42);
    }

    [Fact]
    public void GetReadOnlyMemory_CopiesInputData()
    {
        using var pool = new MessageBufferPool();
        ReadOnlySpan<byte> source = [1, 2, 3, 4, 5];

        var result = pool.GetReadOnlyMemory(source);

        result.Length.ShouldBe(5);
        result.Span.SequenceEqual(source).ShouldBeTrue();
    }

    [Fact]
    public void ReturnMethods_AreNoOps_ForNullBuffers_AndDisposedPool()
    {
        var pool = new MessageBufferPool();
        pool.Dispose();

        pool.ReturnByteBuffer(null);
        pool.ReturnCharBuffer(null);
        pool.ReturnByteBuffer([1, 2, 3], clearArray: true);
        pool.ReturnCharBuffer(['a', 'b'], clearArray: true);
    }

    [Fact]
    public void RentAndMemoryMethods_Throw_WhenPoolIsDisposed()
    {
        using var pool = new MessageBufferPool();
        pool.Dispose();

        Should.Throw<ObjectDisposedException>(() => pool.RentByteBuffer(1));
        Should.Throw<ObjectDisposedException>(() => pool.RentCharBuffer(1));
        Should.Throw<ObjectDisposedException>(() => pool.GetMemory(1));
        Should.Throw<ObjectDisposedException>(() => pool.GetReadOnlyMemory([1]));
    }
}

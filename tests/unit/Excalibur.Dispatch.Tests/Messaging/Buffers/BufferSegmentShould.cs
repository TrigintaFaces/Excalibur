using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Tests.Messaging.Buffers;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BufferSegmentShould
{
    [Fact]
    public void CreateWithValidParameters()
    {
        // Arrange
        var buffer = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var segment = new BufferSegment(buffer, 1, 3);

        // Assert
        segment.Buffer.ShouldBeSameAs(buffer);
        segment.Offset.ShouldBe(1);
        segment.Length.ShouldBe(3);
        segment.PooledBuffer.ShouldBeNull();
    }

    [Fact]
    public void ThrowOnNullByteArrayBuffer()
    {
        byte[] nullBuffer = null!;
        Should.Throw<ArgumentNullException>(() => new BufferSegment(nullBuffer, 0, 0));
    }

    [Fact]
    public void ThrowOnNegativeOffset()
    {
        var buffer = new byte[10];
        Should.Throw<ArgumentOutOfRangeException>(() => new BufferSegment(buffer, -1, 5));
    }

    [Fact]
    public void ThrowOnOffsetBeyondBufferLength()
    {
        var buffer = new byte[5];
        Should.Throw<ArgumentOutOfRangeException>(() => new BufferSegment(buffer, 6, 0));
    }

    [Fact]
    public void ThrowOnNegativeLength()
    {
        var buffer = new byte[10];
        Should.Throw<ArgumentOutOfRangeException>(() => new BufferSegment(buffer, 0, -1));
    }

    [Fact]
    public void ThrowOnLengthExceedingBuffer()
    {
        var buffer = new byte[5];
        Should.Throw<ArgumentOutOfRangeException>(() => new BufferSegment(buffer, 3, 5));
    }

    [Fact]
    public void AllowZeroLengthSegment()
    {
        var buffer = new byte[5];

        var segment = new BufferSegment(buffer, 2, 0);

        segment.Length.ShouldBe(0);
        segment.Offset.ShouldBe(2);
    }

    [Fact]
    public void ProvideMemoryView()
    {
        var buffer = new byte[] { 10, 20, 30, 40, 50 };
        var segment = new BufferSegment(buffer, 1, 3);

        var memory = segment.Memory;

        memory.Length.ShouldBe(3);
        memory.Span[0].ShouldBe((byte)20);
        memory.Span[1].ShouldBe((byte)30);
        memory.Span[2].ShouldBe((byte)40);
    }

    [Fact]
    public void ProvideArraySegment()
    {
        var buffer = new byte[] { 10, 20, 30, 40, 50 };
        var segment = new BufferSegment(buffer, 1, 3);

        var arraySegment = segment.AsArraySegment();

        arraySegment.Array.ShouldBeSameAs(buffer);
        arraySegment.Offset.ShouldBe(1);
        arraySegment.Count.ShouldBe(3);
    }

    [Fact]
    public void ImplementEqualityCorrectly()
    {
        var buffer = new byte[] { 1, 2, 3, 4 };
        var segment1 = new BufferSegment(buffer, 0, 4);
        var segment2 = new BufferSegment(buffer, 0, 4);

        segment1.Equals(segment2).ShouldBeTrue();
        (segment1 == segment2).ShouldBeTrue();
        (segment1 != segment2).ShouldBeFalse();
    }

    [Fact]
    public void DetectInequalityWithDifferentOffset()
    {
        var buffer = new byte[] { 1, 2, 3, 4 };
        var segment1 = new BufferSegment(buffer, 0, 3);
        var segment2 = new BufferSegment(buffer, 1, 3);

        segment1.Equals(segment2).ShouldBeFalse();
        (segment1 != segment2).ShouldBeTrue();
    }

    [Fact]
    public void DetectInequalityWithDifferentBuffer()
    {
        var buffer1 = new byte[] { 1, 2, 3 };
        var buffer2 = new byte[] { 1, 2, 3 };
        var segment1 = new BufferSegment(buffer1, 0, 3);
        var segment2 = new BufferSegment(buffer2, 0, 3);

        segment1.Equals(segment2).ShouldBeFalse();
    }

    [Fact]
    public void ImplementObjectEquals()
    {
        var buffer = new byte[] { 1, 2, 3 };
        var segment = new BufferSegment(buffer, 0, 3);

        segment.Equals((object)segment).ShouldBeTrue();
        segment.Equals((object)"not a segment").ShouldBeFalse();
        segment.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void ProduceConsistentHashCode()
    {
        var buffer = new byte[] { 1, 2, 3 };
        var segment1 = new BufferSegment(buffer, 0, 3);
        var segment2 = new BufferSegment(buffer, 0, 3);

        segment1.GetHashCode().ShouldBe(segment2.GetHashCode());
    }

    [Fact]
    public void ThrowOnNullPooledBuffer()
    {
        Excalibur.Dispatch.Abstractions.IPooledBuffer? nullPooled = null;
        Should.Throw<ArgumentNullException>(() => new BufferSegment(nullPooled, 0, 0));
    }
}

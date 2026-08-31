using System.Buffers;

using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Tests.Messaging.Buffers;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class RentedBufferShould
{
    // ---------------------------------------------------------------------------------------------
    // SAFETY: a rented array must never be returned to the pool twice. A double return puts one
    // array in the pool twice, hands it to two concurrent renters, and -- because the return clears --
    // lets one renter zero the other's live data.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ReturnTheArrayOnlyOnceWhenACopyIsDisposedAfterTheOriginal()
    {
        var pool = new RecordingArrayPool();
        var raw = pool.Rent(16);
        var buffer = new RentedBuffer(raw, 16, pool);

        var copy = buffer;

        buffer.Dispose();
        copy.Dispose();

        pool.ReturnCount.ShouldBe(1);
        pool.LastReturned.ShouldBeSameAs(raw);
    }

    [Fact]
    public void ReturnTheArrayOnlyOnceWhenACopyPassedToAMethodIsDisposed()
    {
        var pool = new RecordingArrayPool();
        var raw = pool.Rent(16);
        var buffer = new RentedBuffer(raw, 16, pool);

        DisposeTheArgument(buffer);
        buffer.Dispose();

        pool.ReturnCount.ShouldBe(1);
        pool.LastReturned.ShouldBeSameAs(raw);
    }

    [Fact]
    public void ReturnTheArrayOnlyOnceWhenTheSameInstanceIsDisposedRepeatedly()
    {
        var pool = new RecordingArrayPool();
        var raw = pool.Rent(16);
        var buffer = new RentedBuffer(raw, 16, pool);

        buffer.Dispose();
        buffer.Dispose();
        buffer.Dispose();

        pool.ReturnCount.ShouldBe(1);
    }

    [Fact]
    public async Task ReturnTheArrayOnlyOnceWhenTwoThreadsDisposeConcurrently()
    {
        // Binds the atomic claim in Dispose: a non-atomic check-then-null can let both threads
        // through and return the array twice.
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var pool = new RecordingArrayPool();
            var raw = pool.Rent(16);
            var buffer = new RentedBuffer(raw, 16, pool);

            using var gate = new ManualResetEventSlim(initialState: false);

            var first = Task.Run(() =>
            {
                gate.Wait();
                buffer.Dispose();
            });
            var second = Task.Run(() =>
            {
                gate.Wait();
                buffer.Dispose();
            });

            gate.Set();
            await Task.WhenAll(first, second);

            pool.ReturnCount.ShouldBe(1, $"attempt {attempt} returned the array more than once");
        }
    }

    [Fact]
    public void RejectAccessAfterTheArrayHasBeenReturned()
    {
        // The array now belongs to whoever rents it next, so reading through a spent owner
        // must fail loudly rather than hand back someone else's live buffer.
        var pool = new RecordingArrayPool();
        var buffer = new RentedBuffer(pool.Rent(16), 16, pool);

        buffer.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => buffer.Buffer);
        _ = Should.Throw<ObjectDisposedException>(() => buffer.Memory);
    }

    // ---------------------------------------------------------------------------------------------
    // LIVENESS: the buffer still works, and the array genuinely goes back to the pool, so an
    // implementation that simply never returns anything cannot satisfy the safety arms above.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ReturnTheArrayToThePoolSoItCanBeRentedAgain()
    {
        var pool = ArrayPool<byte>.Create(maxArrayLength: 1024, maxArraysPerBucket: 4);
        var raw = pool.Rent(16);
        var buffer = new RentedBuffer(raw, 16, pool);

        buffer.Dispose();

        pool.Rent(16).ShouldBeSameAs(raw);
    }

    [Fact]
    public void ProvideSpanAccessToBuffer()
    {
        var pool = ArrayPool<byte>.Create(maxArrayLength: 1024, maxArraysPerBucket: 4);
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
        var pool = ArrayPool<byte>.Create(maxArrayLength: 1024, maxArraysPerBucket: 4);
        var rawBuffer = pool.Rent(16);
        rawBuffer[0] = 10;

        var buffer = new RentedBuffer(rawBuffer, 1, pool);

        buffer.Memory.Length.ShouldBe(1);
        buffer.Memory.Span[0].ShouldBe((byte)10);

        buffer.Dispose();
    }

    [Fact]
    public void RoundTripDataWrittenThroughTheBuffer()
    {
        var pool = ArrayPool<byte>.Create(maxArrayLength: 1024, maxArraysPerBucket: 4);
        var buffer = new RentedBuffer(pool.Rent(16), 4, pool);

        buffer.Span[0] = 10;
        buffer.Span[3] = 40;

        buffer.Memory.Span[0].ShouldBe((byte)10);
        buffer.Memory.Span[3].ShouldBe((byte)40);

        buffer.Dispose();
    }

    [Fact]
    public void ExposeBufferAndLength()
    {
        var pool = ArrayPool<byte>.Create(maxArrayLength: 1024, maxArraysPerBucket: 4);
        var rawBuffer = pool.Rent(32);
        var buffer = new RentedBuffer(rawBuffer, 10, pool);

        buffer.Buffer.ShouldBeSameAs(rawBuffer);
        buffer.Length.ShouldBe(10);

        buffer.Dispose();
    }

    private static void DisposeTheArgument(RentedBuffer buffer) => buffer.Dispose();

    /// <summary>
    /// An isolated pool that counts returns, so a double return is observable without touching
    /// (or corrupting) <see cref="ArrayPool{T}.Shared" />.
    /// </summary>
    private sealed class RecordingArrayPool : ArrayPool<byte>
    {
        private readonly ArrayPool<byte> _inner = Create(maxArrayLength: 1024, maxArraysPerBucket: 8);
        private int _returnCount;
        private byte[]? _lastReturned;

        public int ReturnCount => Volatile.Read(ref _returnCount);

        public byte[]? LastReturned => Volatile.Read(ref _lastReturned);

        public override byte[] Rent(int minimumLength) => _inner.Rent(minimumLength);

        public override void Return(byte[] array, bool clearArray = false)
        {
            _ = Interlocked.Increment(ref _returnCount);
            _ = Interlocked.Exchange(ref _lastReturned, array);
            _inner.Return(array, clearArray);
        }
    }
}

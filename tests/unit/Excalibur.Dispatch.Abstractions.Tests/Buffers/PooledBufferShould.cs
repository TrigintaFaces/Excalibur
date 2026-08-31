using System.Buffers;

namespace Excalibur.Dispatch.Tests.Buffers;

/// <summary>
/// Unit tests for PooledBuffer.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PooledBufferShould : UnitTestBase
{
	[Fact]
	public void Constructor_WithSize_CreatesBuffer()
	{
		// Act
		using var buffer = new PooledBuffer(1024);

		// Assert
		buffer.Size.ShouldBeGreaterThanOrEqualTo(1024);
		buffer.Length.ShouldBe(buffer.Size);
		buffer.Buffer.ShouldNotBeNull();
		buffer.Array.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithSizeAndPool_CreatesBuffer()
	{
		// Act
		using var buffer = new PooledBuffer(512, ArrayPool<byte>.Shared);

		// Assert
		buffer.Size.ShouldBeGreaterThanOrEqualTo(512);
	}

	[Fact]
	public void Constructor_WithNullPool_UsesSharedPool()
	{
		// Act
		var buffer = new PooledBuffer(256, null!);

		// Assert
		buffer.Size.ShouldBeGreaterThanOrEqualTo(256);

		// Disposal is where the fallback is actually observable: the instance now returns to the pool
		// it recorded at construction, so a missing null-to-Shared fallback surfaces here rather than
		// passing silently. Shared itself cannot be intercepted, so this is the assertable half.
		Should.NotThrow(buffer.Dispose);
	}

	[Fact]
	public void Constructor_WithManager_CreatesBuffer()
	{
		// Arrange
		var manager = A.Fake<IPooledBufferService>();
		var data = new byte[128];

		// Act
		using var buffer = new PooledBuffer(manager, data);

		// Assert
		buffer.Buffer.ShouldBe(data);
		buffer.Size.ShouldBe(128);
	}

	[Fact]
	public void Constructor_WithManagerAndClear_CreatesBuffer()
	{
		// Arrange
		var manager = A.Fake<IPooledBufferService>();
		var data = new byte[64];

		// Act
		using var buffer = new PooledBuffer(manager, data, clearOnReturn: false);

		// Assert
		buffer.Buffer.ShouldBe(data);
	}

	[Fact]
	public void Constructor_WithNullManager_ThrowsArgumentNullException()
	{
		Should.Throw<ArgumentNullException>(
			() => new PooledBuffer(null!, new byte[10]));
	}

	[Fact]
	public void Constructor_WithNullBuffer_ThrowsArgumentNullException()
	{
		var manager = A.Fake<IPooledBufferService>();

		Should.Throw<ArgumentNullException>(
			() => new PooledBuffer(manager, null!));
	}

	[Fact]
	public void Memory_ReturnsValidMemory()
	{
		// Arrange
		using var buffer = new PooledBuffer(128);

		// Act
		var memory = buffer.Memory;

		// Assert
		memory.Length.ShouldBe(buffer.Size);
	}

	[Fact]
	public void Span_ReturnsValidSpan()
	{
		// Arrange
		using var buffer = new PooledBuffer(128);

		// Act
		var span = buffer.Span;

		// Assert
		span.Length.ShouldBe(buffer.Size);
	}

	[Fact]
	public void AsSpan_ReturnsSpan()
	{
		// Arrange
		using var buffer = new PooledBuffer(64);

		// Act
		var span = buffer.AsSpan();

		// Assert
		span.Length.ShouldBe(buffer.Size);
	}

	[Fact]
	public void AsMemory_ReturnsMemory()
	{
		// Arrange
		using var buffer = new PooledBuffer(64);

		// Act
		var memory = buffer.AsMemory();

		// Assert
		memory.Length.ShouldBe(buffer.Size);
	}

	[Fact]
	public void Dispose_WithManager_ReturnsBufferToManager()
	{
		// Arrange
		var manager = A.Fake<IPooledBufferService>();
		var data = new byte[32];
		var buffer = new PooledBuffer(manager, data);

		// Act
		buffer.Dispose();

		// Assert
		A.CallTo(() => manager.ReturnBuffer(buffer, A<bool>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Dispose_WithoutManager_ReturnsToSharedPool()
	{
		// Arrange
		var buffer = new PooledBuffer(128);

		// Act & Assert - should not throw
		buffer.Dispose();
	}

	[Fact]
	public void Dispose_CalledMultipleTimes_DoesNotThrow()
	{
		// Arrange
		var buffer = new PooledBuffer(64);

		// Act & Assert
		buffer.Dispose();
		buffer.Dispose();
	}

	[Fact]
	public void Buffer_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var buffer = new PooledBuffer(64);
		buffer.Dispose();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => _ = buffer.Buffer);
	}

	[Fact]
	public void Memory_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var buffer = new PooledBuffer(64);
		buffer.Dispose();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => _ = buffer.Memory);
	}

	[Fact]
	public void Span_AfterDispose_ThrowsObjectDisposedException()
	{
		// Arrange
		var buffer = new PooledBuffer(64);
		buffer.Dispose();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => _ = buffer.Span);
	}

	[Fact]
	public void Size_AfterDispose_ReturnsZero()
	{
		// Arrange
		var buffer = new PooledBuffer(64);
		buffer.Dispose();

		// Act & Assert
		buffer.Size.ShouldBe(0);
	}

	// -------------------------------------------------------------------------------------------
	// SAFETY: the array must reach the pool exactly once. A second return puts one array into the
	// pool twice, hands it to two renters, and -- because the return clears -- lets one of them
	// zero the other's live data.
	// -------------------------------------------------------------------------------------------

	[Fact]
	public async Task ReturnTheBufferOnlyOnceWhenTwoThreadsDisposeConcurrently()
	{
		// This is the arm that binds the defect. Reading a flag and then writing it is not atomic, so
		// two threads can both observe "not yet returned". Depending on how they interleave that shows
		// up two ways, and both are failures: the array reaches the pool twice, or the loser reads the
		// array off a wrapper the winner has already released and throws out of Dispose.
		// The race window is small and its width moves with machine load: against the check-then-act
		// implementation this arm has been observed catching anywhere from 3 to 68 anomalies per 500
		// attempts. The count is set high enough that the low end still cannot slip through a run.
		const int Attempts = 2000;
		var wrongReturnCount = 0;
		var disposeThrew = 0;

		for (var attempt = 0; attempt < Attempts; attempt++)
		{
			var service = new CountingBufferService();
			var buffer = new PooledBuffer(service, new byte[64]);

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

			try
			{
				await Task.WhenAll(first, second);
			}
#pragma warning disable CA1031 // the point of the arm is that Dispose must not throw at all
			catch (Exception)
#pragma warning restore CA1031
			{
				disposeThrew++;
			}

			if (service.ReturnCount != 1)
			{
				wrongReturnCount++;
			}
		}

		// One assertion over both counts, so a failure always reports the full picture rather than
		// short-circuiting on whichever interleaving happened to dominate this run.
		(disposeThrew + wrongReturnCount).ShouldBe(
			0,
			$"of {Attempts} concurrent disposals, Dispose threw on {disposeThrew} and the buffer failed to "
			+ $"reach the pool exactly once on {wrongReturnCount}");
	}

	[Fact]
	public void ReturnTheBufferOnlyOnceWhenDisposedRepeatedly()
	{
		// Arrange
		var service = new CountingBufferService();
		var buffer = new PooledBuffer(service, new byte[64]);

		// Act
		buffer.Dispose();
		buffer.Dispose();
		buffer.Dispose();

		// Assert
		service.ReturnCount.ShouldBe(1);
	}

	[Fact]
	public void ReturnTheBufferOnlyOnceWhenASecondReferenceIsDisposed()
	{
		// Arrange
		var service = new CountingBufferService();
		var buffer = new PooledBuffer(service, new byte[64]);
		var alias = buffer;

		// Act
		buffer.Dispose();
		alias.Dispose();

		// Assert
		service.ReturnCount.ShouldBe(1);
	}

	[Fact]
	public void ExposeTheArrayToTheBufferServiceDuringTheReturn()
	{
		// A buffer service is handed the wrapper, not the array, and reads the array back off it.
		// Releasing the field before the return would throw ObjectDisposedException straight out of
		// Dispose and leak the buffer.
		var service = new CountingBufferService();
		var data = new byte[64];
		var buffer = new PooledBuffer(service, data);

		Should.NotThrow(buffer.Dispose);

		service.LastReturnedArray.ShouldBeSameAs(data);
	}

	// -------------------------------------------------------------------------------------------
	// LIVENESS: the buffer still reaches the pool. Without this, an implementation that never
	// returns anything would satisfy every safety arm above.
	// -------------------------------------------------------------------------------------------

	[Fact]
	public void ReturnTheBufferExactlyOnceOnASingleDispose()
	{
		// Arrange
		var service = new CountingBufferService();
		var buffer = new PooledBuffer(service, new byte[64]);

		// Act
		buffer.Dispose();

		// Assert
		service.ReturnCount.ShouldBe(1);
	}

	// -------------------------------------------------------------------------------------------
	// SAFETY: the array must go back to the pool it was RENTED from. Returning it to
	// ArrayPool<byte>.Shared instead starves the caller's pool of an array permanently and hands
	// Shared -- which is process-wide -- an array it never allocated.
	//
	// These arms use a pool that is DISTINCT from Shared, which is the whole point: the pre-existing
	// coverage passed ArrayPool<byte>.Shared, so the two pools coincided and the defect could not be
	// expressed, let alone observed.
	// -------------------------------------------------------------------------------------------

	[Fact]
	public void ReturnTheArrayToThePoolItWasRentedFromAndNoOther()
	{
		// Arrange
		var rentedFrom = new RecordingArrayPool();
		var other = new RecordingArrayPool();
		var buffer = new PooledBuffer(64, rentedFrom);
		var array = buffer.Buffer;

		// Act
		buffer.Dispose();

		// Assert
		rentedFrom.ReturnCount.ShouldBe(1);
		rentedFrom.LastReturned.ShouldBeSameAs(array);
		other.ReturnCount.ShouldBe(0);
	}

	[Fact]
	public void MakeTheArrayAvailableAgainFromTheSameCustomPool()
	{
		// ArrayPool<byte>.Shared cannot be intercepted and its buckets are process-wide, so "Shared
		// never received it" is not directly assertable. This is the deterministic equivalent: the
		// array is demonstrably back inside the custom pool, so it is not sitting in Shared. Returning
		// to the wrong pool leaves this bucket empty and Rent hands back a freshly allocated array.
		var pool = ArrayPool<byte>.Create(maxArrayLength: 1024, maxArraysPerBucket: 4);
		var buffer = new PooledBuffer(64, pool);
		var array = buffer.Buffer;

		buffer.Dispose();

		// ShouldBeSameAs is reference equality; a wrong-pool return yields a different instance with
		// identical (zeroed) content, so spell that out rather than printing two identical byte dumps.
		pool.Rent(64).ShouldBeSameAs(
			array,
			"the custom pool handed back a different array instance, so the original was returned "
			+ "somewhere else and this pool had to allocate a replacement");
	}

	[Fact]
	public void ReturnTheArrayToTheCustomPoolOnlyOnceWhenDisposedRepeatedly()
	{
		// Arrange
		var pool = new RecordingArrayPool();
		var buffer = new PooledBuffer(64, pool);

		// Act
		buffer.Dispose();
		buffer.Dispose();

		// Assert
		pool.ReturnCount.ShouldBe(1);
	}

	/// <summary>
	/// An array pool that counts returns, so returning to the wrong pool is observable. Distinct from
	/// <see cref="ArrayPool{T}.Shared" /> by construction, which is what makes the defect expressible.
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

	/// <summary>
	/// A buffer service that counts returns. Implemented directly against the interface rather than
	/// faked, so the count is thread-safe under the concurrent arm and so the return path exercises
	/// the same "read the array off the wrapper" step a real service performs.
	/// </summary>
	private sealed class CountingBufferService : IPooledBufferService
	{
		private int _returnCount;
		private byte[]? _lastReturnedArray;

		public int ReturnCount => Volatile.Read(ref _returnCount);

		public byte[]? LastReturnedArray => Volatile.Read(ref _lastReturnedArray);

		public int RentedBuffers => 0;

		public int LargestBufferRequested => 0;

		public IDisposablePooledBuffer RentBuffer(int minimumLength, bool clearBuffer = false) =>
			new PooledBuffer(this, new byte[minimumLength]);

		public void ReturnBuffer(IPooledBuffer buffer, bool clearBuffer = true)
		{
			ArgumentNullException.ThrowIfNull(buffer);

			// A real service reads the array off the wrapper here; this must not throw.
			_ = Interlocked.Exchange(ref _lastReturnedArray, buffer.Buffer);
			_ = Interlocked.Increment(ref _returnCount);
		}

		public BufferPoolStatistics GetStatistics() => new();
	}
}

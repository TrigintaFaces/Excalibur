using System.Buffers;

namespace Excalibur.Dispatch.Abstractions.Tests.Buffers;

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
		using var buffer = new PooledBuffer(256, null!);

		// Assert
		buffer.Size.ShouldBeGreaterThanOrEqualTo(256);
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
}

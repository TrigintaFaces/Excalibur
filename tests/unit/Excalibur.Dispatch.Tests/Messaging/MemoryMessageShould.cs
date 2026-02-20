using System.Buffers;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MemoryMessageShould
{
	[Fact]
	public void CreateWithMemoryOwner()
	{
		// Arrange
		var owner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(owner);

		// Assert
		message.ContentType.ShouldBe("application/octet-stream");
		message.OwnsMemory.ShouldBeTrue();
		message.Body.Length.ShouldBeGreaterThan(0);
		Guid.TryParse(message.MessageId, out _).ShouldBeTrue();
		message.Timestamp.ShouldNotBe(default);
		message.Headers.ShouldNotBeNull();
		message.Headers.Count.ShouldBe(0);
		message.MessageType.ShouldBe("MemoryMessage");
		message.Features.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithCustomContentType()
	{
		var owner = MemoryPool<byte>.Shared.Rent(50);

		using var message = new MemoryMessage(owner, "application/json");

		message.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void CreateWithBorrowedMemory()
	{
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var memory = new Memory<byte>(buffer);

		using var message = new MemoryMessage(memory, "application/octet-stream");

		message.OwnsMemory.ShouldBeFalse();
		message.Body.Length.ShouldBe(5);
		message.ContentType.ShouldBe("application/octet-stream");
	}

	[Fact]
	public void ThrowOnNullMemoryOwner()
	{
		Should.Throw<ArgumentNullException>(() => new MemoryMessage((IMemoryOwner<byte>)null!, "text/plain"));
	}

	[Fact]
	public void ThrowOnNullContentTypeWithOwner()
	{
		var owner = MemoryPool<byte>.Shared.Rent(10);
		Should.Throw<ArgumentNullException>(() => new MemoryMessage(owner, null!));
	}

	[Fact]
	public void ThrowOnNullContentTypeWithBorrowedMemory()
	{
		Should.Throw<ArgumentNullException>(() => new MemoryMessage(Memory<byte>.Empty, null!));
	}

	[Fact]
	public void ReturnActionKind()
	{
		var owner = MemoryPool<byte>.Shared.Rent(10);
		using var message = new MemoryMessage(owner);

		message.Kind.ShouldBe(MessageKinds.Action);
	}

	[Fact]
	public void ParseMessageIdAsGuid()
	{
		var owner = MemoryPool<byte>.Shared.Rent(10);
		using var message = new MemoryMessage(owner);

		message.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void HandleDoubleDispose()
	{
		var owner = MemoryPool<byte>.Shared.Rent(10);
		var message = new MemoryMessage(owner);

		// Should not throw
		message.Dispose();
		message.Dispose();
	}

	[Fact]
	public void NotDisposeUnownedMemory()
	{
		var buffer = new byte[] { 1, 2, 3 };
		var message = new MemoryMessage(new Memory<byte>(buffer), "text/plain");

		// Should not throw — doesn't own memory
		message.Dispose();

		// Buffer should still be valid
		buffer[0].ShouldBe((byte)1);
	}
}

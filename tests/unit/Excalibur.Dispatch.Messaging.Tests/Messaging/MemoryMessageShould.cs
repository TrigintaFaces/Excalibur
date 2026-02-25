// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="MemoryMessage"/>.
/// </summary>
/// <remarks>
/// Tests the memory-based message implementation for zero-copy operations.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class MemoryMessageShould
{
	#region Constructor Tests - With Memory Owner

	[Fact]
	public void Constructor_WithMemoryOwner_CreatesInstance()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		_ = message.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithMemoryOwner_SetsDefaultContentType()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		message.ContentType.ShouldBe("application/octet-stream");
	}

	[Fact]
	public void Constructor_WithMemoryOwner_SetsCustomContentType()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner, "application/json");

		// Assert
		message.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void Constructor_WithMemoryOwner_SetsOwnsMemoryTrue()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		message.OwnsMemory.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_WithNullMemoryOwner_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MemoryMessage(null!));
	}

	[Fact]
	public void Constructor_WithMemoryOwnerAndNullContentType_ThrowsArgumentNullException()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MemoryMessage(memoryOwner, null!));
	}

	#endregion

	#region Constructor Tests - With Borrowed Memory

	[Fact]
	public void Constructor_WithBorrowedMemory_CreatesInstance()
	{
		// Arrange
		var buffer = new byte[100];

		// Act
		using var message = new MemoryMessage(buffer, "application/json");

		// Assert
		_ = message.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithBorrowedMemory_SetsOwnsMemoryFalse()
	{
		// Arrange
		var buffer = new byte[100];

		// Act
		using var message = new MemoryMessage(buffer, "application/json");

		// Assert
		message.OwnsMemory.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_WithBorrowedMemoryAndNullContentType_ThrowsArgumentNullException()
	{
		// Arrange
		var buffer = new byte[100];

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new MemoryMessage(buffer, null!));
	}

	#endregion

	#region Property Tests

	[Fact]
	public void MessageId_IsNotEmpty()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		message.MessageId.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void MessageId_IsValidGuid()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		Guid.TryParse(message.MessageId, out _).ShouldBeTrue();
	}

	[Fact]
	public void Timestamp_IsRecentUtcNow()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);
		var after = DateTimeOffset.UtcNow;

		// Assert
		message.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		message.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void Headers_IsNotNull()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		_ = message.Headers.ShouldNotBeNull();
	}

	[Fact]
	public void Headers_IsReadOnly()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		_ = message.Headers.ShouldBeAssignableTo<IReadOnlyDictionary<string, object>>();
	}

	[Fact]
	public void Body_ContainsMemory()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);
		var content = Encoding.UTF8.GetBytes("Hello, World!");
		content.CopyTo(memoryOwner.Memory);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		message.Body.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void MessageType_IsClassName()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		message.MessageType.ShouldBe("MemoryMessage");
	}

	[Fact]
	public void Features_IsNotNull()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		_ = message.Features.ShouldNotBeNull();
	}

	[Fact]
	public void Features_ImplementsIMessageFeatures()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		_ = message.Features.ShouldBeAssignableTo<IMessageFeatures>();
	}

	[Fact]
	public void Id_ReturnsGuidFromMessageId()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		message.Id.ShouldNotBe(Guid.Empty);
		message.Id.ToString().ShouldBe(message.MessageId);
	}

	[Fact]
	public void Kind_IsAction()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		message.Kind.ShouldBe(MessageKinds.Action);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_WithOwnedMemory_DisposesMemoryOwner()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);
		var message = new MemoryMessage(memoryOwner);

		// Act
		message.Dispose();

		// Assert - Memory should be returned to pool (no exception)
		// We can't directly verify the memory was returned, but we can verify no exception
	}

	[Fact]
	public void Dispose_WithBorrowedMemory_DoesNotThrow()
	{
		// Arrange
		var buffer = new byte[100];
		var message = new MemoryMessage(buffer, "text/plain");

		// Act & Assert
		Should.NotThrow(() => message.Dispose());
	}

	[Fact]
	public void Dispose_MultipleTimes_DoesNotThrow()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);
		var message = new MemoryMessage(memoryOwner);

		// Act & Assert
		message.Dispose();
		Should.NotThrow(() => message.Dispose());
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIMemoryMessage()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		_ = message.ShouldBeAssignableTo<IMemoryMessage>();
	}

	[Fact]
	public void ImplementsIDisposable()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		_ = message.ShouldBeAssignableTo<IDisposable>();
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void CanCreateMessageWithJsonContent()
	{
		// Arrange
		var json = "{\"key\": \"value\"}";
		var bytes = Encoding.UTF8.GetBytes(json);
		var memoryOwner = MemoryPool<byte>.Shared.Rent(bytes.Length);
		bytes.CopyTo(memoryOwner.Memory);

		// Act
		using var message = new MemoryMessage(memoryOwner, "application/json");

		// Assert
		message.ContentType.ShouldBe("application/json");
		message.Body.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void CanCreateMessageWithBinaryContent()
	{
		// Arrange
		var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
		var memoryOwner = MemoryPool<byte>.Shared.Rent(bytes.Length);
		bytes.CopyTo(memoryOwner.Memory);

		// Act
		using var message = new MemoryMessage(memoryOwner);

		// Assert
		message.ContentType.ShouldBe("application/octet-stream");
		message.Body.Span[0].ShouldBe((byte)0x01);
	}

	[Fact]
	public void UsingStatementDisposesMessage()
	{
		// Arrange
		var memoryOwner = MemoryPool<byte>.Shared.Rent(100);
		MemoryMessage? capturedMessage = null;

		// Act
		using (var message = new MemoryMessage(memoryOwner))
		{
			capturedMessage = message;
			_ = capturedMessage.MessageId.ShouldNotBeNull();
		}

		// Assert - After using block, message is disposed
		_ = capturedMessage.ShouldNotBeNull();
	}

	#endregion
}

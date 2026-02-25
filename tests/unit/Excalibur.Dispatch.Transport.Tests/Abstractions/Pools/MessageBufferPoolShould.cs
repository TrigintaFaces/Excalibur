// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Pools;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class MessageBufferPoolShould : IDisposable
{
	private readonly MessageBufferPool _sut = new();

	[Fact]
	public void RentByteBufferWithRequestedMinimumLength()
	{
		// Act
		var buffer = _sut.RentByteBuffer(1024);

		// Assert
		buffer.ShouldNotBeNull();
		buffer.Length.ShouldBeGreaterThanOrEqualTo(1024);
	}

	[Fact]
	public void ReturnByteBufferSafely()
	{
		// Arrange
		var buffer = _sut.RentByteBuffer(512);

		// Act & Assert - should not throw
		_sut.ReturnByteBuffer(buffer);
	}

	[Fact]
	public void ReturnByteBufferWithClearArray()
	{
		// Arrange
		var buffer = _sut.RentByteBuffer(256);
		buffer[0] = 0xFF;

		// Act & Assert - should not throw
		_sut.ReturnByteBuffer(buffer, clearArray: true);
	}

	[Fact]
	public void ReturnNullByteBufferSafely()
	{
		// Act & Assert - should not throw
		_sut.ReturnByteBuffer(null);
	}

	[Fact]
	public void RentCharBufferWithRequestedMinimumLength()
	{
		// Act
		var buffer = _sut.RentCharBuffer(1024);

		// Assert
		buffer.ShouldNotBeNull();
		buffer.Length.ShouldBeGreaterThanOrEqualTo(1024);
	}

	[Fact]
	public void ReturnCharBufferSafely()
	{
		// Arrange
		var buffer = _sut.RentCharBuffer(512);

		// Act & Assert - should not throw
		_sut.ReturnCharBuffer(buffer);
	}

	[Fact]
	public void ReturnCharBufferWithClearArray()
	{
		// Arrange
		var buffer = _sut.RentCharBuffer(256);
		buffer[0] = 'A';

		// Act & Assert - should not throw
		_sut.ReturnCharBuffer(buffer, clearArray: true);
	}

	[Fact]
	public void ReturnNullCharBufferSafely()
	{
		// Act & Assert - should not throw
		_sut.ReturnCharBuffer(null);
	}

	[Fact]
	public void GetMemoryWithRequestedLength()
	{
		// Act
		var memory = _sut.GetMemory(128);

		// Assert
		memory.Length.ShouldBe(128);
	}

	[Fact]
	public void GetReadOnlyMemoryWithCorrectData()
	{
		// Arrange
		var data = new byte[] { 1, 2, 3, 4, 5 };

		// Act
		var memory = _sut.GetReadOnlyMemory(data);

		// Assert
		memory.Length.ShouldBe(5);
		memory.Span[0].ShouldBe((byte)1);
		memory.Span[4].ShouldBe((byte)5);
	}

	[Fact]
	public void ThrowObjectDisposedExceptionWhenRentingByteBufferAfterDispose()
	{
		// Arrange
		_sut.Dispose();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => _sut.RentByteBuffer(64));
	}

	[Fact]
	public void ThrowObjectDisposedExceptionWhenRentingCharBufferAfterDispose()
	{
		// Arrange
		_sut.Dispose();

		// Act & Assert
		Should.Throw<ObjectDisposedException>(() => _sut.RentCharBuffer(64));
	}

	[Fact]
	public void ReturnByteBufferSafelyWhenDisposed()
	{
		// Arrange
		var buffer = _sut.RentByteBuffer(64);
		_sut.Dispose();

		// Act & Assert - should not throw
		_sut.ReturnByteBuffer(buffer);
	}

	[Fact]
	public void ReturnCharBufferSafelyWhenDisposed()
	{
		// Arrange
		var buffer = _sut.RentCharBuffer(64);
		_sut.Dispose();

		// Act & Assert - should not throw
		_sut.ReturnCharBuffer(buffer);
	}

	[Fact]
	public void SupportCustomMaxBufferSize()
	{
		// Arrange
		using var pool = new MessageBufferPool(maxBufferSize: 1024);

		// Act
		var buffer = pool.RentByteBuffer(2048);

		// Assert - should cap at max
		buffer.ShouldNotBeNull();
		buffer.Length.ShouldBeGreaterThanOrEqualTo(1024);
	}

	[Fact]
	public void HandleDoubleDispose()
	{
		// Act & Assert - should not throw
		_sut.Dispose();
		_sut.Dispose();
	}

	public void Dispose()
	{
		_sut.Dispose();
	}
}

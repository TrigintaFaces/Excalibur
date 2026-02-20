// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Excalibur.Dispatch.Abstractions.Serialization;

#pragma warning disable CA2263 // Prefer generic overload — we are intentionally testing non-generic overloads

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="Utf8JsonSerializerExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class Utf8JsonSerializerExtensionsShould
{
	#region Generic Sync Overloads

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public void SerializeToUtf8Bytes_Generic_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.SerializeToUtf8Bytes<string>(null!, "test"));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public void SerializeToUtf8Bytes_Generic_DelegatesToSerializer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var expected = new byte[] { 1, 2, 3 };
		A.CallTo(() => serializer.SerializeToUtf8Bytes(A<object?>._, typeof(string))).Returns(expected);

		// Act
		var result = serializer.SerializeToUtf8Bytes("test");

		// Assert
		result.ShouldBe(expected);
		A.CallTo(() => serializer.SerializeToUtf8Bytes("test", typeof(string))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SerializeToUtf8_Generic_ThrowsOnNullSerializer()
	{
		var writer = A.Fake<IBufferWriter<byte>>();
		Should.Throw<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.SerializeToUtf8<string>(null!, writer, "test"));
	}

	[Fact]
	public void SerializeToUtf8_Generic_DelegatesToSerializer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var writer = A.Fake<IBufferWriter<byte>>();

		// Act
		serializer.SerializeToUtf8(writer, "hello");

		// Assert
		A.CallTo(() => serializer.SerializeToUtf8(writer, "hello", typeof(string))).MustHaveHappenedOnceExactly();
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public void DeserializeFromUtf8_Generic_Span_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.DeserializeFromUtf8<string>(null!, ReadOnlySpan<byte>.Empty));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public void DeserializeFromUtf8_Generic_Span_DelegatesToSerializer()
	{
		// Arrange - Use a concrete implementation since FakeItEasy can't intercept ReadOnlySpan<byte> params
		var mock = new MockUtf8JsonSerializer("hello");

		// Act
		var result = mock.DeserializeFromUtf8<string>(new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes("\"hello\"")));

		// Assert
		result.ShouldBe("hello");
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public void DeserializeFromUtf8_Generic_Memory_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.DeserializeFromUtf8<string>(null!, ReadOnlyMemory<byte>.Empty));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public void DeserializeFromUtf8_Generic_Memory_DelegatesToSerializer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var utf8Bytes = Encoding.UTF8.GetBytes("\"hello\"");
		A.CallTo(() => serializer.DeserializeFromUtf8(A<ReadOnlyMemory<byte>>._, typeof(string))).Returns("hello");

		// Act
		var result = serializer.DeserializeFromUtf8<string>(new ReadOnlyMemory<byte>(utf8Bytes));

		// Assert
		result.ShouldBe("hello");
	}

	#endregion

	#region Async Overloads

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task SerializeToUtf8BytesAsync_WithType_ThrowsOnNullSerializer()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.SerializeToUtf8BytesAsync(null!, (object?)"test", typeof(string)));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task SerializeToUtf8BytesAsync_WithType_DelegatesToSerializer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var expected = new byte[] { 10, 20, 30 };
		A.CallTo(() => serializer.SerializeToUtf8Bytes(A<object?>._, typeof(string))).Returns(expected);

		// Act
		var result = await serializer.SerializeToUtf8BytesAsync((object?)"test", typeof(string)).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task SerializeToUtf8BytesAsync_WithTypeAndCt_ThrowsOnNullSerializer()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.SerializeToUtf8BytesAsync(null!, (object?)"test", typeof(string), CancellationToken.None));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task SerializeToUtf8BytesAsync_WithTypeAndCt_ThrowsOnCancellation()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => serializer.SerializeToUtf8BytesAsync((object?)"test", typeof(string), cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task SerializeToUtf8BytesAsync_WithTypeAndCt_DelegatesToSerializer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var expected = new byte[] { 1, 2 };
		A.CallTo(() => serializer.SerializeToUtf8Bytes(A<object?>._, typeof(int))).Returns(expected);

		// Act
		var result = await serializer.SerializeToUtf8BytesAsync((object?)42, typeof(int), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task SerializeToUtf8BytesAsync_Generic_ThrowsOnNullSerializer()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.SerializeToUtf8BytesAsync<string>(null!, "test"));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task SerializeToUtf8BytesAsync_Generic_DelegatesToSerializer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var expected = new byte[] { 5, 6, 7 };
		A.CallTo(() => serializer.SerializeToUtf8Bytes(A<object?>._, typeof(string))).Returns(expected);

		// Act
		var result = await serializer.SerializeToUtf8BytesAsync("value").ConfigureAwait(false);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task SerializeToUtf8BytesAsync_GenericWithCt_ThrowsOnNullSerializer()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.SerializeToUtf8BytesAsync<string>(null!, "test", CancellationToken.None));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task SerializeToUtf8BytesAsync_GenericWithCt_ThrowsOnCancellation()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => serializer.SerializeToUtf8BytesAsync("test", cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task SerializeToUtf8BytesAsync_GenericWithCt_DelegatesToSerializer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var expected = new byte[] { 8, 9 };
		A.CallTo(() => serializer.SerializeToUtf8Bytes(A<object?>._, typeof(int))).Returns(expected);

		// Act
		var result = await serializer.SerializeToUtf8BytesAsync(99, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task DeserializeFromUtf8Async_WithType_ThrowsOnNullSerializer()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.DeserializeFromUtf8Async(null!, ReadOnlyMemory<byte>.Empty, typeof(string)));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task DeserializeFromUtf8Async_WithType_DelegatesToSerializer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var bytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("\"hello\""));
		A.CallTo(() => serializer.DeserializeFromUtf8(A<ReadOnlyMemory<byte>>._, typeof(string))).Returns("hello");

		// Act
		var result = await serializer.DeserializeFromUtf8Async(bytes, typeof(string)).ConfigureAwait(false);

		// Assert
		result.ShouldBe("hello");
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task DeserializeFromUtf8Async_WithTypeAndCt_ThrowsOnNullSerializer()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.DeserializeFromUtf8Async(
				null!, ReadOnlyMemory<byte>.Empty, typeof(string), CancellationToken.None));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task DeserializeFromUtf8Async_WithTypeAndCt_ThrowsOnCancellation()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => serializer.DeserializeFromUtf8Async(
				ReadOnlyMemory<byte>.Empty, typeof(string), cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task DeserializeFromUtf8Async_WithTypeAndCt_DelegatesToSerializer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var bytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("42"));
		A.CallTo(() => serializer.DeserializeFromUtf8(A<ReadOnlyMemory<byte>>._, typeof(int))).Returns(42);

		// Act
		var result = await serializer.DeserializeFromUtf8Async(bytes, typeof(int), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(42);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task DeserializeFromUtf8Async_Generic_ThrowsOnNullSerializer()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.DeserializeFromUtf8Async<string>(null!, ReadOnlyMemory<byte>.Empty));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task DeserializeFromUtf8Async_Generic_DelegatesToSerializer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var bytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("\"test\""));
		A.CallTo(() => serializer.DeserializeFromUtf8(A<ReadOnlyMemory<byte>>._, typeof(string))).Returns("test");

		// Act
		var result = await serializer.DeserializeFromUtf8Async<string>(bytes).ConfigureAwait(false);

		// Assert
		result.ShouldBe("test");
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task DeserializeFromUtf8Async_GenericWithCt_ThrowsOnNullSerializer()
	{
		await Should.ThrowAsync<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.DeserializeFromUtf8Async<string>(
				null!, ReadOnlyMemory<byte>.Empty, CancellationToken.None));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task DeserializeFromUtf8Async_GenericWithCt_ThrowsOnCancellation()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => serializer.DeserializeFromUtf8Async<string>(ReadOnlyMemory<byte>.Empty, cts.Token)).ConfigureAwait(false);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public async Task DeserializeFromUtf8Async_GenericWithCt_DelegatesToSerializer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var bytes = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("\"value\""));
		A.CallTo(() => serializer.DeserializeFromUtf8(A<ReadOnlyMemory<byte>>._, typeof(string))).Returns("value");

		// Act
		var result = await serializer.DeserializeFromUtf8Async<string>(bytes, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("value");
	}

	#endregion

	#region DeserializeFromString<T> Tests

	[Fact]
	public void DeserializeFromString_Generic_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.DeserializeFromString<string>(null!, "{}"));
	}

	[Fact]
	public void DeserializeFromString_Generic_ThrowsOnNullJson()
	{
		var serializer = A.Fake<IUtf8JsonSerializer>();
		Should.Throw<ArgumentNullException>(
			() => serializer.DeserializeFromString<string>(null!));
	}

	[Fact]
	public void DeserializeFromString_Generic_ThrowsOnWhitespaceJson()
	{
		var serializer = A.Fake<IUtf8JsonSerializer>();
		Should.Throw<ArgumentException>(
			() => serializer.DeserializeFromString<string>("   "));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public void DeserializeFromString_Generic_ConvertsToUtf8AndDeserializes()
	{
		// Arrange - Use mock since FakeItEasy can't match ReadOnlySpan<byte>
		var mock = new MockUtf8JsonSerializer("result");

		// Act
		var result = mock.DeserializeFromString<string>("{\"key\":\"result\"}");

		// Assert
		result.ShouldBe("result");
	}

	#endregion

	#region DeserializeFromString (non-generic) Tests

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public void DeserializeFromString_NonGeneric_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.DeserializeFromString(null!, "{}", typeof(string)));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public void DeserializeFromString_NonGeneric_ThrowsOnNullJson()
	{
		var serializer = A.Fake<IUtf8JsonSerializer>();
		Should.Throw<ArgumentNullException>(
			() => serializer.DeserializeFromString(null!, typeof(string)));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public void DeserializeFromString_NonGeneric_ThrowsOnNullType()
	{
		var serializer = A.Fake<IUtf8JsonSerializer>();
		Should.Throw<ArgumentNullException>(
			() => serializer.DeserializeFromString("{}", null!));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	public void DeserializeFromString_NonGeneric_ConvertsToUtf8AndDeserializes()
	{
		// Arrange - Use mock since FakeItEasy can't match ReadOnlySpan<byte>
		var mock = new MockUtf8JsonSerializer(42);

		// Act
		var result = mock.DeserializeFromString("{\"value\":42}", typeof(int));

		// Assert
		result.ShouldBe(42);
	}

	#endregion

	#region SerializeToString<T> Tests

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public void SerializeToString_Generic_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.SerializeToString(null!, "test"));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public void SerializeToString_Generic_ReturnsUtf8String()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var expectedBytes = Encoding.UTF8.GetBytes("{\"value\":\"hello\"}");
		A.CallTo(() => serializer.SerializeToUtf8Bytes(A<object?>.That.IsEqualTo("test"), typeof(string))).Returns(expectedBytes);

		// Act
		var result = serializer.SerializeToString("test");

		// Assert
		result.ShouldBe("{\"value\":\"hello\"}");
	}

	#endregion

	#region SerializeToString (non-generic) Tests

	[Fact]
	public void SerializeToString_NonGeneric_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.SerializeToString(null!, "test", typeof(string)));
	}

	[Fact]
	public void SerializeToString_NonGeneric_ThrowsOnNullType()
	{
		var serializer = A.Fake<IUtf8JsonSerializer>();
		Should.Throw<ArgumentNullException>(
			() => serializer.SerializeToString("test", null!));
	}

	[Fact]
	public void SerializeToString_NonGeneric_ReturnsUtf8String()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var expectedBytes = Encoding.UTF8.GetBytes("{\"name\":\"world\"}");
		A.CallTo(() => serializer.SerializeToUtf8Bytes(A<object?>._, typeof(string))).Returns(expectedBytes);

		// Act
		var result = serializer.SerializeToString((object?)"test", typeof(string));

		// Assert
		result.ShouldBe("{\"name\":\"world\"}");
	}

	#endregion

	#region SerializeToPooledUtf8Buffer Tests

	[Fact]
	public void SerializeToPooledUtf8Buffer_Generic_ThrowsOnNullSerializer()
	{
		var bufferManager = A.Fake<IPooledBufferService>();
		Should.Throw<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.SerializeToPooledUtf8Buffer(null!, "test", bufferManager));
	}

	[Fact]
	public void SerializeToPooledUtf8Buffer_Generic_ThrowsOnNullBufferManager()
	{
		var serializer = A.Fake<IUtf8JsonSerializer>();
		Should.Throw<ArgumentNullException>(
			() => serializer.SerializeToPooledUtf8Buffer("test", null!));
	}

	[Fact]
	public void SerializeToPooledUtf8Buffer_NonGeneric_ThrowsOnNullSerializer()
	{
		var bufferManager = A.Fake<IPooledBufferService>();
		Should.Throw<ArgumentNullException>(
			() => Utf8JsonSerializerExtensions.SerializeToPooledUtf8Buffer(null!, "test", typeof(string), bufferManager));
	}

	[Fact]
	public void SerializeToPooledUtf8Buffer_NonGeneric_ThrowsOnNullType()
	{
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var bufferManager = A.Fake<IPooledBufferService>();
		Should.Throw<ArgumentNullException>(
			() => serializer.SerializeToPooledUtf8Buffer("test", null!, bufferManager));
	}

	[Fact]
	public void SerializeToPooledUtf8Buffer_NonGeneric_ThrowsOnNullBufferManager()
	{
		var serializer = A.Fake<IUtf8JsonSerializer>();
		Should.Throw<ArgumentNullException>(
			() => serializer.SerializeToPooledUtf8Buffer("test", typeof(string), null!));
	}

	[Fact]
	public void SerializeToPooledUtf8Buffer_Generic_SerializesAndRentsBuffer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var bufferManager = A.Fake<IPooledBufferService>();
		var rentedBuffer = new PooledBuffer(bufferManager, new byte[64]);

		A.CallTo(() => bufferManager.RentBuffer(A<int>._, A<bool>._)).Returns(rentedBuffer);

		// Act
		using var result = serializer.SerializeToPooledUtf8Buffer("test", bufferManager);

		// Assert
		result.ShouldNotBeNull();
		A.CallTo(() => serializer.SerializeToUtf8(A<IBufferWriter<byte>>._, "test", typeof(string)))
			.MustHaveHappenedOnceExactly();
		A.CallTo(() => bufferManager.RentBuffer(A<int>._, A<bool>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void SerializeToPooledUtf8Buffer_NonGeneric_SerializesAndRentsBuffer()
	{
		// Arrange
		var serializer = A.Fake<IUtf8JsonSerializer>();
		var bufferManager = A.Fake<IPooledBufferService>();
		var rentedBuffer = new PooledBuffer(bufferManager, new byte[64]);

		A.CallTo(() => bufferManager.RentBuffer(A<int>._, A<bool>._)).Returns(rentedBuffer);

		// Act
		using var result = serializer.SerializeToPooledUtf8Buffer((object?)"test", typeof(string), bufferManager);

		// Assert
		result.ShouldNotBeNull();
		A.CallTo(() => serializer.SerializeToUtf8(A<IBufferWriter<byte>>._, "test", typeof(string)))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region MockUtf8JsonSerializer

	/// <summary>
	/// Concrete mock for testing ReadOnlySpan methods which FakeItEasy cannot proxy.
	/// </summary>
	[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Test mock")]
	[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Test mock")]
	private sealed class MockUtf8JsonSerializer(object? returnValue) : IUtf8JsonSerializer
	{
		public string Serialize(object value, Type type) => string.Empty;
		public object? Deserialize(string json, Type type) => returnValue;
		public byte[] SerializeToUtf8Bytes(object? value, Type type) => Encoding.UTF8.GetBytes(value?.ToString() ?? "");
		public void SerializeToUtf8(IBufferWriter<byte> writer, object? value, Type type) { }
		public object? DeserializeFromUtf8(ReadOnlySpan<byte> utf8Json, Type type) => returnValue;
		public object? DeserializeFromUtf8(ReadOnlyMemory<byte> utf8Json, Type type) => returnValue;
	}

	#endregion
}

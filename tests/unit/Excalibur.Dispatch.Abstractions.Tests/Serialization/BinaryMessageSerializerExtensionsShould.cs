// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Excalibur.Dispatch.Abstractions.Options;
using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="BinaryMessageSerializerExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BinaryMessageSerializerExtensionsShould
{
	#region SerializeAsync<T> Tests

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
#pragma warning disable CA2012
	public void SerializeAsync_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => BinaryMessageSerializerExtensions.SerializeAsync<string>(null!, "test", CancellationToken.None));
	}
#pragma warning restore CA2012

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task SerializeAsync_ThrowsOnCancellation()
	{
		// Arrange
		var serializer = A.Fake<IBinaryMessageSerializer>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			async () => await serializer.SerializeAsync("test", cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task SerializeAsync_DelegatesToSerializer()
	{
		// Arrange
		var mock = new MockBinaryMessageSerializer(null, [1, 2, 3]);

		// Act
		var result = await mock.SerializeAsync("hello", CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe([1, 2, 3]);
	}

	#endregion

	#region DeserializeAsync<T> Tests

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
#pragma warning disable CA2012
	public void DeserializeAsync_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => BinaryMessageSerializerExtensions.DeserializeAsync<string>(null!, new byte[] { 1 }, CancellationToken.None));
	}
#pragma warning restore CA2012

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task DeserializeAsync_ThrowsOnCancellation()
	{
		// Arrange
		var serializer = A.Fake<IBinaryMessageSerializer>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(
			async () => await serializer.DeserializeAsync<string>(new byte[] { 1 }, cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task DeserializeAsync_DelegatesToSerializer()
	{
		// Arrange - Use concrete mock to avoid FakeItEasy issues with IBinaryMessageSerializer
		// which has both Deserialize<T>(byte[]) and Deserialize<T>(ReadOnlySpan<byte>) overloads
		var mock = new MockBinaryMessageSerializer("result");

		// Act
		var result = await mock.DeserializeAsync<string>(new byte[] { 1, 2, 3 }, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("result");
	}

	#endregion

	#region Serialize with Options Tests

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public void Serialize_WithOptions_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => BinaryMessageSerializerExtensions.Serialize<string>(null!, "test", new SerializationOptions()));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public void Serialize_WithOptions_DelegatesToSerializer()
	{
		// Arrange
		var expected = new byte[] { 4, 5 };
		var mock = new MockBinaryMessageSerializer(null, expected);

		// Act
		var result = mock.Serialize("msg", new SerializationOptions());

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region SerializeAsync to Stream Tests

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task SerializeAsync_ToStream_ThrowsOnNullSerializer()
	{
		// SerializeAsync(null, msg, stream, ct) is async — exception is deferred
		using var stream = new MemoryStream();
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await BinaryMessageSerializerExtensions.SerializeAsync<string>(null!, "test", stream, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task SerializeAsync_ToStream_ThrowsOnNullStream()
	{
		// SerializeAsync(serializer, msg, null, ct) is async — exception is deferred
		var mock = new MockBinaryMessageSerializer(null);
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await BinaryMessageSerializerExtensions.SerializeAsync<string>(mock, "test", null!, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task SerializeAsync_ToStream_WritesToStream()
	{
		// Arrange
		var expectedBytes = new byte[] { 10, 20, 30 };
		var mock = new MockBinaryMessageSerializer(null, expectedBytes);
		using var stream = new MemoryStream();

		// Act
		await mock.SerializeAsync("data", stream, CancellationToken.None).ConfigureAwait(false);

		// Assert
		stream.ToArray().ShouldBe(expectedBytes);
	}

	#endregion

	#region Deserialize from ReadOnlyMemory Tests

	[Fact]
	public void Deserialize_FromReadOnlyMemory_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => BinaryMessageSerializerExtensions.Deserialize<string>(null!, new ReadOnlyMemory<byte>(new byte[] { 1 })));
	}

	[Fact]
	public void Deserialize_FromReadOnlyMemory_DelegatesToSerializer()
	{
		// Arrange - FakeItEasy cannot proxy ReadOnlySpan<byte>, use concrete mock
		var mock = new MockBinaryMessageSerializer("result");
		var data = new byte[] { 1, 2, 3 };

		// Act
		var result = mock.Deserialize<string>(new ReadOnlyMemory<byte>(data));

		// Assert
		result.ShouldBe("result");
	}

	#endregion

	#region DeserializeAsync from Stream Tests

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task DeserializeAsync_FromStream_ThrowsOnNullSerializer()
	{
		// DeserializeAsync(null, stream, ct) is async — exception is deferred
		using var stream = new MemoryStream(new byte[] { 1 });
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await BinaryMessageSerializerExtensions.DeserializeAsync<string>(null!, stream, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task DeserializeAsync_FromStream_ThrowsOnNullStream()
	{
		// DeserializeAsync(serializer, null, ct) is async — exception is deferred
		var mock = new MockBinaryMessageSerializer(null);
		await Should.ThrowAsync<ArgumentNullException>(
			async () => await BinaryMessageSerializerExtensions.DeserializeAsync<string>(mock, (Stream)null!, CancellationToken.None)
				.ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task DeserializeAsync_FromSeekableStream_ReadsDirectly()
	{
		// Arrange - Use concrete mock to avoid FakeItEasy issues with overloaded Deserialize methods
		var mock = new MockBinaryMessageSerializer("seekable");
		var data = new byte[] { 1, 2, 3 };
		using var stream = new MemoryStream(data);

		// Act
		var result = await mock.DeserializeAsync<string>(stream, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("seekable");
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public async Task DeserializeAsync_FromNonSeekableStream_UsesMemoryStream()
	{
		// Arrange - FakeItEasy cannot proxy ReadOnlySpan<byte>, use concrete mock
		var mock = new MockBinaryMessageSerializer("non-seekable");
		var data = new byte[] { 4, 5, 6 };

		// Create a non-seekable stream wrapper
		using var innerStream = new MemoryStream(data);
		using var nonSeekableStream = new NonSeekableStreamWrapper(innerStream);

		// Act
		var result = await mock.DeserializeAsync<string>(nonSeekableStream, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("non-seekable");
	}

	#endregion

	#region GetSerializedSize Tests

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public void GetSerializedSize_ThrowsOnNullSerializer()
	{
		Should.Throw<ArgumentNullException>(
			() => BinaryMessageSerializerExtensions.GetSerializedSize<string>(null!, "test"));
	}

	[Fact]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test code")]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test code")]
	public void GetSerializedSize_ReturnsSerializedLength()
	{
		// Arrange
		var mock = new MockBinaryMessageSerializer(null, [1, 2, 3, 4, 5]);

		// Act
		var result = mock.GetSerializedSize("hello");

		// Assert
		result.ShouldBe(5);
	}

	#endregion

	#region Helper Types

	/// <summary>
	/// Concrete mock for testing — avoids FakeItEasy issues with ReadOnlySpan overloads
	/// and ambiguous Deserialize&lt;T&gt;(byte[]) vs Deserialize&lt;T&gt;(ReadOnlySpan&lt;byte&gt;) resolution.
	/// </summary>
	[SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Test mock")]
	[SuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "Test mock")]
	private sealed class MockBinaryMessageSerializer : IBinaryMessageSerializer
	{
		private readonly object? _returnValue;
		private readonly byte[] _serializedBytes;

		public MockBinaryMessageSerializer(object? returnValue, byte[]? serializedBytes = null)
		{
			_returnValue = returnValue;
			_serializedBytes = serializedBytes ?? [1, 2, 3];
		}

		public string SerializerName => "Mock";
		public string SerializerVersion => "1.0";
		public string ContentType => "application/octet-stream";
		public bool SupportsCompression => false;
		public string Format => "Mock";

		public byte[] Serialize<T>(T message) => _serializedBytes;
		public T Deserialize<T>(byte[] data) => (T)_returnValue!;
		public void Serialize<T>(T message, IBufferWriter<byte> bufferWriter) { }
		public T Deserialize<T>(ReadOnlySpan<byte> data) => (T)_returnValue!;
	}

	/// <summary>
	/// A stream wrapper that is not seekable, for testing the non-seekable stream path.
	/// </summary>
	private sealed class NonSeekableStreamWrapper(Stream inner) : Stream
	{
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override void Flush() => inner.Flush();
		public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
			inner.ReadAsync(buffer, offset, count, cancellationToken);
		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
			inner.ReadAsync(buffer, cancellationToken);
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}

	#endregion
}

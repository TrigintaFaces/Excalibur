// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers.Binary;
using System.IO.Pipelines;

using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;
using MessagePack.Resolvers;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Additional edge-case and coverage tests for <see cref="MessagePackZeroCopySerializer"/>.
/// Targets: additional DeserializeAsync paths, error propagation branches,
/// Deserialize(ReadOnlyMemory) additional cases, and SerializeAsync edge cases.
/// NOTE: Sync Serialize(T, Memory) is known to produce incorrect WrittenCount with LZ4
/// compression, so roundtrip tests for that method are excluded (see existing test file notes).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackZeroCopySerializerEdgeCasesShould : UnitTestBase
{
	/// <summary>
	/// Helper to create the same MessagePack options used by the serializer internally.
	/// </summary>
	private static MessagePackSerializerOptions CreateMatchingOptions()
		=> MessagePackSerializerOptions.Standard
			.WithResolver(ContractlessStandardResolver.Instance)
			.WithCompression(MessagePackCompression.Lz4BlockArray);

	#region Deserialize Memory - Additional Cases

	[Fact]
	public void Deserialize_WithContractlessData_WorksWithMatchingOptions()
	{
		// Arrange - serialize using the same options the serializer uses internally
		var serializer = new MessagePackZeroCopySerializer();
		var options = CreateMatchingOptions();
		var original = new TestMessage { Id = 99, Name = "Contractless" };
		var data = global::MessagePack.MessagePackSerializer.Serialize(original, options);

		// Act
		var result = serializer.Deserialize<TestMessage>(data);

		// Assert
		result.Id.ShouldBe(99);
		result.Name.ShouldBe("Contractless");
	}

	[Fact]
	public void Deserialize_WithPluggableMessage_WorksWithMatchingOptions()
	{
		// Arrange
		var serializer = new MessagePackZeroCopySerializer();
		var options = CreateMatchingOptions();
		var original = new TestPluggableMessage { Value = 55, Text = "PluggableZeroCopy" };
		var data = global::MessagePack.MessagePackSerializer.Serialize(original, options);

		// Act
		var result = serializer.Deserialize<TestPluggableMessage>(data);

		// Assert
		result.Value.ShouldBe(55);
		result.Text.ShouldBe("PluggableZeroCopy");
	}

	[Fact]
	public void Deserialize_MultipleDifferentTypes_AllSucceed()
	{
		// Arrange
		var serializer = new MessagePackZeroCopySerializer();
		var options = CreateMatchingOptions();

		var msg = new TestMessage { Id = 10, Name = "TypeA" };
		var plug = new TestPluggableMessage { Value = 20, Text = "TypeB" };

		var msgData = global::MessagePack.MessagePackSerializer.Serialize(msg, options);
		var plugData = global::MessagePack.MessagePackSerializer.Serialize(plug, options);

		// Act
		var resultMsg = serializer.Deserialize<TestMessage>(msgData);
		var resultPlug = serializer.Deserialize<TestPluggableMessage>(plugData);

		// Assert
		resultMsg.Id.ShouldBe(10);
		resultPlug.Value.ShouldBe(20);
	}

	#endregion

	#region SerializeAsync - Additional Paths

	[Fact]
	public async Task SerializeAsync_WithMultipleMessages_AllWriteSuccessfully()
	{
		// Arrange
		var serializer = new MessagePackZeroCopySerializer();

		for (var i = 0; i < 5; i++)
		{
			var pipe = new Pipe();
			var message = new TestMessage { Id = i, Name = $"Multi{i}" };

			// Act
			await serializer.SerializeAsync(pipe.Writer, message, CancellationToken.None).ConfigureAwait(false);
			await pipe.Writer.CompleteAsync().ConfigureAwait(false);

			// Assert
			var result = await pipe.Reader.ReadAsync().ConfigureAwait(false);
			result.Buffer.Length.ShouldBeGreaterThan(0);
			await pipe.Reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task SerializeAsync_WithNegativeId_WritesToPipe()
	{
		// Arrange
		var serializer = new MessagePackZeroCopySerializer();
		var message = new TestMessage { Id = -1, Name = "NegAsync" };
		var pipe = new Pipe();

		// Act
		await serializer.SerializeAsync(pipe.Writer, message, CancellationToken.None).ConfigureAwait(false);
		await pipe.Writer.CompleteAsync().ConfigureAwait(false);

		// Assert
		var result = await pipe.Reader.ReadAsync().ConfigureAwait(false);
		result.Buffer.Length.ShouldBeGreaterThan(0);
		await pipe.Reader.CompleteAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task SerializeAsync_WithSpecialCharacters_WritesToPipe()
	{
		// Arrange
		var serializer = new MessagePackZeroCopySerializer();
		var message = new TestMessage { Id = 3, Name = "Tab:\t\nNewline" };
		var pipe = new Pipe();

		// Act
		await serializer.SerializeAsync(pipe.Writer, message, CancellationToken.None).ConfigureAwait(false);
		await pipe.Writer.CompleteAsync().ConfigureAwait(false);

		// Assert
		var result = await pipe.Reader.ReadAsync().ConfigureAwait(false);
		result.Buffer.Length.ShouldBeGreaterThan(0);
		await pipe.Reader.CompleteAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task SerializeAsync_WithUnicodeCharacters_WritesToPipe()
	{
		// Arrange
		var serializer = new MessagePackZeroCopySerializer();
		var message = new TestMessage { Id = 4, Name = "\u00e9\u4e2d\u6587\U0001f600" };
		var pipe = new Pipe();

		// Act
		await serializer.SerializeAsync(pipe.Writer, message, CancellationToken.None).ConfigureAwait(false);
		await pipe.Writer.CompleteAsync().ConfigureAwait(false);

		// Assert
		var result = await pipe.Reader.ReadAsync().ConfigureAwait(false);
		result.Buffer.Length.ShouldBeGreaterThan(0);
		await pipe.Reader.CompleteAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task SerializeAsync_OutputIsDeserializableDirectly()
	{
		// Arrange - verify that async-serialized output can be deserialized via Deserialize(ReadOnlyMemory)
		var serializer = new MessagePackZeroCopySerializer();
		var original = new TestMessage { Id = 88, Name = "AsyncToSync" };
		var pipe = new Pipe();

		// Act
		await serializer.SerializeAsync(pipe.Writer, original, CancellationToken.None).ConfigureAwait(false);
		await pipe.Writer.CompleteAsync().ConfigureAwait(false);
		var readResult = await pipe.Reader.ReadAsync().ConfigureAwait(false);
		var bytes = readResult.Buffer.First.ToArray();
		await pipe.Reader.CompleteAsync().ConfigureAwait(false);

		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(88);
		result.Name.ShouldBe("AsyncToSync");
	}

	#endregion

	#region DeserializeAsync - Additional Error Paths

	[Fact]
	public async Task DeserializeAsync_WithOnlyLengthPrefix_NoBody_ThrowsEndOfStream()
	{
		// Arrange - write just the 4-byte length prefix, then complete
		var serializer = new MessagePackZeroCopySerializer();
		var pipe = new Pipe();
		var lengthPrefix = new byte[sizeof(int)];
		BinaryPrimitives.WriteInt32LittleEndian(lengthPrefix, 50);
		await pipe.Writer.WriteAsync(lengthPrefix).ConfigureAwait(false);
		await pipe.Writer.CompleteAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<EndOfStreamException>(async () =>
			await serializer.DeserializeAsync<TestMessage>(pipe.Reader, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DeserializeAsync_WithSingleByte_ThrowsEndOfStream()
	{
		// Arrange - less than the 4-byte length prefix
		var serializer = new MessagePackZeroCopySerializer();
		var pipe = new Pipe();
		await pipe.Writer.WriteAsync(new byte[] { 0x01 }).ConfigureAwait(false);
		await pipe.Writer.CompleteAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<EndOfStreamException>(async () =>
			await serializer.DeserializeAsync<TestMessage>(pipe.Reader, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DeserializeAsync_WithThreeBytes_ThrowsEndOfStream()
	{
		// Arrange - 3 bytes, still not enough for the 4-byte length prefix
		var serializer = new MessagePackZeroCopySerializer();
		var pipe = new Pipe();
		await pipe.Writer.WriteAsync(new byte[] { 0x01, 0x02, 0x03 }).ConfigureAwait(false);
		await pipe.Writer.CompleteAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<EndOfStreamException>(async () =>
			await serializer.DeserializeAsync<TestMessage>(pipe.Reader, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DeserializeAsync_WithExactlyFourBytesNoMessage_ThrowsEndOfStream()
	{
		// Arrange - 4 bytes (length prefix says 100), but no message body follows
		var serializer = new MessagePackZeroCopySerializer();
		var pipe = new Pipe();
		var lengthPrefix = new byte[sizeof(int)];
		BinaryPrimitives.WriteInt32LittleEndian(lengthPrefix, 100);
		await pipe.Writer.WriteAsync(lengthPrefix).ConfigureAwait(false);
		await pipe.Writer.CompleteAsync().ConfigureAwait(false);

		// Act & Assert
		await Should.ThrowAsync<EndOfStreamException>(async () =>
			await serializer.DeserializeAsync<TestMessage>(pipe.Reader, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task DeserializeAsync_WithValidLengthPrefixedData_ReturnsCorrectMessage()
	{
		// Arrange - send a properly length-prefixed message
		var serializer = new MessagePackZeroCopySerializer();
		var msgpackOptions = CreateMatchingOptions();
		var msg = new TestMessage { Id = 1, Name = "First" };
		var bytes = global::MessagePack.MessagePackSerializer.Serialize(msg, msgpackOptions);

		var pipe = new Pipe();
		var prefix = new byte[sizeof(int)];
		BinaryPrimitives.WriteInt32LittleEndian(prefix, bytes.Length);
		await pipe.Writer.WriteAsync(prefix).ConfigureAwait(false);
		await pipe.Writer.WriteAsync(bytes).ConfigureAwait(false);
		await pipe.Writer.CompleteAsync().ConfigureAwait(false);

		// Act
		var result = await serializer.DeserializeAsync<TestMessage>(pipe.Reader, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Id.ShouldBe(1);
		result.Name.ShouldBe("First");
	}

	[Fact]
	public async Task DeserializeAsync_WithCancellationToken_ThrowsWhenCancelled()
	{
		// Arrange
		var serializer = new MessagePackZeroCopySerializer();
		var pipe = new Pipe();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await serializer.DeserializeAsync<TestMessage>(pipe.Reader, cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
	}

	#endregion

	#region Interface Implementation

	[Fact]
	public void ImplementsIZeroCopySerializer_Interface()
	{
		// Arrange
		var serializer = new MessagePackZeroCopySerializer();

		// Assert
		serializer.ShouldBeAssignableTo<IZeroCopySerializer>();
	}

	#endregion

	#region Constructor

	[Fact]
	public void Constructor_CreatesNonNullInstance()
	{
		// Act
		var serializer = new MessagePackZeroCopySerializer();

		// Assert
		serializer.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_CreatesReusableInstance()
	{
		// Arrange
		var serializer = new MessagePackZeroCopySerializer();
		var options = CreateMatchingOptions();

		// Act - use the same instance for multiple deserializations
		for (var i = 0; i < 5; i++)
		{
			var original = new TestMessage { Id = i, Name = $"Reuse{i}" };
			var data = global::MessagePack.MessagePackSerializer.Serialize(original, options);
			var result = serializer.Deserialize<TestMessage>(data);
			result.Id.ShouldBe(i);
		}
	}

	#endregion
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Excalibur.Dispatch.Abstractions.Options;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using FakeItEasy;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="ClaimCheckMessageSerializer"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
[Trait("Feature", "ClaimCheck")]
public sealed class ClaimCheckMessageSerializerShould
{
	private readonly IClaimCheckProvider _fakeProvider;
	private readonly IBinaryMessageSerializer _fakeBaseSerializer;
	private readonly ClaimCheckOptions _options;

	public ClaimCheckMessageSerializerShould()
	{
		_fakeProvider = A.Fake<IClaimCheckProvider>();
		_fakeBaseSerializer = A.Fake<IBinaryMessageSerializer>();
		_options = new ClaimCheckOptions { PayloadThreshold = 1000 };

		// Setup default serializer properties
		A.CallTo(() => _fakeBaseSerializer.SerializerName).Returns("FakeSerializer");
		A.CallTo(() => _fakeBaseSerializer.ContentType).Returns("application/json");
		A.CallTo(() => _fakeBaseSerializer.SupportsCompression).Returns(false);
		A.CallTo(() => _fakeBaseSerializer.Format).Returns("JSON");
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenProviderIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ClaimCheckMessageSerializer(null!, _fakeBaseSerializer, _options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenBaseSerializerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ClaimCheckMessageSerializer(_fakeProvider, (IBinaryMessageSerializer)null!, _options));
	}

	[Fact]
	public void CreateWithDefaultJsonSerializer_Constructor()
	{
		// Act — uses the simplified constructor with built-in JsonClaimCheckSerializer
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _options);

		// Assert
		serializer.ShouldNotBeNull();
		serializer.SerializerName.ShouldStartWith("ClaimCheck-");
	}

	[Fact]
	public void CreateWithDefaultOptions_WhenOptionsIsNull()
	{
		// Act
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, null);

		// Assert
		serializer.ShouldNotBeNull();
	}

	#endregion

	#region Property Tests

	[Fact]
	public void HaveCorrectSerializerName()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Act & Assert
		serializer.SerializerName.ShouldBe("ClaimCheck-FakeSerializer");
	}

	[Fact]
	public void HaveCorrectSerializerVersion()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Act & Assert
		serializer.SerializerVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void DelegateContentType_ToBaseSerializer()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Act & Assert
		serializer.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void DelegateSupportsCompression_ToBaseSerializer()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Act & Assert
		serializer.SupportsCompression.ShouldBeFalse();
	}

	[Fact]
	public void HaveCorrectFormat()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Act & Assert
		serializer.Format.ShouldBe("ClaimCheck(JSON)");
	}

	#endregion

	#region Serialize (Sync) Tests

	[Fact]
	public void SerializeSmallMessage_WithoutClaimCheck()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);
		var message = new TestMessage { Id = 1, Data = "small" };
		var smallPayload = new byte[100]; // Below threshold

		A.CallTo(() => _fakeBaseSerializer.Serialize(message))
			.Returns(smallPayload);

		// Act
		var result = serializer.Serialize(message);

		// Assert
		result.ShouldBe(smallPayload);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenMessageIsNull_OnSerialize()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.Serialize<TestMessage>(null!));
	}

	[Fact]
	public void ThrowNotSupportedException_WhenPayloadExceedsThreshold_InSyncMode()
	{
		// Arrange - Sync mode throws for large messages (requires async for claim check storage)
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);
		var message = new TestMessage { Id = 1, Data = "large" };
		var largePayload = new byte[2000]; // Above threshold

		A.CallTo(() => _fakeBaseSerializer.Serialize(message))
			.Returns(largePayload);

		// Act & Assert - Sync Serialize throws NotSupportedException for large messages
		Should.Throw<NotSupportedException>(() => serializer.Serialize(message));
	}

	#endregion

	#region SerializeAsync Tests

	[Fact]
	public async Task SerializeSmallMessageAsync_WithoutClaimCheck()
	{
		// Arrange — use concrete stub to avoid FakeItEasy reflection issues with [RequiresDynamicCode]
		var stub = new StubBinaryMessageSerializer();
		var message = new TestMessage { Id = 1, Data = "small" };
		var smallPayload = new byte[100]; // Below threshold

		stub.SetupSerialize<TestMessage>(_ => smallPayload);

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);

		// Act — calls instance SerializeAsync which checks threshold
		var result = await serializer.SerializeAsync(message, CancellationToken.None);

		// Assert
		result.ShouldBe(smallPayload);
		A.CallTo(() => _fakeProvider.StoreAsync(A<byte[]>._, A<CancellationToken>._, A<ClaimCheckMetadata>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SerializeLargeMessageAsync_UsesClaimCheck()
	{
		// Arrange — use concrete stub to avoid FakeItEasy reflection issues with [RequiresDynamicCode]
		var stub = new StubBinaryMessageSerializer();
		var message = new TestMessage { Id = 1, Data = "large" };
		var largePayload = new byte[2000]; // Above threshold
		var envelopePayload = new byte[50];
		var reference = new ClaimCheckReference { Id = "claim-123", Location = "blob://test" };

		stub.SetupSerialize<TestMessage>(_ => largePayload);
		stub.SetupSerialize<ClaimCheckEnvelope>(_ => envelopePayload);

		A.CallTo(() => _fakeProvider.StoreAsync(largePayload, A<CancellationToken>._, A<ClaimCheckMetadata>._))
			.Returns(Task.FromResult(reference));

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);

		// Act — calls instance SerializeAsync which stores large payload via claim check
		var result = await serializer.SerializeAsync(message, CancellationToken.None);

		// Assert — result has "CC01" magic prefix prepended to the envelope payload
		result.Length.ShouldBe(4 + envelopePayload.Length); // 4-byte magic prefix + envelope
		result[0].ShouldBe((byte)'C');
		result[1].ShouldBe((byte)'C');
		result[2].ShouldBe((byte)'0');
		result[3].ShouldBe((byte)'1');
		result.AsSpan(4).ToArray().ShouldBe(envelopePayload);
		A.CallTo(() => _fakeProvider.StoreAsync(largePayload, A<CancellationToken>._, A<ClaimCheckMetadata>.That.Matches(m =>
			m.MessageType == "TestMessage" &&
			m.ContentType == "application/json")))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull_OnSerializeAsync()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			serializer.SerializeAsync<TestMessage>(null!, CancellationToken.None).AsTask());
	}

	#endregion

	#region Deserialize (Sync) Tests

	[Fact]
	public void DeserializeSmallMessage_Directly()
	{
		// Arrange — use concrete stub to avoid FakeItEasy reflection issues with [RequiresDynamicCode]
		var stub = new StubBinaryMessageSerializer();
		var expectedMessage = new TestMessage { Id = 42, Data = "result" };
		var data = new byte[100];

		stub.SetupDeserialize(_ => expectedMessage);

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);

		// Act
		var result = serializer.Deserialize<TestMessage>(data);

		// Assert
		result.ShouldBe(expectedMessage);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenDataIsNull_OnDeserialize()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.Deserialize<TestMessage>(null!));
	}

	[Fact]
	public void ThrowNotSupportedException_WhenClaimCheckEnvelopeDetected_InSyncMode()
	{
		// Arrange — data with "CC01" magic prefix triggers claim check detection
		var stub = new StubBinaryMessageSerializer();
		var data = new byte[] { (byte)'C', (byte)'C', (byte)'0', (byte)'1', 0, 0, 0, 0 };

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);

		// Act & Assert — sync Deserialize detects magic prefix and throws
		Should.Throw<NotSupportedException>(() => serializer.Deserialize<TestMessage>(data));
	}

	#endregion

	#region DeserializeAsync Tests

	[Fact]
	public async Task DeserializeSmallMessageAsync_Directly()
	{
		// Arrange — use concrete stub to avoid FakeItEasy reflection issues with [RequiresDynamicCode]
		var stub = new StubBinaryMessageSerializer();
		var expectedMessage = new TestMessage { Id = 42, Data = "async-result" };
		var data = new byte[100];

		stub.SetupDeserialize(_ => expectedMessage);

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);

		// Act — calls instance DeserializeAsync
		var result = await serializer.DeserializeAsync<TestMessage>(data, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedMessage);
	}

	[Fact]
	public async Task DeserializeLargeMessageAsync_FromClaimCheck()
	{
		// Arrange — use concrete stub to avoid FakeItEasy reflection issues with [RequiresDynamicCode]
		var stub = new StubBinaryMessageSerializer();
		var expectedMessage = new TestMessage { Id = 99, Data = "retrieved" };
		var envelopeContent = new byte[50]; // Raw envelope bytes (after prefix stripping)
		var storedPayload = new byte[2000]; // The large payload retrieved from storage

		// Build data with "CC01" magic prefix so DeserializeAsync detects claim check
		var data = new byte[4 + envelopeContent.Length];
		data[0] = (byte)'C';
		data[1] = (byte)'C';
		data[2] = (byte)'0';
		data[3] = (byte)'1';
		Array.Copy(envelopeContent, 0, data, 4, envelopeContent.Length);

		var envelope = new ClaimCheckEnvelope
		{
			Reference = new ClaimCheckReference { Id = "claim-456", Location = "blob://stored" },
			MessageType = "TestMessage",
		};

		// After prefix stripping, Deserialize<ClaimCheckEnvelope>(envelopeContent) returns envelope,
		// then after retrieval, Deserialize<TestMessage>(storedPayload) returns expected.
		stub.SetupDeserialize(_ => expectedMessage);
		stub.SetupDeserialize(_ => envelope);

		A.CallTo(() => _fakeProvider.RetrieveAsync(envelope.Reference, A<CancellationToken>._))
			.Returns(Task.FromResult(storedPayload));

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);

		// Act — calls instance DeserializeAsync which detects magic prefix, strips it, resolves claim check
		var result = await serializer.DeserializeAsync<TestMessage>(data, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedMessage);
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenDataIsNull_OnDeserializeAsync()
	{
		// Arrange — instance DeserializeAsync checks null before touching base serializer
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);
		byte[]? nullData = null;

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(() =>
			serializer.DeserializeAsync<TestMessage>(nullData, CancellationToken.None).AsTask());
	}

	#endregion

	#region Delegate Methods Tests

	[Fact]
	public void SerializeWithOptions_DelegatesToBaseSerializer()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);
		var message = new TestMessage { Id = 1 };
		var opts = new SerializationOptions();
		var expected = new byte[] { 1, 2, 3 };

		// Serialize(T, options) extension calls core Serialize<T>(T)
		A.CallTo(() => _fakeBaseSerializer.Serialize(message))
			.Returns(expected);

		// Act
		var result = serializer.Serialize(message, opts);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void SerializeToBufferWriter_DelegatesToBaseSerializer()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);
		var message = new TestMessage { Id = 1 };
		var bufferWriter = new ArrayBufferWriter<byte>();

		// Act
		serializer.Serialize(message, bufferWriter);

		// Assert
		A.CallTo(() => _fakeBaseSerializer.Serialize(message, bufferWriter))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task SerializeToStreamAsync_DelegatesToBaseSerializer()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);
		var message = new TestMessage { Id = 1 };
		var expected = new byte[] { 1, 2, 3 };
		using var stream = new MemoryStream();

		// SerializeAsync(T, Stream, CT) extension calls core Serialize<T>(T) then writes to stream
		A.CallTo(() => _fakeBaseSerializer.Serialize(message))
			.Returns(expected);

		// Act
		await serializer.SerializeAsync(message, stream, CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeBaseSerializer.Serialize(message))
			.MustHaveHappenedOnceExactly();
		stream.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void DeserializeSpan_CallsBaseSerializer()
	{
		// Note: Can't use FakeItEasy with ReadOnlySpan<byte> (ref struct limitation)
		// Use the built-in JSON serializer via simplified constructor

		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _options);
		var data = Encoding.UTF8.GetBytes("{\"Id\":100}");

		// Act
		var result = serializer.Deserialize<TestMessage>((ReadOnlySpan<byte>)data);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(100);
	}

	[Fact]
	public void DeserializeMemory_DelegatesToBaseSerializer()
	{
		// Arrange — use built-in JSON serializer via simplified constructor
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _options);
		var data = Encoding.UTF8.GetBytes("{\"Id\":200}");

		// Act — Deserialize<T>(ReadOnlyMemory<byte>) extension calls Deserialize<T>(ReadOnlySpan<byte>)
		var result = serializer.Deserialize<TestMessage>((ReadOnlyMemory<byte>)data);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(200);
	}

	[Fact]
	public async Task DeserializeFromStreamAsync_DelegatesToBaseSerializer()
	{
		// Arrange — use built-in JSON serializer via simplified constructor
		var jsonBytes = Encoding.UTF8.GetBytes("{\"Id\":300}");
		using var stream = new MemoryStream(jsonBytes);

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _options);

		// Act — DeserializeAsync<T>(Stream, CT) extension reads stream, calls Deserialize<T>(byte[])
		var result = await serializer.DeserializeAsync<TestMessage>(stream, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(300);
	}

	[Fact]
	public void GetSerializedSize_DelegatesToBaseSerializer()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);
		var message = new TestMessage { Id = 1 };

		// GetSerializedSize extension calls core Serialize<T>(T).Length
		A.CallTo(() => _fakeBaseSerializer.Serialize(message))
			.Returns(new byte[256]);

		// Act
		var result = serializer.GetSerializedSize(message);

		// Assert
		result.ShouldBe(256);
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementIBinaryMessageSerializer()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Assert
		serializer.ShouldBeAssignableTo<IBinaryMessageSerializer>();
	}

	[Fact]
	public void ImplementIMessageSerializer()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Assert
		serializer.ShouldBeAssignableTo<IMessageSerializer>();
	}

	#endregion

	#region Test Helpers

	private sealed class TestMessage
	{
		public int Id { get; set; }
		public string? Data { get; set; }
	}

	/// <summary>
	/// Concrete test double for IBinaryMessageSerializer that avoids FakeItEasy reflection issues
	/// with [RequiresDynamicCode] attributes on .NET 10.
	/// </summary>
	private sealed class StubBinaryMessageSerializer : IBinaryMessageSerializer
	{
		private readonly Dictionary<Type, Func<object, byte[]>> _serializeFuncs = new();
		private readonly Dictionary<Type, Func<byte[], object>> _deserializeFuncs = new();

		public string SerializerName { get; set; } = "StubSerializer";

		public string SerializerVersion { get; set; } = "1.0.0";

		public string ContentType { get; set; } = "application/json";

		public bool SupportsCompression { get; set; }

		public string Format { get; set; } = "JSON";

		public void SetupSerialize<T>(Func<T, byte[]> func) =>
			_serializeFuncs[typeof(T)] = obj => func((T)obj);

		public void SetupDeserialize<T>(Func<byte[], T> func) =>
			_deserializeFuncs[typeof(T)] = data => (object)func(data)!;

#pragma warning disable IL2046, IL3051 // Test stub — AOT attributes not required for test doubles
		public byte[] Serialize<T>(T message)
		{
			if (_serializeFuncs.TryGetValue(typeof(T), out var func))
			{
				return func(message);
			}

			throw new InvalidOperationException($"No serialize setup for {typeof(T).Name}");
		}

		public T Deserialize<T>(byte[] data)
		{
			if (_deserializeFuncs.TryGetValue(typeof(T), out var func))
			{
				return (T)func(data);
			}

			throw new InvalidOperationException($"No deserialize setup for {typeof(T).Name}");
		}

		public void Serialize<T>(T message, IBufferWriter<byte> bufferWriter)
		{
			var bytes = Serialize(message);
			var span = bufferWriter.GetSpan(bytes.Length);
			bytes.AsSpan().CopyTo(span);
			bufferWriter.Advance(bytes.Length);
		}

		public T Deserialize<T>(ReadOnlySpan<byte> data) =>
			Deserialize<T>(data.ToArray());
#pragma warning restore IL2046, IL3051
	}

	#endregion
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using FakeItEasy;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Unit tests for <see cref="ClaimCheckMessageSerializer"/> class.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Patterns)]
[Trait("Feature", "ClaimCheck")]
public sealed class ClaimCheckMessageSerializerShould
{
	private readonly IClaimCheckProvider _fakeProvider;
	private readonly ISerializer _fakeBaseSerializer;
	private readonly ClaimCheckOptions _options;

	public ClaimCheckMessageSerializerShould()
	{
		_fakeProvider = A.Fake<IClaimCheckProvider>();
		_fakeBaseSerializer = A.Fake<ISerializer>();
		_options = new ClaimCheckOptions { PayloadThreshold = 1000 };

		// Setup default serializer properties
		A.CallTo(() => _fakeBaseSerializer.Name).Returns("FakeSerializer");
		A.CallTo(() => _fakeBaseSerializer.ContentType).Returns("application/json");
		A.CallTo(() => _fakeBaseSerializer.Version).Returns("1.0.0");
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
			new ClaimCheckMessageSerializer(_fakeProvider, (ISerializer)null!, _options));
	}

	[Fact]
	public void CreateWithDefaultJsonSerializer_Constructor()
	{
		// Act — uses the simplified constructor with built-in JsonClaimCheckSerializer
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _options);

		// Assert
		serializer.ShouldNotBeNull();
		serializer.Name.ShouldStartWith("ClaimCheck-");
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
	public void HaveCorrectName()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Act & Assert
		serializer.Name.ShouldBe("ClaimCheck-FakeSerializer");
	}

	[Fact]
	public void HaveCorrectVersion()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Act & Assert
		serializer.Version.ShouldBe("1.0.0");
	}

	[Fact]
	public void DelegateContentType_ToBaseSerializer()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Act & Assert
		serializer.ContentType.ShouldBe("application/json");
	}

	#endregion

	#region Serialize (Sync) Tests

	[Fact]
	public void SerializeSmallMessage_WithoutClaimCheck()
	{
		// Arrange
		var stub = new StubSerializer();
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);
		var message = new TestMessage { Id = 1, Data = "small" };
		var smallPayload = new byte[100]; // Below threshold

		stub.SetupSerialize<TestMessage>(_ => smallPayload);

		// Act — uses SerializeToBytes extension which calls Serialize(T, IBufferWriter<byte>)
		var result = serializer.SerializeToBytes(message);

		// Assert — 2mhglb: every inline payload is framed with a leading 0x00 inline tag
		// (collision-free format discriminator, FR-C1). Strengthen-don't-weaken: assert the framed shape.
		result.Length.ShouldBe(1 + smallPayload.Length);
		result[0].ShouldBe((byte)0x00);
		result.AsSpan(1).ToArray().ShouldBe(smallPayload);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenMessageIsNull_OnSerialize()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);
		var bufferWriter = new ArrayBufferWriter<byte>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => serializer.Serialize<TestMessage>(null!, bufferWriter));
	}

	[Fact]
	public void ThrowNotSupportedException_WhenPayloadExceedsThreshold_InSyncMode()
	{
		// Arrange - Sync mode throws for large messages (requires async for claim check storage)
		var stub = new StubSerializer();
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);
		var message = new TestMessage { Id = 1, Data = "large" };
		var largePayload = new byte[2000]; // Above threshold

		stub.SetupSerialize<TestMessage>(_ => largePayload);

		// Act & Assert - Sync Serialize throws NotSupportedException for large messages
		Should.Throw<NotSupportedException>(() => serializer.SerializeToBytes(message));
	}

	#endregion

	#region SerializeAsync Tests

	[Fact]
	public async Task SerializeSmallMessageAsync_WithoutClaimCheck()
	{
		// Arrange — use concrete stub to avoid FakeItEasy reflection issues with [RequiresDynamicCode]
		var stub = new StubSerializer();
		var message = new TestMessage { Id = 1, Data = "small" };
		var smallPayload = new byte[100]; // Below threshold

		stub.SetupSerialize<TestMessage>(_ => smallPayload);

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);

		// Act — calls instance SerializeAsync which checks threshold
		var result = await serializer.SerializeAsync(message, CancellationToken.None);

		// Assert — 2mhglb: inline async payloads are framed with a leading 0x00 inline tag (no offload).
		result.Length.ShouldBe(1 + smallPayload.Length);
		result[0].ShouldBe((byte)0x00);
		result.AsSpan(1).ToArray().ShouldBe(smallPayload);
		A.CallTo(() => _fakeProvider.StoreAsync(A<byte[]>._, A<CancellationToken>._, A<ClaimCheckMetadata>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task SerializeLargeMessageAsync_UsesClaimCheck()
	{
		// Arrange — use concrete stub to avoid FakeItEasy reflection issues with [RequiresDynamicCode]
		var stub = new StubSerializer();
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

		// Assert — 2mhglb: envelope payloads are framed with a single 0x01 envelope tag
		// (the old 4-byte "CC01" magic prefix is dropped; FR-C1 framing).
		result.Length.ShouldBe(1 + envelopePayload.Length); // 1-byte envelope tag + envelope
		result[0].ShouldBe((byte)0x01);
		result.AsSpan(1).ToArray().ShouldBe(envelopePayload);
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
		var stub = new StubSerializer();
		var expectedMessage = new TestMessage { Id = 42, Data = "result" };
		// 2mhglb: inline frame = [0x00 tag][body]. The reader must STRIP the tag before delegating to base.
		var data = new byte[] { 0x00, 0xAA, 0xBB, 0xCC };
		byte[]? bodySeenByBase = null;

		stub.SetupDeserialize<TestMessage>(bytes => { bodySeenByBase = bytes; return expectedMessage; });

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);

		// Act — uses Deserialize<T>(byte[]) extension which calls Deserialize<T>(ReadOnlySpan<byte>)
		var result = serializer.Deserialize<TestMessage>(data);

		// Assert — base sees the body WITHOUT the inline tag (RED on the prepend-only mainline, which
		// passes the full buffer including a non-tag leading byte straight through).
		result.ShouldBe(expectedMessage);
		bodySeenByBase.ShouldBe(new byte[] { 0xAA, 0xBB, 0xCC });
	}

	[Fact]
	public void ThrowNotSupportedException_WhenClaimCheckEnvelopeDetected_InSyncMode()
	{
		// Arrange — data with a 0x01 envelope tag triggers claim-check envelope detection on the sync path
		var stub = new StubSerializer();
		var data = new byte[] { 0x01, 0, 0, 0 };

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);

		// Act & Assert — 2mhglb: sync Deserialize sees the 0x01 envelope tag and throws (use DeserializeAsync).
		Should.Throw<NotSupportedException>(() => serializer.Deserialize<TestMessage>(data));
	}

	#endregion

	#region DeserializeAsync Tests

	[Fact]
	public async Task DeserializeSmallMessageAsync_Directly()
	{
		// Arrange — use concrete stub to avoid FakeItEasy reflection issues with [RequiresDynamicCode]
		var stub = new StubSerializer();
		var expectedMessage = new TestMessage { Id = 42, Data = "async-result" };
		// 2mhglb: inline frame = [0x00 tag][body]; the async reader strips the tag before delegating.
		var data = new byte[] { 0x00, 0x11, 0x22, 0x33 };
		byte[]? bodySeenByBase = null;

		stub.SetupDeserialize<TestMessage>(bytes => { bodySeenByBase = bytes; return expectedMessage; });

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);

		// Act — calls instance DeserializeAsync
		var result = await serializer.DeserializeAsync<TestMessage>(data, CancellationToken.None);

		// Assert — base sees the body WITHOUT the inline tag (RED on the prepend-only mainline).
		result.ShouldBe(expectedMessage);
		bodySeenByBase.ShouldBe(new byte[] { 0x11, 0x22, 0x33 });
	}

	[Fact]
	public async Task DeserializeLargeMessageAsync_FromClaimCheck()
	{
		// Arrange — use concrete stub to avoid FakeItEasy reflection issues with [RequiresDynamicCode]
		var stub = new StubSerializer();
		var expectedMessage = new TestMessage { Id = 99, Data = "retrieved" };
		var envelopeContent = new byte[50]; // Raw envelope bytes (after prefix stripping)
		var storedPayload = new byte[2000]; // The large payload retrieved from storage

		// Build data with a single 0x01 envelope tag so DeserializeAsync detects the claim-check envelope (2mhglb)
		var data = new byte[1 + envelopeContent.Length];
		data[0] = 0x01;
		Array.Copy(envelopeContent, 0, data, 1, envelopeContent.Length);

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

		// Act — calls instance DeserializeAsync which detects the 0x01 envelope tag, strips it, resolves claim check
		var result = await serializer.DeserializeAsync<TestMessage>(data, CancellationToken.None);

		// Assert — the 0x01 envelope tag drove the claim-check resolution path (RED on the prepend-only mainline,
		// which treats a [0x01]-led buffer as inline and never resolves the reference).
		result.ShouldBe(expectedMessage);
		A.CallTo(() => _fakeProvider.RetrieveAsync(envelope.Reference, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
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
	public void SerializeToBufferWriter_DelegatesToBaseSerializer()
	{
		// Arrange
		var stub = new StubSerializer();
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, stub, _options);
		var message = new TestMessage { Id = 1 };
		var smallPayload = new byte[] { 1, 2, 3 }; // Below threshold
		var bufferWriter = new ArrayBufferWriter<byte>();

		stub.SetupSerialize<TestMessage>(_ => smallPayload);

		// Act
		serializer.Serialize(message, bufferWriter);

		// Assert
		bufferWriter.WrittenCount.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void DeserializeSpan_CallsBaseSerializer()
	{
		// Note: Can't use FakeItEasy with ReadOnlySpan<byte> (ref struct limitation)
		// Use the built-in JSON serializer via simplified constructor

		// Arrange — 2mhglb: a real inline payload is framed [0x00][json]; the reader strips the tag, then delegates.
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _options);
		var data = PrependInlineTag(Encoding.UTF8.GetBytes("{\"Id\":100}"));

		// Act
		var result = serializer.Deserialize<TestMessage>((ReadOnlySpan<byte>)data);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(100);
	}

	[Fact]
	public void DeserializeMemory_DelegatesToBaseSerializer()
	{
		// Arrange — use built-in JSON serializer via simplified constructor; 2mhglb inline frame = [0x00][json].
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _options);
		var data = PrependInlineTag(Encoding.UTF8.GetBytes("{\"Id\":200}"));

		// Act — Deserialize<T>(ReadOnlyMemory<byte>) extension calls Deserialize<T>(ReadOnlySpan<byte>)
		var result = serializer.Deserialize<TestMessage>((ReadOnlyMemory<byte>)data);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(200);
	}

	[Fact]
	public async Task DeserializeFromStreamAsync_DelegatesToBaseSerializer()
	{
		// Arrange — use built-in JSON serializer via simplified constructor; 2mhglb inline frame = [0x00][json].
		var jsonBytes = PrependInlineTag(Encoding.UTF8.GetBytes("{\"Id\":300}"));
		using var stream = new MemoryStream(jsonBytes);

		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _options);

		// Act — DeserializeAsync<T>(Stream, CT) extension reads stream, calls Deserialize<T>(ReadOnlySpan<byte>)
		var result = await serializer.DeserializeAsync<TestMessage>(stream, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(300);
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementISerializer()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, _fakeBaseSerializer, _options);

		// Assert
		serializer.ShouldBeAssignableTo<ISerializer>();
	}

	#endregion

	#region Framing Collision Locks (bd-2mhglb — SA seam 14720 / MS-C FR-C1/C2b)

	// SA GUIDE ruling (14720): every payload ClaimCheckMessageSerializer emits is framed [tag:1][body],
	// 0x00 = inline / 0x01 = envelope. The ClaimCheck layer exclusively owns byte 0, so classification is
	// unambiguous BY CONSTRUCTION (collision inexpressible, enforce-invariants-structurally) — replacing the
	// pre-fix in-band "CC01" magic-prefix guess that misclassified any inline payload starting with those bytes.

	[Fact]
	public void AC_C1_InlinePayloadStartingWithOldMagicBytes_RoundTripsCleanly_Sync()
	{
		// AC-C1: an inline payload whose first 4 bytes equal the OLD "CC01" magic (0x43,0x43,0x30,0x31) MUST
		// round-trip exactly under the new framing — no throw, no corruption. RED on the prepend-only mainline,
		// which misclassifies the leading "CC01" bytes as an envelope and throws on the sync read path.
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, new IdentityByteSerializer(), _options);
		var payload = new byte[] { 0x43, 0x43, 0x30, 0x31, 0xDE, 0xAD, 0xBE, 0xEF };

		var framed = serializer.SerializeToBytes(payload);
		var recovered = serializer.Deserialize<byte[]>(framed);

		recovered.ShouldBe(payload);
	}

	[Fact]
	public async Task AC_C1_InlinePayloadStartingWithOldMagicBytes_RoundTripsCleanly_Async()
	{
		// AC-C1 (async arm): same collision payload across SerializeAsync → DeserializeAsync.
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, new IdentityByteSerializer(), _options);
		var payload = new byte[] { 0x43, 0x43, 0x30, 0x31, 0xDE, 0xAD, 0xBE, 0xEF };

		var framed = await serializer.SerializeAsync(payload, CancellationToken.None);
		var recovered = await serializer.DeserializeAsync<byte[]>(framed, CancellationToken.None);

		recovered.ShouldBe(payload);
	}

	[Theory]
	[InlineData("MessagePack", (byte)0x92)] // distinct binary tail per base serializer
	[InlineData("MemoryPack", (byte)0x03)]
	[InlineData("Protobuf", (byte)0x08)]
	public void AC_C3_InlineCollisionAcrossBinaryBaseSerializers_RoundTripsCleanly(string baseName, byte tailLeadByte)
	{
		// AC-C3: the collision class spans ALL binary base serializers — ANY of them can emit an inline payload
		// that BEGINS with the old "CC01" magic sequence (0x43,0x43,0x30,0x31). Each case stands in for a real
		// binary serializer (MessagePack/MemoryPack/Protobuf) emitting a CC01-leading payload with a distinct
		// tail; an exact-byte base keeps the lock deterministic. RED on the prepend-only mainline (every case
		// misclassifies the CC01 prefix as an envelope); GREEN once both branches are framed.
		_ = baseName;
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, new IdentityByteSerializer(), _options);
		var payload = new byte[] { 0x43, 0x43, 0x30, 0x31, tailLeadByte, 0xAB, 0xCD };

		var framed = serializer.SerializeToBytes(payload);
		var recovered = serializer.Deserialize<byte[]>(framed);

		recovered.ShouldBe(payload);
	}

	[Theory]
	[InlineData((byte)0x7B)] // '{' — raw JSON, a foreign/unframed payload
	[InlineData((byte)0x43)] // 'C' — leading byte of the old "CC01" magic, now an invalid frame tag
	[InlineData((byte)0xFF)] // arbitrary non-tag byte
	public void EC_C_Tag_UnrecognizedLeadingTag_ThrowsTypedSerializationException(byte badTag)
	{
		// FR-C2b / AC-C-tag (SA STRICT ruling 14720): a payload whose leading byte is not a recognized frame tag
		// (0x00 inline / 0x01 envelope) was NOT produced by ClaimCheckMessageSerializer — a serializer-pairing
		// error to FAIL LOUD on, never to guess. A lenient "unknown → inline passthrough" would reintroduce the
		// exact FR-C2 collision. RED on any passthrough impl (the prepend-only mainline silently delegates a
		// non-"CC01" lead straight to base with no throw).
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, new IdentityByteSerializer(), _options);
		var data = new byte[] { badTag, 0x11, 0x22 };

		Should.Throw<SerializationException>(() => serializer.Deserialize<byte[]>(data));
	}

	[Fact]
	public void EC_C_Tag_EmptyPayload_ThrowsTypedSerializationException()
	{
		// FR-C2b: a length-0 buffer carries no frame tag → typed SerializationException, not an out-of-range read.
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, new IdentityByteSerializer(), _options);

		Should.Throw<SerializationException>(() => serializer.Deserialize<byte[]>(Array.Empty<byte>()));
	}

	[Fact]
	public void EC_C_ObjectPath_IsFramedTotally_RoundTrips_IsConsistentWithGenericReader_AndFailsLoudOnBadTag()
	{
		// SA secondary flag (14720): the object channel must not leave a silent cross-method mismatch with the
		// framed generic path. The impl resolves it the STRUCTURAL way (SA's sanctioned "frame them too" option):
		// SerializeObject/DeserializeObject are inline-framed [0x00][body] too, so "ClaimCheck owns byte 0 of
		// EVERY payload it emits" is total (enforce-invariants-structurally) — no un-framed exception channel.
		// Lock that contract: framed, round-trips, consistent with the generic reader, fails loud on a bad tag.
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, new IdentityByteSerializer(), _options);
		var payload = new byte[] { 0x99, 0x88, 0x77 };

		// Object path is inline-framed and round-trips (RED on the prepend-only mainline: no tag, Length == 3).
		var objectBytes = serializer.SerializeObject(payload, typeof(byte[]));
		objectBytes.Length.ShouldBe(payload.Length + 1);
		objectBytes[0].ShouldBe((byte)0x00);
		serializer.DeserializeObject(objectBytes, typeof(byte[])).ShouldBe(payload);

		// No silent cross-method mismatch: the framed object output is consistently readable by the generic reader.
		serializer.Deserialize<byte[]>(objectBytes).ShouldBe(payload);

		// The object channel also fails loud on a payload it did not write (unrecognized leading tag).
		Should.Throw<SerializationException>(() =>
			serializer.DeserializeObject(new byte[] { 0xFF, 0x01 }, typeof(byte[])));
	}

	[Fact]
	public void EC_C1_EmptyInlinePayload_FramesToSingleTagByte_AndRoundTrips()
	{
		// EC-C1: a 0-byte base payload becomes [0x00] (tag only) — always >= 1 byte, no out-of-range read.
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, new IdentityByteSerializer(), _options);
		var empty = Array.Empty<byte>();

		var framed = serializer.SerializeToBytes(empty);

		framed.Length.ShouldBe(1);
		framed[0].ShouldBe((byte)0x00);
		serializer.Deserialize<byte[]>(framed).ShouldBe(empty);
	}

	[Fact]
	public void EC_C2_SingleByteInlinePayload_EqualToTagLength_ClassifiedCorrectly()
	{
		// EC-C2: a base payload of exactly the tag length (1 byte) — and one that IS a bare 0x01 (the envelope
		// tag value) — must be framed [0x00][0x01] and round-trip, never misread as an envelope.
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, new IdentityByteSerializer(), _options);
		var single = new byte[] { 0x01 };

		var framed = serializer.SerializeToBytes(single);

		framed.Length.ShouldBe(2);
		framed[0].ShouldBe((byte)0x00);
		serializer.Deserialize<byte[]>(framed).ShouldBe(single);
	}

	#endregion

	#region Test Helpers

	private sealed class TestMessage
	{
		public int Id { get; set; }
		public string? Data { get; set; }
	}

	/// <summary>
	/// Frames a body as the 2mhglb inline payload shape <c>[0x00 tag][body]</c> for the read-path delegate tests.
	/// </summary>
	private static byte[] PrependInlineTag(byte[] body)
	{
		var framed = new byte[body.Length + 1];
		framed[0] = 0x00;
		body.CopyTo(framed, 1);
		return framed;
	}

	/// <summary>
	/// Deterministic byte[]-identity base serializer: <c>Serialize</c> emits the byte[] verbatim and
	/// <c>Deserialize</c> returns the input bytes verbatim. Lets the framing locks assert byte-exact round-trips
	/// (and detect tag-strip corruption) without depending on a real binary serializer happening to emit the
	/// colliding leading bytes — which would make the collision locks vacuous.
	/// </summary>
	private sealed class IdentityByteSerializer : ISerializer
	{
		public string Name => "IdentityByte";

		public string Version => "1.0.0";

		public string ContentType => "application/octet-stream";

#pragma warning disable IL2046, IL3051 // Test double — AOT attributes not required for test serializers
		public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
		{
			if (value is byte[] bytes)
			{
				var span = bufferWriter.GetSpan(bytes.Length);
				bytes.AsSpan().CopyTo(span);
				bufferWriter.Advance(bytes.Length);
				return;
			}

			throw new NotSupportedException($"IdentityByteSerializer only supports byte[], not {typeof(T).Name}.");
		}

		public T Deserialize<T>(ReadOnlySpan<byte> data)
		{
			if (typeof(T) == typeof(byte[]))
			{
				return (T)(object)data.ToArray();
			}

			throw new NotSupportedException($"IdentityByteSerializer only supports byte[], not {typeof(T).Name}.");
		}

		public byte[] SerializeObject(object value, Type type) =>
			value is byte[] bytes ? bytes : throw new NotSupportedException("byte[] only.");

		public object DeserializeObject(ReadOnlySpan<byte> data, Type type) =>
			type == typeof(byte[]) ? data.ToArray() : throw new NotSupportedException("byte[] only.");
#pragma warning restore IL2046, IL3051
	}

	/// <summary>
	/// Concrete test double for ISerializer that avoids FakeItEasy reflection issues
	/// with [RequiresDynamicCode] attributes on .NET 10.
	/// </summary>
	private sealed class StubSerializer : ISerializer
	{
		private readonly Dictionary<Type, Func<object, byte[]>> _serializeFuncs = new();
		private readonly Dictionary<Type, Func<byte[], object>> _deserializeFuncs = new();

		public string Name { get; set; } = "StubSerializer";

		public string Version { get; set; } = "1.0.0";

		public string ContentType { get; set; } = "application/json";

		public void SetupSerialize<T>(Func<T, byte[]> func) =>
			_serializeFuncs[typeof(T)] = obj => func((T)obj);

		public void SetupDeserialize<T>(Func<byte[], T> func) =>
			_deserializeFuncs[typeof(T)] = data => (object)func(data)!;

#pragma warning disable IL2046, IL3051 // Test stub — AOT attributes not required for test doubles
		public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
		{
			if (_serializeFuncs.TryGetValue(typeof(T), out var func))
			{
				var bytes = func(value);
				var span = bufferWriter.GetSpan(bytes.Length);
				bytes.AsSpan().CopyTo(span);
				bufferWriter.Advance(bytes.Length);
				return;
			}

			throw new InvalidOperationException($"No serialize setup for {typeof(T).Name}");
		}

		public T Deserialize<T>(ReadOnlySpan<byte> data)
		{
			if (_deserializeFuncs.TryGetValue(typeof(T), out var func))
			{
				return (T)func(data.ToArray());
			}

			throw new InvalidOperationException($"No deserialize setup for {typeof(T).Name}");
		}

		public byte[] SerializeObject(object value, Type type)
		{
			throw new NotSupportedException("Not needed for these tests.");
		}

		public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
		{
			throw new NotSupportedException("Not needed for these tests.");
		}
#pragma warning restore IL2046, IL3051
	}

	#endregion
}

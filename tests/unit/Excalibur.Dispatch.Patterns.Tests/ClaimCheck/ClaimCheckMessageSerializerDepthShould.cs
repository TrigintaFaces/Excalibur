// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Depth coverage tests for <see cref="ClaimCheckMessageSerializer"/> edge cases.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Test code")]
[SuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Test code")]
public sealed class ClaimCheckMessageSerializerDepthShould
{
	private readonly IClaimCheckProvider _fakeProvider = A.Fake<IClaimCheckProvider>();

	[Fact]
	public async Task SerializeAsync_ThrowsOperationCanceledException_WhenAlreadyCancelled()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(() =>
			serializer.SerializeAsync(new TestDto { Id = 1 }, cts.Token).AsTask());
	}

	[Fact]
	public async Task DeserializeAsync_ThrowsOperationCanceledException_WhenAlreadyCancelled()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		await Should.ThrowAsync<OperationCanceledException>(() =>
			serializer.DeserializeAsync<TestDto>(new byte[] { 1, 2 }, cts.Token).AsTask());
	}

	[Fact]
	public void Serialize_SmallPayload_DoesNotCallProvider()
	{
		// Arrange
		var options = new ClaimCheckOptions { PayloadThreshold = 1024 };
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, options);
		var message = new TestDto { Id = 42 };

		// Act — uses SerializeToBytes extension which calls Serialize(T, IBufferWriter<byte>)
		var result = serializer.SerializeToBytes(message);

		// Assert — 2mhglb: inline payloads are framed with a leading 0x00 inline tag (no offload).
		result.ShouldNotBeEmpty();
		result[0].ShouldBe((byte)0x00);
		A.CallTo(_fakeProvider).MustNotHaveHappened();
	}

	[Fact]
	public void Serialize_LargePayload_ThrowsNotSupportedException()
	{
		// Arrange - threshold very small so any real payload exceeds it
		var options = new ClaimCheckOptions { PayloadThreshold = 1 };
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, options);

		// Act & Assert
		var ex = Should.Throw<NotSupportedException>(() =>
			serializer.SerializeToBytes(new TestDto { Id = 1, Name = "large" }));
		ex.Message.ShouldContain("SerializeAsync");
	}

	[Fact]
	public void Deserialize_NormalData_DelegatesToBase()
	{
		// Arrange — 2mhglb: a real inline payload is framed [0x00][json]; the reader strips the tag, then delegates.
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		var json = PrependInlineTag(System.Text.Encoding.UTF8.GetBytes("{\"Id\":7}"));

		// Act — uses Deserialize<T>(byte[]) extension which calls Deserialize<T>(ReadOnlySpan<byte>)
		var result = serializer.Deserialize<TestDto>(json);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(7);
	}

	[Fact]
	public void Deserialize_EnvelopeTaggedData_OnSyncPath_ThrowsNotSupportedException()
	{
		// 2mhglb: a 0x01 envelope-tagged frame on the SYNC read path must throw "use DeserializeAsync"
		// (envelope resolution needs the async claim-check provider). RED on the pre-fix mainline, which only
		// recognised a 4-byte "CC01" prefix and treats [0x01,0x00] as base bytes (JsonException instead).
		var data = new byte[] { 0x01, 0x00 };
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);

		// Act & Assert
		var ex = Should.Throw<NotSupportedException>(() =>
			serializer.Deserialize<TestDto>(data));
		ex.Message.ShouldContain("DeserializeAsync");
	}

	[Fact]
	public void Deserialize_ShortUnrecognizedTagData_ThrowsTypedSerializationException()
	{
		// 2mhglb FR-C2b strict framing: a short buffer whose leading byte (0x43) is not a frame tag (0x00/0x01)
		// was NOT produced by ClaimCheckMessageSerializer → typed SerializationException (fail loud), never a
		// base JsonException or a silent wrong decode. RED on the pre-fix mainline (it delegates straight to base).
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		var data = new byte[] { 0x43, 0x43 }; // 0x43 is not a valid frame tag

		// Act & Assert
		Should.Throw<SerializationException>(() =>
			serializer.Deserialize<TestDto>(data));
	}

	[Fact]
	public async Task SerializeAsync_SmallPayload_ReturnsDirectly()
	{
		// Arrange
		var options = new ClaimCheckOptions { PayloadThreshold = 1_000_000 };
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider, options);
		var msg = new TestDto { Id = 42 };

		// Act
		var result = await serializer.SerializeAsync(msg, CancellationToken.None);

		// Assert — 2mhglb: inline async payloads are framed with a leading 0x00 inline tag (no offload).
		result.ShouldNotBeEmpty();
		result[0].ShouldBe((byte)0x00);
		A.CallTo(() => _fakeProvider.StoreAsync(A<byte[]>._, A<CancellationToken>._, A<ClaimCheckMetadata?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void Name_IncludesBaseSerializerName()
	{
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		serializer.Name.ShouldStartWith("ClaimCheck-");
	}

	[Fact]
	public void Version_Returns100()
	{
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		serializer.Version.ShouldBe("1.0.0");
	}

	[Fact]
	public void ContentType_DelegatesToBase()
	{
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		serializer.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void Deserialize_Span_DelegatesToBase()
	{
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		var bytes = PrependInlineTag(System.Text.Encoding.UTF8.GetBytes("{\"Id\":99}")); // 2mhglb inline frame

		var result = serializer.Deserialize<TestDto>((ReadOnlySpan<byte>)bytes);

		result.ShouldNotBeNull();
		result.Id.ShouldBe(99);
	}

	private sealed class TestDto
	{
		public int Id { get; set; }
		public string? Name { get; set; }
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
}
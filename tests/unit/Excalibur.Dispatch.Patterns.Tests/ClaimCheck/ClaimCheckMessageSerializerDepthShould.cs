// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Depth coverage tests for <see cref="ClaimCheckMessageSerializer"/> edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
		var cts = new CancellationTokenSource();
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
		var cts = new CancellationTokenSource();
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

		// Act
		var result = serializer.Serialize(message);

		// Assert
		result.ShouldNotBeEmpty();
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
			serializer.Serialize(new TestDto { Id = 1, Name = "large" }));
		ex.Message.ShouldContain("SerializeAsync");
	}

	[Fact]
	public void Deserialize_NormalData_DelegatesToBase()
	{
		// Arrange
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		var json = System.Text.Encoding.UTF8.GetBytes("{\"Id\":7}");

		// Act
		var result = serializer.Deserialize<TestDto>(json);

		// Assert
		result.ShouldNotBeNull();
		result.Id.ShouldBe(7);
	}

	[Fact]
	public void Deserialize_MagicPrefixData_ThrowsNotSupportedException()
	{
		// Arrange - data starts with "CC01" magic prefix
		var data = new byte[] { 0x43, 0x43, 0x30, 0x31, 0x00 };
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);

		// Act & Assert
		var ex = Should.Throw<NotSupportedException>(() =>
			serializer.Deserialize<TestDto>(data));
		ex.Message.ShouldContain("DeserializeAsync");
	}

	[Fact]
	public void Deserialize_TooShortData_ForMagicPrefix_DoesNotThrow()
	{
		// Arrange - data shorter than 4 bytes cannot contain magic prefix
		// But it might still fail JSON deserialization, which is fine
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		var data = new byte[] { 0x43, 0x43 }; // Only 2 bytes

		// Act & Assert - Should not throw NotSupportedException (not a claim check envelope)
		// May throw JsonException from base serializer, which is expected
		Should.Throw<System.Text.Json.JsonException>(() =>
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

		// Assert
		result.ShouldNotBeEmpty();
		A.CallTo(() => _fakeProvider.StoreAsync(A<byte[]>._, A<CancellationToken>._, A<ClaimCheckMetadata?>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void SerializerName_IncludesBaseSerializerName()
	{
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		serializer.SerializerName.ShouldStartWith("ClaimCheck-");
	}

	[Fact]
	public void SerializerVersion_Returns100()
	{
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		serializer.SerializerVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void Format_IncludesBaseFormat()
	{
		var serializer = new ClaimCheckMessageSerializer(_fakeProvider);
		serializer.Format.ShouldStartWith("ClaimCheck(");
		serializer.Format.ShouldEndWith(")");
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
		var bytes = System.Text.Encoding.UTF8.GetBytes("{\"Id\":99}");

		var result = serializer.Deserialize<TestDto>((ReadOnlySpan<byte>)bytes);

		result.ShouldNotBeNull();
		result.Id.ShouldBe(99);
	}

	private sealed class TestDto
	{
		public int Id { get; set; }
		public string? Name { get; set; }
	}
}

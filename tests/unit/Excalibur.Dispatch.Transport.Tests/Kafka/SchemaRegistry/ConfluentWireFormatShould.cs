// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.SchemaRegistry;

/// <summary>
/// Unit tests for the <see cref="ConfluentWireFormat"/> static class.
/// </summary>
/// <remarks>
/// Sprint 513 (S513.3): Schema Registry unit tests.
/// Tests verify wire format header reading/writing operations.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Kafka")]
[Trait("Feature", "SchemaRegistry")]
public sealed class ConfluentWireFormatShould
{
	#region Constant Tests

	[Fact]
	public void MagicByte_IsZero()
	{
		// Assert
		ConfluentWireFormat.MagicByte.ShouldBe((byte)0x00);
	}

	[Fact]
	public void HeaderSize_IsFive()
	{
		// Assert
		ConfluentWireFormat.HeaderSize.ShouldBe(5);
	}

	#endregion

	#region WriteHeader Tests

	[Fact]
	public void WriteHeader_WritesMagicByte()
	{
		// Arrange
		var buffer = new byte[5];

		// Act
		ConfluentWireFormat.WriteHeader(buffer, 123);

		// Assert
		buffer[0].ShouldBe((byte)0x00);
	}

	[Fact]
	public void WriteHeader_WritesSchemaIdBigEndian()
	{
		// Arrange
		var buffer = new byte[5];
		const int schemaId = 0x01020304;

		// Act
		ConfluentWireFormat.WriteHeader(buffer, schemaId);

		// Assert - Big-endian: most significant byte first
		buffer[1].ShouldBe((byte)0x01);
		buffer[2].ShouldBe((byte)0x02);
		buffer[3].ShouldBe((byte)0x03);
		buffer[4].ShouldBe((byte)0x04);
	}

	[Fact]
	public void WriteHeader_ThrowsForShortBuffer()
	{
		// Arrange
		var buffer = new byte[4]; // Too short

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			ConfluentWireFormat.WriteHeader(buffer, 123));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(65535)]
	[InlineData(int.MaxValue)]
	public void WriteHeader_HandlesVariousSchemaIds(int schemaId)
	{
		// Arrange
		var buffer = new byte[5];

		// Act
		ConfluentWireFormat.WriteHeader(buffer, schemaId);

		// Assert - Should be readable back
		var readId = ConfluentWireFormat.ReadSchemaId(buffer);
		readId.ShouldBe(schemaId);
	}

	#endregion

	#region ReadSchemaId Tests

	[Fact]
	public void ReadSchemaId_ReadsCorrectId()
	{
		// Arrange - Magic byte + schema ID 123 in big-endian
		var buffer = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x7B };

		// Act
		var schemaId = ConfluentWireFormat.ReadSchemaId(buffer);

		// Assert
		schemaId.ShouldBe(123);
	}

	[Fact]
	public void ReadSchemaId_ThrowsForShortMessage()
	{
		// Arrange
		var buffer = new byte[4];

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			ConfluentWireFormat.ReadSchemaId(buffer));
	}

	[Fact]
	public void ReadSchemaId_ThrowsForInvalidMagicByte()
	{
		// Arrange
		var buffer = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01 }; // Wrong magic byte

		// Act & Assert
		var ex = Should.Throw<InvalidOperationException>(() =>
			ConfluentWireFormat.ReadSchemaId(buffer));
		ex.Message.ShouldContain("magic byte");
	}

	#endregion

	#region TryReadSchemaId Tests

	[Fact]
	public void TryReadSchemaId_ReturnsTrueForValidMessage()
	{
		// Arrange
		var buffer = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x7B };

		// Act
		var result = ConfluentWireFormat.TryReadSchemaId(buffer, out var schemaId);

		// Assert
		result.ShouldBeTrue();
		schemaId.ShouldBe(123);
	}

	[Fact]
	public void TryReadSchemaId_ReturnsFalseForShortMessage()
	{
		// Arrange
		var buffer = new byte[4];

		// Act
		var result = ConfluentWireFormat.TryReadSchemaId(buffer, out var schemaId);

		// Assert
		result.ShouldBeFalse();
		schemaId.ShouldBe(0);
	}

	[Fact]
	public void TryReadSchemaId_ReturnsFalseForInvalidMagicByte()
	{
		// Arrange
		var buffer = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01 };

		// Act
		var result = ConfluentWireFormat.TryReadSchemaId(buffer, out var schemaId);

		// Assert
		result.ShouldBeFalse();
		schemaId.ShouldBe(0);
	}

	#endregion

	#region GetPayload Tests

	[Fact]
	public void GetPayload_ReturnsPayloadBytes()
	{
		// Arrange
		var buffer = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01, 0xAA, 0xBB, 0xCC };

		// Act
		var payload = ConfluentWireFormat.GetPayload(buffer);

		// Assert
		payload.Length.ShouldBe(3);
		payload[0].ShouldBe((byte)0xAA);
		payload[1].ShouldBe((byte)0xBB);
		payload[2].ShouldBe((byte)0xCC);
	}

	[Fact]
	public void GetPayload_ReturnsEmptyForHeaderOnly()
	{
		// Arrange
		var buffer = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01 };

		// Act
		var payload = ConfluentWireFormat.GetPayload(buffer);

		// Assert
		payload.Length.ShouldBe(0);
	}

	[Fact]
	public void GetPayload_ThrowsForShortMessage()
	{
		// Arrange
		var buffer = new byte[4];

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			ConfluentWireFormat.GetPayload(buffer));
	}

	[Fact]
	public void GetPayload_ThrowsForInvalidMagicByte()
	{
		// Arrange
		var buffer = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01, 0xAA };

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			ConfluentWireFormat.GetPayload(buffer));
	}

	#endregion

	#region TryGetPayload Tests

	[Fact]
	public void TryGetPayload_ReturnsTrueForValidMessage()
	{
		// Arrange
		var buffer = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01, 0xAA, 0xBB };

		// Act
		var result = ConfluentWireFormat.TryGetPayload(buffer, out var payload);

		// Assert
		result.ShouldBeTrue();
		payload.Length.ShouldBe(2);
	}

	[Fact]
	public void TryGetPayload_ReturnsFalseForShortMessage()
	{
		// Arrange
		var buffer = new byte[4];

		// Act
		var result = ConfluentWireFormat.TryGetPayload(buffer, out var payload);

		// Assert
		result.ShouldBeFalse();
		payload.Length.ShouldBe(0);
	}

	[Fact]
	public void TryGetPayload_ReturnsFalseForInvalidMagicByte()
	{
		// Arrange
		var buffer = new byte[] { 0xFF, 0x00, 0x00, 0x00, 0x01, 0xAA };

		// Act
		var result = ConfluentWireFormat.TryGetPayload(buffer, out var payload);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsWireFormat Tests

	[Fact]
	public void IsWireFormat_ReturnsTrueForValidFormat()
	{
		// Arrange
		var buffer = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01 };

		// Act
		var result = ConfluentWireFormat.IsWireFormat(buffer);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsWireFormat_ReturnsTrueWithPayload()
	{
		// Arrange
		var buffer = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x01, 0xAA, 0xBB };

		// Act
		var result = ConfluentWireFormat.IsWireFormat(buffer);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsWireFormat_ReturnsFalseForShortMessage()
	{
		// Arrange
		var buffer = new byte[4];

		// Act
		var result = ConfluentWireFormat.IsWireFormat(buffer);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsWireFormat_ReturnsFalseForWrongMagicByte()
	{
		// Arrange
		var buffer = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01 };

		// Act
		var result = ConfluentWireFormat.IsWireFormat(buffer);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsWireFormat_ReturnsFalseForEmptySpan()
	{
		// Arrange
		var buffer = Array.Empty<byte>();

		// Act
		var result = ConfluentWireFormat.IsWireFormat(buffer);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region Round-Trip Tests

	[Theory]
	[InlineData(1)]
	[InlineData(100)]
	[InlineData(12345)]
	[InlineData(1000000)]
	public void WriteAndRead_RoundTripsCorrectly(int schemaId)
	{
		// Arrange
		var buffer = new byte[10];

		// Act
		ConfluentWireFormat.WriteHeader(buffer, schemaId);
		var readId = ConfluentWireFormat.ReadSchemaId(buffer);

		// Assert
		readId.ShouldBe(schemaId);
	}

	[Fact]
	public void WritePayloadAndGetPayload_PreservesData()
	{
		// Arrange
		var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
		var buffer = new byte[5 + payload.Length];
		ConfluentWireFormat.WriteHeader(buffer, 999);
		payload.CopyTo(buffer.AsSpan(5));

		// Act
		var extractedPayload = ConfluentWireFormat.GetPayload(buffer);

		// Assert
		extractedPayload.SequenceEqual(payload).ShouldBeTrue();
	}

	#endregion
}

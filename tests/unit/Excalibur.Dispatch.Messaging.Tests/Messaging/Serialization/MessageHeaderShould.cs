// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Messaging.Serialization;

/// <summary>
/// Unit tests for <see cref="MessageHeader"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Priority", "0")]
public sealed class MessageHeaderShould
{
	#region MagicValue Tests

	[Fact]
	public void MagicValue_HasExpectedValue()
	{
		// Assert - 0x45584D53 = "EXMS" in ASCII
		MessageHeader.MagicValue.ShouldBe(0x45584D53u);
	}

	#endregion

	#region Field Tests

	[Fact]
	public void Magic_CanBeSet()
	{
		// Arrange
		var header = new MessageHeader();

		// Act
		header.Magic = MessageHeader.MagicValue;

		// Assert
		header.Magic.ShouldBe(MessageHeader.MagicValue);
	}

	[Fact]
	public void Version_CanBeSet()
	{
		// Arrange
		var header = new MessageHeader();

		// Act
		header.Version = 1;

		// Assert
		header.Version.ShouldBe((byte)1);
	}

	[Fact]
	public void TypeId_CanBeSet()
	{
		// Arrange
		var header = new MessageHeader();

		// Act
		header.TypeId = 12345u;

		// Assert
		header.TypeId.ShouldBe(12345u);
	}

	[Fact]
	public void PayloadSize_CanBeSet()
	{
		// Arrange
		var header = new MessageHeader();

		// Act
		header.PayloadSize = 1024;

		// Assert
		header.PayloadSize.ShouldBe(1024);
	}

	[Fact]
	public void Timestamp_CanBeSet()
	{
		// Arrange
		var header = new MessageHeader();
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		// Act
		header.Timestamp = timestamp;

		// Assert
		header.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void Checksum_CanBeSet()
	{
		// Arrange
		var header = new MessageHeader();

		// Act
		header.Checksum = 0xDEADBEEF;

		// Assert
		header.Checksum.ShouldBe(0xDEADBEEF);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameValues_ReturnsTrue()
	{
		// Arrange
		var header1 = new MessageHeader
		{
			Magic = MessageHeader.MagicValue,
			Version = 1,
			TypeId = 100,
			PayloadSize = 500,
			Timestamp = 1000L,
			Checksum = 0xABCD,
		};
		var header2 = new MessageHeader
		{
			Magic = MessageHeader.MagicValue,
			Version = 1,
			TypeId = 100,
			PayloadSize = 500,
			Timestamp = 1000L,
			Checksum = 0xABCD,
		};

		// Assert
		header1.Equals(header2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentMagic_ReturnsFalse()
	{
		// Arrange
		var header1 = new MessageHeader { Magic = 0x12345678 };
		var header2 = new MessageHeader { Magic = 0x87654321 };

		// Assert
		header1.Equals(header2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentVersion_ReturnsFalse()
	{
		// Arrange
		var header1 = new MessageHeader { Magic = MessageHeader.MagicValue, Version = 1 };
		var header2 = new MessageHeader { Magic = MessageHeader.MagicValue, Version = 2 };

		// Assert
		header1.Equals(header2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentTypeId_ReturnsFalse()
	{
		// Arrange
		var header1 = new MessageHeader { Magic = MessageHeader.MagicValue, TypeId = 100 };
		var header2 = new MessageHeader { Magic = MessageHeader.MagicValue, TypeId = 200 };

		// Assert
		header1.Equals(header2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentPayloadSize_ReturnsFalse()
	{
		// Arrange
		var header1 = new MessageHeader { Magic = MessageHeader.MagicValue, PayloadSize = 100 };
		var header2 = new MessageHeader { Magic = MessageHeader.MagicValue, PayloadSize = 200 };

		// Assert
		header1.Equals(header2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentTimestamp_ReturnsFalse()
	{
		// Arrange
		var header1 = new MessageHeader { Magic = MessageHeader.MagicValue, Timestamp = 1000L };
		var header2 = new MessageHeader { Magic = MessageHeader.MagicValue, Timestamp = 2000L };

		// Assert
		header1.Equals(header2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentChecksum_ReturnsFalse()
	{
		// Arrange
		var header1 = new MessageHeader { Magic = MessageHeader.MagicValue, Checksum = 0xAAAA };
		var header2 = new MessageHeader { Magic = MessageHeader.MagicValue, Checksum = 0xBBBB };

		// Assert
		header1.Equals(header2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithObject_ReturnsTrueForEqualHeader()
	{
		// Arrange
		var header = new MessageHeader { Magic = MessageHeader.MagicValue };
		object boxed = new MessageHeader { Magic = MessageHeader.MagicValue };

		// Assert
		header.Equals(boxed).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var header = new MessageHeader { Magic = MessageHeader.MagicValue };

		// Assert
		header.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithDifferentType_ReturnsFalse()
	{
		// Arrange
		var header = new MessageHeader { Magic = MessageHeader.MagicValue };

		// Assert
		header.Equals("not a header").ShouldBeFalse();
	}

	#endregion

	#region Operator Tests

	[Fact]
	public void EqualityOperator_WithSameValues_ReturnsTrue()
	{
		// Arrange
		var header1 = new MessageHeader { Magic = MessageHeader.MagicValue, Version = 1 };
		var header2 = new MessageHeader { Magic = MessageHeader.MagicValue, Version = 1 };

		// Assert
		(header1 == header2).ShouldBeTrue();
	}

	[Fact]
	public void InequalityOperator_WithDifferentValues_ReturnsTrue()
	{
		// Arrange
		var header1 = new MessageHeader { Magic = MessageHeader.MagicValue, Version = 1 };
		var header2 = new MessageHeader { Magic = MessageHeader.MagicValue, Version = 2 };

		// Assert
		(header1 != header2).ShouldBeTrue();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_EqualHeaders_ReturnSameValue()
	{
		// Arrange
		var header1 = new MessageHeader
		{
			Magic = MessageHeader.MagicValue,
			Version = 1,
			TypeId = 100,
			PayloadSize = 500,
			Timestamp = 1000L,
			Checksum = 0xABCD,
		};
		var header2 = new MessageHeader
		{
			Magic = MessageHeader.MagicValue,
			Version = 1,
			TypeId = 100,
			PayloadSize = 500,
			Timestamp = 1000L,
			Checksum = 0xABCD,
		};

		// Assert
		header1.GetHashCode().ShouldBe(header2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_ReturnsConsistentValue()
	{
		// Arrange
		var header = new MessageHeader { Magic = MessageHeader.MagicValue, Version = 1 };

		// Act
		var hash1 = header.GetHashCode();
		var hash2 = header.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);
	}

	#endregion

	#region Default Struct Tests

	[Fact]
	public void DefaultHeader_HasZeroValues()
	{
		// Arrange
		var header = default(MessageHeader);

		// Assert
		header.Magic.ShouldBe(0u);
		header.Version.ShouldBe((byte)0);
		header.TypeId.ShouldBe(0u);
		header.PayloadSize.ShouldBe(0);
		header.Timestamp.ShouldBe(0L);
		header.Checksum.ShouldBe(0u);
	}

	#endregion
}

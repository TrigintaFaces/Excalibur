// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="SerializerIds"/>.
/// </summary>
/// <remarks>
/// Tests the well-known serializer IDs and validation methods.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Priority", "0")]
public sealed class SerializerIdsShould : UnitTestBase
{
	#region Constant Value Tests

	[Fact]
	public void MemoryPack_IsOne()
	{
		// Assert
		SerializerIds.MemoryPack.ShouldBe((byte)1);
	}

	[Fact]
	public void SystemTextJson_IsTwo()
	{
		// Assert
		SerializerIds.SystemTextJson.ShouldBe((byte)2);
	}

	[Fact]
	public void MessagePack_IsThree()
	{
		// Assert
		SerializerIds.MessagePack.ShouldBe((byte)3);
	}

	[Fact]
	public void Protobuf_IsFour()
	{
		// Assert
		SerializerIds.Protobuf.ShouldBe((byte)4);
	}

	[Fact]
	public void FrameworkReservedStart_IsFive()
	{
		// Assert
		SerializerIds.FrameworkReservedStart.ShouldBe((byte)5);
	}

	[Fact]
	public void FrameworkReservedEnd_Is199()
	{
		// Assert
		SerializerIds.FrameworkReservedEnd.ShouldBe((byte)199);
	}

	[Fact]
	public void CustomRangeStart_Is200()
	{
		// Assert
		SerializerIds.CustomRangeStart.ShouldBe((byte)200);
	}

	[Fact]
	public void CustomRangeEnd_Is254()
	{
		// Assert
		SerializerIds.CustomRangeEnd.ShouldBe((byte)254);
	}

	[Fact]
	public void Invalid_IsZero()
	{
		// Assert
		SerializerIds.Invalid.ShouldBe((byte)0);
	}

	[Fact]
	public void Unknown_Is255()
	{
		// Assert
		SerializerIds.Unknown.ShouldBe((byte)255);
	}

	#endregion

	#region IsValidId Tests

	[Theory]
	[InlineData(1, true)]
	[InlineData(2, true)]
	[InlineData(3, true)]
	[InlineData(4, true)]
	[InlineData(100, true)]
	[InlineData(200, true)]
	[InlineData(254, true)]
	public void IsValidId_WithValidIds_ReturnsTrue(byte id, bool expected)
	{
		// Act
		var result = SerializerIds.IsValidId(id);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(255)]
	public void IsValidId_WithInvalidIds_ReturnsFalse(byte id)
	{
		// Act
		var result = SerializerIds.IsValidId(id);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsFrameworkId Tests

	[Theory]
	[InlineData(1, true)]
	[InlineData(2, true)]
	[InlineData(3, true)]
	[InlineData(4, true)]
	[InlineData(100, true)]
	[InlineData(199, true)]
	public void IsFrameworkId_WithFrameworkIds_ReturnsTrue(byte id, bool expected)
	{
		// Act
		var result = SerializerIds.IsFrameworkId(id);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(200)]
	[InlineData(254)]
	[InlineData(255)]
	public void IsFrameworkId_WithNonFrameworkIds_ReturnsFalse(byte id)
	{
		// Act
		var result = SerializerIds.IsFrameworkId(id);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsCustomId Tests

	[Theory]
	[InlineData(200, true)]
	[InlineData(220, true)]
	[InlineData(254, true)]
	public void IsCustomId_WithCustomIds_ReturnsTrue(byte id, bool expected)
	{
		// Act
		var result = SerializerIds.IsCustomId(id);

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(199)]
	[InlineData(255)]
	public void IsCustomId_WithNonCustomIds_ReturnsFalse(byte id)
	{
		// Act
		var result = SerializerIds.IsCustomId(id);

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region IsValidSerializerId Tests

	[Fact]
	public void IsValidSerializerId_DelegatesToIsValidId()
	{
		// Assert
		SerializerIds.IsValidSerializerId(1).ShouldBeTrue();
		SerializerIds.IsValidSerializerId(254).ShouldBeTrue();
		SerializerIds.IsValidSerializerId(0).ShouldBeFalse();
		SerializerIds.IsValidSerializerId(255).ShouldBeFalse();
	}

	#endregion

	#region Range Consistency Tests

	[Fact]
	public void BuiltInSerializers_AreInFrameworkRange()
	{
		// Assert
		SerializerIds.IsFrameworkId(SerializerIds.MemoryPack).ShouldBeTrue();
		SerializerIds.IsFrameworkId(SerializerIds.SystemTextJson).ShouldBeTrue();
		SerializerIds.IsFrameworkId(SerializerIds.MessagePack).ShouldBeTrue();
		SerializerIds.IsFrameworkId(SerializerIds.Protobuf).ShouldBeTrue();
	}

	[Fact]
	public void FrameworkRange_DoesNotOverlapWithCustomRange()
	{
		// Assert
		(SerializerIds.FrameworkReservedEnd < SerializerIds.CustomRangeStart).ShouldBeTrue();
	}

	[Fact]
	public void AllValidIds_AreNotInvalidOrUnknown()
	{
		// Test full range
		for (byte id = 1; id < 255; id++)
		{
			SerializerIds.IsValidId(id).ShouldBeTrue($"ID {id} should be valid");
		}

		// Invalid/Unknown
		SerializerIds.IsValidId(0).ShouldBeFalse();
		SerializerIds.IsValidId(255).ShouldBeFalse();
	}

	#endregion

	#region Static Class Tests

	[Fact]
	public void IsStaticClass()
	{
		// Assert
		typeof(SerializerIds).IsAbstract.ShouldBeTrue();
		typeof(SerializerIds).IsSealed.ShouldBeTrue();
	}

	#endregion
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.ContextValues;

/// <summary>
/// Unit tests for <see cref="CausationId"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class CausationIdShould
{
	#region Constructor Tests

	[Fact]
	public void Create_WithGuid_SetsValue()
	{
		// Arrange
		var guid = Guid.NewGuid();

		// Act
		var causationId = new CausationId(guid);

		// Assert
		causationId.Value.ShouldBe(guid);
	}

	[Fact]
	public void Create_WithValidString_ParsesValue()
	{
		// Arrange
		var guidString = "12345678-1234-1234-1234-123456789abc";

		// Act
		var causationId = new CausationId(guidString);

		// Assert
		causationId.Value.ShouldBe(Guid.Parse(guidString));
	}

	[Fact]
	public void Create_WithDefaultConstructor_GeneratesNewGuid()
	{
		// Arrange & Act
		var causationId = new CausationId();

		// Assert
		causationId.Value.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void Create_WithNullString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new CausationId(null));
	}

	[Fact]
	public void Create_WithEmptyString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new CausationId(""));
	}

	[Fact]
	public void Create_WithWhitespaceString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new CausationId("   "));
	}

	[Fact]
	public void Create_WithInvalidGuidFormat_ThrowsFormatException()
	{
		// Arrange & Act & Assert
		Should.Throw<FormatException>(() => new CausationId("not-a-guid"));
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsGuidString()
	{
		// Arrange
		var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
		var causationId = new CausationId(guid);

		// Act
		var result = causationId.ToString();

		// Assert
		result.ShouldBe("12345678-1234-1234-1234-123456789abc");
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_SameGuid_ReturnsTrue()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var causationId1 = new CausationId(guid);
		var causationId2 = new CausationId(guid);

		// Act & Assert
		causationId1.Equals(causationId2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_DifferentGuid_ReturnsFalse()
	{
		// Arrange
		var causationId1 = new CausationId(Guid.NewGuid());
		var causationId2 = new CausationId(Guid.NewGuid());

		// Act & Assert
		causationId1.Equals(causationId2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_SameReference_ReturnsTrue()
	{
		// Arrange
		var causationId = new CausationId();

		// Act & Assert
		causationId.Equals(causationId).ShouldBeTrue();
	}

	[Fact]
	public void Equals_Null_ReturnsFalse()
	{
		// Arrange
		var causationId = new CausationId();

		// Act & Assert
		causationId.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_Object_SameGuid_ReturnsTrue()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var causationId1 = new CausationId(guid);
		object causationId2 = new CausationId(guid);

		// Act & Assert
		causationId1.Equals(causationId2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_Object_DifferentType_ReturnsFalse()
	{
		// Arrange
		var causationId = new CausationId();
		object other = "not a causation id";

		// Act & Assert
		causationId.Equals(other).ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_SameGuid_ReturnsSameHash()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var causationId1 = new CausationId(guid);
		var causationId2 = new CausationId(guid);

		// Act & Assert
		causationId1.GetHashCode().ShouldBe(causationId2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_MatchesGuidHashCode()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var causationId = new CausationId(guid);

		// Act & Assert
		causationId.GetHashCode().ShouldBe(guid.GetHashCode());
	}

	#endregion

	#region Interface Implementation

	[Fact]
	public void ImplementsICausationId()
	{
		// Arrange
		var causationId = new CausationId();

		// Assert
		causationId.ShouldBeAssignableTo<ICausationId>();
	}

	[Fact]
	public void ImplementsIEquatable()
	{
		// Arrange
		var causationId = new CausationId();

		// Assert
		causationId.ShouldBeAssignableTo<IEquatable<CausationId>>();
	}

	#endregion
}

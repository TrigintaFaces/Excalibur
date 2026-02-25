// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.ContextValues;

/// <summary>
/// Unit tests for <see cref="CausationId"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ContextValues")]
[Trait("Priority", "0")]
public sealed class CausationIdShould
{
	#region Constructor Tests - Guid

	[Fact]
	public void Constructor_WithGuid_SetsValue()
	{
		// Arrange
		var guid = Guid.NewGuid();

		// Act
		var causationId = new CausationId(guid);

		// Assert
		causationId.Value.ShouldBe(guid);
	}

	[Fact]
	public void Constructor_WithEmptyGuid_SetsEmptyGuid()
	{
		// Act
		var causationId = new CausationId(Guid.Empty);

		// Assert
		causationId.Value.ShouldBe(Guid.Empty);
	}

	#endregion

	#region Constructor Tests - String

	[Fact]
	public void Constructor_WithValidString_ParsesGuid()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var guidString = guid.ToString();

		// Act
		var causationId = new CausationId(guidString);

		// Assert
		causationId.Value.ShouldBe(guid);
	}

	[Fact]
	public void Constructor_WithNullString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new CausationId((string?)null));
	}

	[Fact]
	public void Constructor_WithEmptyString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new CausationId(string.Empty));
	}

	[Fact]
	public void Constructor_WithWhitespaceString_ThrowsArgumentException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new CausationId("   "));
	}

	[Fact]
	public void Constructor_WithInvalidString_ThrowsFormatException()
	{
		// Act & Assert
		_ = Should.Throw<FormatException>(() => new CausationId("not-a-guid"));
	}

	#endregion

	#region Parameterless Constructor Tests

	[Fact]
	public void ParameterlessConstructor_GeneratesNewGuid()
	{
		// Act
		var causationId = new CausationId();

		// Assert
		causationId.Value.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void ParameterlessConstructor_GeneratesUniqueValues()
	{
		// Act
		var causationId1 = new CausationId();
		var causationId2 = new CausationId();

		// Assert
		causationId1.Value.ShouldNotBe(causationId2.Value);
	}

	#endregion

	#region Value Property Tests

	[Fact]
	public void Value_CanBeSet()
	{
		// Arrange
		var causationId = new CausationId();
		var newGuid = Guid.NewGuid();

		// Act
		causationId.Value = newGuid;

		// Assert
		causationId.Value.ShouldBe(newGuid);
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsGuidString()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var causationId = new CausationId(guid);

		// Act
		var result = causationId.ToString();

		// Assert
		result.ShouldBe(guid.ToString());
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_WithSameValue_ReturnsTrue()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var causationId1 = new CausationId(guid);
		var causationId2 = new CausationId(guid);

		// Act & Assert
		causationId1.Equals(causationId2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentValue_ReturnsFalse()
	{
		// Arrange
		var causationId1 = new CausationId(Guid.NewGuid());
		var causationId2 = new CausationId(Guid.NewGuid());

		// Act & Assert
		causationId1.Equals(causationId2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var causationId = new CausationId();

		// Act & Assert
		causationId.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithSameReference_ReturnsTrue()
	{
		// Arrange
		var causationId = new CausationId();

		// Act & Assert
		causationId.Equals(causationId).ShouldBeTrue();
	}

	[Fact]
	public void ObjectEquals_WithSameValue_ReturnsTrue()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var causationId1 = new CausationId(guid);
		object causationId2 = new CausationId(guid);

		// Act & Assert
		causationId1.Equals(causationId2).ShouldBeTrue();
	}

	[Fact]
	public void ObjectEquals_WithNonCausationId_ReturnsFalse()
	{
		// Arrange
		var causationId = new CausationId();

		// Act & Assert
		causationId.Equals("not a causation id").ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_WithSameValue_ReturnsSameHash()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var causationId1 = new CausationId(guid);
		var causationId2 = new CausationId(guid);

		// Act & Assert
		causationId1.GetHashCode().ShouldBe(causationId2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_WithDifferentValue_ReturnsDifferentHash()
	{
		// Arrange
		var causationId1 = new CausationId(Guid.NewGuid());
		var causationId2 = new CausationId(Guid.NewGuid());

		// Act & Assert
		causationId1.GetHashCode().ShouldNotBe(causationId2.GetHashCode());
	}

	#endregion

	#region ICausationId Interface Tests

	[Fact]
	public void ImplementsICausationIdInterface()
	{
		// Arrange
		var causationId = new CausationId();

		// Assert
		_ = causationId.ShouldBeAssignableTo<ICausationId>();
	}

	[Fact]
	public void ICausationId_Value_ReturnsCorrectValue()
	{
		// Arrange
		var guid = Guid.NewGuid();
		ICausationId causationId = new CausationId(guid);

		// Act & Assert
		causationId.Value.ShouldBe(guid);
	}

	#endregion

	#region IEquatable Interface Tests

	[Fact]
	public void ImplementsIEquatableInterface()
	{
		// Arrange
		var causationId = new CausationId();

		// Assert
		_ = causationId.ShouldBeAssignableTo<IEquatable<CausationId>>();
	}

	#endregion
}

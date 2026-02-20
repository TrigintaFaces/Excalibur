// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.ContextValues;

/// <summary>
/// Unit tests for <see cref="CorrelationId"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class CorrelationIdShould
{
	#region Constructor Tests

	[Fact]
	public void Create_WithGuid_SetsValue()
	{
		// Arrange
		var guid = Guid.NewGuid();

		// Act
		var correlationId = new CorrelationId(guid);

		// Assert
		correlationId.Value.ShouldBe(guid);
	}

	[Fact]
	public void Create_WithValidString_ParsesValue()
	{
		// Arrange
		var guidString = "abcdef01-2345-6789-abcd-ef0123456789";

		// Act
		var correlationId = new CorrelationId(guidString);

		// Assert
		correlationId.Value.ShouldBe(Guid.Parse(guidString));
	}

	[Fact]
	public void Create_WithDefaultConstructor_GeneratesNewGuid()
	{
		// Arrange & Act
		var correlationId = new CorrelationId();

		// Assert
		correlationId.Value.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void Create_WithNullString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new CorrelationId(null));
	}

	[Fact]
	public void Create_WithEmptyString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new CorrelationId(""));
	}

	[Fact]
	public void Create_WithWhitespaceString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() => new CorrelationId("   "));
	}

	[Fact]
	public void Create_WithInvalidGuidFormat_ThrowsFormatException()
	{
		// Arrange & Act & Assert
		Should.Throw<FormatException>(() => new CorrelationId("invalid-guid-format"));
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsGuidString()
	{
		// Arrange
		var guid = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789");
		var correlationId = new CorrelationId(guid);

		// Act
		var result = correlationId.ToString();

		// Assert
		result.ShouldBe("abcdef01-2345-6789-abcd-ef0123456789");
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_SameGuid_ReturnsTrue()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var correlationId1 = new CorrelationId(guid);
		var correlationId2 = new CorrelationId(guid);

		// Act & Assert
		correlationId1.Equals(correlationId2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_DifferentGuid_ReturnsFalse()
	{
		// Arrange
		var correlationId1 = new CorrelationId(Guid.NewGuid());
		var correlationId2 = new CorrelationId(Guid.NewGuid());

		// Act & Assert
		correlationId1.Equals(correlationId2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_SameReference_ReturnsTrue()
	{
		// Arrange
		var correlationId = new CorrelationId();

		// Act & Assert
		correlationId.Equals(correlationId).ShouldBeTrue();
	}

	[Fact]
	public void Equals_Null_ReturnsFalse()
	{
		// Arrange
		var correlationId = new CorrelationId();

		// Act & Assert
		correlationId.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_Object_SameGuid_ReturnsTrue()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var correlationId1 = new CorrelationId(guid);
		object correlationId2 = new CorrelationId(guid);

		// Act & Assert
		correlationId1.Equals(correlationId2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_Object_DifferentType_ReturnsFalse()
	{
		// Arrange
		var correlationId = new CorrelationId();
		object other = "not a correlation id";

		// Act & Assert
		correlationId.Equals(other).ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_SameGuid_ReturnsSameHash()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var correlationId1 = new CorrelationId(guid);
		var correlationId2 = new CorrelationId(guid);

		// Act & Assert
		correlationId1.GetHashCode().ShouldBe(correlationId2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_MatchesGuidHashCode()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var correlationId = new CorrelationId(guid);

		// Act & Assert
		correlationId.GetHashCode().ShouldBe(guid.GetHashCode());
	}

	#endregion

	#region Interface Implementation

	[Fact]
	public void ImplementsICorrelationId()
	{
		// Arrange
		var correlationId = new CorrelationId();

		// Assert
		correlationId.ShouldBeAssignableTo<ICorrelationId>();
	}

	[Fact]
	public void ImplementsIEquatable()
	{
		// Arrange
		var correlationId = new CorrelationId();

		// Assert
		correlationId.ShouldBeAssignableTo<IEquatable<CorrelationId>>();
	}

	#endregion
}

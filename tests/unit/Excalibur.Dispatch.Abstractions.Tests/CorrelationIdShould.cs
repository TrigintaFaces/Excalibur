// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for CorrelationId value object.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CorrelationIdShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaultConstructor_GeneratesNewGuid()
	{
		// Arrange & Act
		var correlationId = new CorrelationId();

		// Assert
		correlationId.Value.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void Create_WithGuid_StoresValue()
	{
		// Arrange
		var guid = Guid.NewGuid();

		// Act
		var correlationId = new CorrelationId(guid);

		// Assert
		correlationId.Value.ShouldBe(guid);
	}

	[Fact]
	public void Create_WithValidString_ParsesGuid()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var guidString = guid.ToString();

		// Act
		var correlationId = new CorrelationId(guidString);

		// Assert
		correlationId.Value.ShouldBe(guid);
	}

	[Fact]
	public void Create_WithNullString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() => new CorrelationId(null));
	}

	[Fact]
	public void Create_WithEmptyString_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentException>(() => new CorrelationId(string.Empty));
	}

	[Fact]
	public void Create_WithInvalidString_ThrowsFormatException()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<FormatException>(() => new CorrelationId("not-a-guid"));
	}

	[Fact]
	public void ToString_ReturnsGuidString()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var correlationId = new CorrelationId(guid);

		// Act
		var result = correlationId.ToString();

		// Assert
		result.ShouldBe(guid.ToString());
	}

	[Fact]
	public void Equals_WithSameValue_ReturnsTrue()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var id1 = new CorrelationId(guid);
		var id2 = new CorrelationId(guid);

		// Act & Assert
		id1.Equals(id2).ShouldBeTrue();
		id1.Equals((object)id2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithDifferentValue_ReturnsFalse()
	{
		// Arrange
		var id1 = new CorrelationId(Guid.NewGuid());
		var id2 = new CorrelationId(Guid.NewGuid());

		// Act & Assert
		id1.Equals(id2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var correlationId = new CorrelationId();

		// Act & Assert
		correlationId.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_WithSameValue_ReturnsSameHash()
	{
		// Arrange
		var guid = Guid.NewGuid();
		var id1 = new CorrelationId(guid);
		var id2 = new CorrelationId(guid);

		// Act & Assert
		id1.GetHashCode().ShouldBe(id2.GetHashCode());
	}

	[Fact]
	public void Value_CanBeUpdated()
	{
		// Arrange
		var correlationId = new CorrelationId();
		var newGuid = Guid.NewGuid();

		// Act
		correlationId.Value = newGuid;

		// Assert
		correlationId.Value.ShouldBe(newGuid);
	}
}

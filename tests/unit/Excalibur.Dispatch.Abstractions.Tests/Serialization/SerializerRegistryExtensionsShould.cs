// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for the <see cref="SerializerRegistryExtensions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class SerializerRegistryExtensionsShould
{
	[Fact]
	public void GetByName_Should_ReturnSerializer_WhenRegistered()
	{
		// Arrange
		var registry = A.Fake<ISerializerRegistry>();
		var serializer = A.Fake<IPluggableSerializer>();
		A.CallTo(() => serializer.Name).Returns("Json");
		A.CallTo(() => registry.GetAll()).Returns(
			[(1, "Json", serializer)]);

		// Act
		var result = registry.GetByName("Json");

		// Assert
		result.ShouldBeSameAs(serializer);
	}

	[Fact]
	public void GetByName_Should_ReturnNull_WhenNotRegistered()
	{
		// Arrange
		var registry = A.Fake<ISerializerRegistry>();
		A.CallTo(() => registry.GetAll()).Returns([]);

		// Act
		var result = registry.GetByName("Missing");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetByName_Should_ThrowArgumentNullException_WhenRegistryIsNull()
	{
		// Arrange
		ISerializerRegistry registry = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => registry.GetByName("Json"));
	}

	[Fact]
	public void GetByName_Should_ThrowArgumentException_WhenNameIsNullOrWhitespace()
	{
		// Arrange
		var registry = A.Fake<ISerializerRegistry>();

		// Act & Assert
		Should.Throw<ArgumentException>(() => registry.GetByName(null!));
		Should.Throw<ArgumentException>(() => registry.GetByName("  "));
	}

	[Fact]
	public void IsRegistered_ById_Should_ReturnTrue_WhenExists()
	{
		// Arrange
		var registry = A.Fake<ISerializerRegistry>();
		A.CallTo(() => registry.GetById(1)).Returns(A.Fake<IPluggableSerializer>());

		// Act
		var result = registry.IsRegistered((byte)1);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsRegistered_ById_Should_ReturnFalse_WhenNotExists()
	{
		// Arrange
		var registry = A.Fake<ISerializerRegistry>();
		A.CallTo(() => registry.GetById(99)).Returns(null);

		// Act
		var result = registry.IsRegistered((byte)99);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void IsRegistered_ById_Should_ThrowArgumentNullException_WhenRegistryIsNull()
	{
		// Arrange
		ISerializerRegistry registry = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => registry.IsRegistered((byte)1));
	}

	[Fact]
	public void IsRegistered_ByName_Should_ReturnTrue_WhenExists()
	{
		// Arrange
		var registry = A.Fake<ISerializerRegistry>();
		var serializer = A.Fake<IPluggableSerializer>();
		A.CallTo(() => serializer.Name).Returns("Json");
		A.CallTo(() => registry.GetAll()).Returns(
			[(1, "Json", serializer)]);

		// Act
		var result = registry.IsRegistered("Json");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void IsRegistered_ByName_Should_ReturnFalse_WhenNullOrWhitespace()
	{
		// Arrange
		var registry = A.Fake<ISerializerRegistry>();

		// Act & Assert
		registry.IsRegistered((string)null!).ShouldBeFalse();
		registry.IsRegistered("  ").ShouldBeFalse();
	}

	[Fact]
	public void IsRegistered_ByName_Should_ThrowArgumentNullException_WhenRegistryIsNull()
	{
		// Arrange
		ISerializerRegistry registry = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => registry.IsRegistered("Json"));
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Serialization.Tests;

/// <summary>
/// Unit tests for <see cref="SerializationBuilderExtensions" />.
/// </summary>
[Trait(TraitNames.Component, TestComponents.Serialization)]
[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class SerializationBuilderExtensionsShould
{
	private readonly ISerializationBuilder _builder = A.Fake<ISerializationBuilder>();

	public SerializationBuilderExtensionsShould()
	{
		// Make all builder methods return the same builder for chaining
		A.CallTo(() => _builder.Register(A<ISerializer>._, A<byte>._)).Returns(_builder);
		A.CallTo(() => _builder.UseCurrent(A<string>._)).Returns(_builder);
	}

	#region RegisterSystemTextJson

	[Fact]
	public void RegisterSystemTextJson_DelegatesToRegisterWithCorrectId()
	{
		// Act
		_ = _builder.RegisterSystemTextJson();

		// Assert
		A.CallTo(() => _builder.Register(
			A<ISerializer>.That.IsInstanceOf(typeof(SystemTextJsonSerializer)),
			SerializerIds.SystemTextJson))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void RegisterSystemTextJson_ReturnsBuildForChaining()
	{
		// Act
		var result = _builder.RegisterSystemTextJson();

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void RegisterSystemTextJson_ThrowsOnNullBuilder()
	{
		// Arrange
		ISerializationBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.RegisterSystemTextJson());
	}

	#endregion

	#region UseMemoryPack

	[Fact]
	public void UseMemoryPack_DelegatesToUseCurrentWithCorrectName()
	{
		// Act
		_ = _builder.UseMemoryPack();

		// Assert
		A.CallTo(() => _builder.UseCurrent("MemoryPack"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UseMemoryPack_ReturnsBuilderForChaining()
	{
		// Act
		var result = _builder.UseMemoryPack();

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void UseMemoryPack_ThrowsOnNullBuilder()
	{
		// Arrange
		ISerializationBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseMemoryPack());
	}

	#endregion

	#region UseSystemTextJson

	[Fact]
	public void UseSystemTextJson_DelegatesToUseCurrentWithCorrectName()
	{
		// Act
		_ = _builder.UseSystemTextJson();

		// Assert
		A.CallTo(() => _builder.UseCurrent("System.Text.Json"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UseSystemTextJson_ReturnsBuilderForChaining()
	{
		// Act
		var result = _builder.UseSystemTextJson();

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void UseSystemTextJson_ThrowsOnNullBuilder()
	{
		// Arrange
		ISerializationBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseSystemTextJson());
	}

	#endregion

	#region UseMessagePack

	[Fact]
	public void UseMessagePack_DelegatesToUseCurrentWithCorrectName()
	{
		// Act
		_ = _builder.UseMessagePack();

		// Assert
		A.CallTo(() => _builder.UseCurrent("MessagePack"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UseMessagePack_ReturnsBuilderForChaining()
	{
		// Act
		var result = _builder.UseMessagePack();

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void UseMessagePack_ThrowsOnNullBuilder()
	{
		// Arrange
		ISerializationBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseMessagePack());
	}

	#endregion

	#region UseProtobuf

	[Fact]
	public void UseProtobuf_DelegatesToUseCurrentWithCorrectName()
	{
		// Act
		_ = _builder.UseProtobuf();

		// Assert
		A.CallTo(() => _builder.UseCurrent("Protobuf"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UseProtobuf_ReturnsBuilderForChaining()
	{
		// Act
		var result = _builder.UseProtobuf();

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void UseProtobuf_ThrowsOnNullBuilder()
	{
		// Arrange
		ISerializationBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseProtobuf());
	}

	#endregion

	#region UseAvro

	[Fact]
	public void UseAvro_DelegatesToUseCurrentWithCorrectName()
	{
		// Act
		_ = _builder.UseAvro();

		// Assert
		A.CallTo(() => _builder.UseCurrent("Avro"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void UseAvro_ReturnsBuilderForChaining()
	{
		// Act
		var result = _builder.UseAvro();

		// Assert
		result.ShouldBeSameAs(_builder);
	}

	[Fact]
	public void UseAvro_ThrowsOnNullBuilder()
	{
		// Arrange
		ISerializationBuilder builder = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.UseAvro());
	}

	#endregion
}

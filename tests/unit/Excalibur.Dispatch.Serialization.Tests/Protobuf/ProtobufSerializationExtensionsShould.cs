// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.Protobuf;

namespace Excalibur.Dispatch.Serialization.Tests.Protobuf;

/// <summary>
/// Unit tests for <see cref="ProtobufSerializationExtensions" />.
/// </summary>
[Trait(TraitNames.Component, TestComponents.Serialization)]
[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class ProtobufSerializationExtensionsShould
{
	#region AddProtobufSerializer (no-arg overload)

	[Fact]
	public void AddProtobufSerializer_RegistersISerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddProtobufSerializer();

		// Assert
		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<ISerializer>();
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<ProtobufSerializer>();
	}

	[Fact]
	public void AddProtobufSerializer_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddProtobufSerializer());
	}

	[Fact]
	public void AddProtobufSerializer_ReturnsServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddProtobufSerializer();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddProtobufSerializer_UsesTryAdd_DoesNotOverrideExisting()
	{
		// Arrange - register a fake serializer first
		var services = new ServiceCollection();
		var fakeSerializer = A.Fake<ISerializer>();
		services.AddSingleton(fakeSerializer);

		// Act - Protobuf should NOT override the existing registration (TryAdd)
		_ = services.AddProtobufSerializer();

		// Assert
		using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<ISerializer>();
		resolved.ShouldBeSameAs(fakeSerializer);
	}

	[Fact]
	public void AddProtobufSerializer_RegistersPluggableSerializationOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddProtobufSerializer();

		// Assert
		using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<PluggableSerializationOptions>>();
		_ = options.ShouldNotBeNull();
		var value = options.Value;
		value.CurrentSerializerName.ShouldBe("Protobuf");
	}

	#endregion

	#region AddProtobufSerializer (configure overload)

	[Fact]
	public void AddProtobufSerializer_WithConfigure_RegistersISerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddProtobufSerializer(opts => opts.WireFormat = ProtobufWireFormat.Json);

		// Assert
		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<ISerializer>();
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<ProtobufSerializer>();
	}

	[Fact]
	public void AddProtobufSerializer_WithConfigure_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => services.AddProtobufSerializer(_ => { }));
	}

	[Fact]
	public void AddProtobufSerializer_WithConfigure_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => services.AddProtobufSerializer((Action<ProtobufSerializationOptions>)null!));
	}

	[Fact]
	public void AddProtobufSerializer_WithConfigure_ReturnsServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddProtobufSerializer(_ => { });

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddProtobufSerializer_WithConfigure_AppliesOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddProtobufSerializer(opts => opts.WireFormat = ProtobufWireFormat.Json);

		// Assert
		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<ISerializer>();
		serializer.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void AddProtobufSerializer_WithConfigure_UsesTryAdd_DoesNotOverrideExisting()
	{
		// Arrange - register a fake serializer first
		var services = new ServiceCollection();
		var fakeSerializer = A.Fake<ISerializer>();
		services.AddSingleton(fakeSerializer);

		// Act
		_ = services.AddProtobufSerializer(_ => { });

		// Assert
		using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<ISerializer>();
		resolved.ShouldBeSameAs(fakeSerializer);
	}

	[Fact]
	public void AddProtobufSerializer_WithConfigure_RegistersPluggableSerializationOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddProtobufSerializer(_ => { });

		// Assert
		using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<PluggableSerializationOptions>>();
		_ = options.ShouldNotBeNull();
		var value = options.Value;
		value.CurrentSerializerName.ShouldBe("Protobuf");
	}

	#endregion
}

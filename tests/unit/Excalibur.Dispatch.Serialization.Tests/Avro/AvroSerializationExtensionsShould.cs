// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.Avro;

namespace Excalibur.Dispatch.Serialization.Tests.Avro;

/// <summary>
/// Unit tests for <see cref="AvroSerializationExtensions" />.
/// </summary>
[Trait(TraitNames.Component, TestComponents.Serialization)]
[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class AvroSerializationExtensionsShould
{
	#region AddAvroSerializer (no-arg overload)

	[Fact]
	public void AddAvroSerializer_RegistersISerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAvroSerializer();

		// Assert
		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<ISerializer>();
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<AvroSerializer>();
	}

	[Fact]
	public void AddAvroSerializer_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddAvroSerializer());
	}

	[Fact]
	public void AddAvroSerializer_ReturnsServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAvroSerializer();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAvroSerializer_UsesTryAdd_DoesNotOverrideExisting()
	{
		// Arrange - register a fake serializer first
		var services = new ServiceCollection();
		var fakeSerializer = A.Fake<ISerializer>();
		services.AddSingleton(fakeSerializer);

		// Act - Avro should NOT override the existing registration (TryAdd)
		_ = services.AddAvroSerializer();

		// Assert
		using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<ISerializer>();
		resolved.ShouldBeSameAs(fakeSerializer);
	}

	[Fact]
	public void AddAvroSerializer_RegistersPluggableSerializationOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAvroSerializer();

		// Assert
		using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<PluggableSerializationOptions>>();
		_ = options.ShouldNotBeNull();
		var value = options.Value;
		value.CurrentSerializerName.ShouldBe("Avro");
	}

	#endregion

	#region AddAvroSerializer (configure overload)

	[Fact]
	public void AddAvroSerializer_WithConfigure_RegistersISerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAvroSerializer(_ => { });

		// Assert
		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<ISerializer>();
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<AvroSerializer>();
	}

	[Fact]
	public void AddAvroSerializer_WithConfigure_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => services.AddAvroSerializer(_ => { }));
	}

	[Fact]
	public void AddAvroSerializer_WithConfigure_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(
			() => services.AddAvroSerializer((Action<AvroSerializationOptions>)null!));
	}

	[Fact]
	public void AddAvroSerializer_WithConfigure_ReturnsServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAvroSerializer(_ => { });

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddAvroSerializer_WithConfigure_UsesTryAdd_DoesNotOverrideExisting()
	{
		// Arrange - register a fake serializer first
		var services = new ServiceCollection();
		var fakeSerializer = A.Fake<ISerializer>();
		services.AddSingleton(fakeSerializer);

		// Act
		_ = services.AddAvroSerializer(_ => { });

		// Assert
		using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<ISerializer>();
		resolved.ShouldBeSameAs(fakeSerializer);
	}

	[Fact]
	public void AddAvroSerializer_WithConfigure_RegistersPluggableSerializationOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAvroSerializer(_ => { });

		// Assert
		using var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<PluggableSerializationOptions>>();
		_ = options.ShouldNotBeNull();
		var value = options.Value;
		value.CurrentSerializerName.ShouldBe("Avro");
	}

	#endregion
}

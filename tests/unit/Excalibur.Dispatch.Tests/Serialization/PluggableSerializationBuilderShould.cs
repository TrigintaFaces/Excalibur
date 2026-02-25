// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="IPluggableSerializationBuilder"/> and
/// <see cref="PluggableSerializationServiceCollectionExtensions"/> validating
/// DI configuration and auto-registration behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PluggableSerializationBuilderShould
{
	#region AutoRegisterMemoryPack Tests

	[Fact]
	public void AutoRegisterMemoryPack_ByDefault()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();

		// Act
		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISerializerRegistry>();

		// Assert - MemoryPack should be auto-registered
		registry.IsRegistered(SerializerIds.MemoryPack).ShouldBeTrue();
	}

	[Fact]
	public void SetMemoryPackAsCurrent_ByDefault()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();

		// Act
		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISerializerRegistry>();

		// Assert - MemoryPack should be the current serializer
		var (id, serializer) = registry.GetCurrent();
		_ = serializer.ShouldNotBeNull();
		serializer.Name.ShouldBe("MemoryPack");
		id.ShouldBe(SerializerIds.MemoryPack);
	}

	[Fact]
	public void DisableMemoryPackAutoRegistration_WhenOptionIsSetToFalse()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();
		_ = services.Configure<PluggableSerializationOptions>(options =>
		{
			options.AutoRegisterMemoryPack = false;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISerializerRegistry>();

		// Assert - MemoryPack should NOT be registered
		registry.IsRegistered(SerializerIds.MemoryPack).ShouldBeFalse();

		// GetCurrent should throw since nothing is registered
		_ = Should.Throw<InvalidOperationException>(() => registry.GetCurrent());
	}

	[Fact]
	public void AllowOnlySystemTextJson_WhenMemoryPackDisabled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();

		// Add SystemTextJson and disable MemoryPack auto-registration
		_ = services.AddPluggableSerializer(
			SerializerIds.SystemTextJson,
			new SystemTextJsonPluggableSerializer(),
			setAsCurrent: true);

		_ = services.Configure<PluggableSerializationOptions>(options =>
		{
			options.AutoRegisterMemoryPack = false;
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISerializerRegistry>();

		// Assert
		registry.IsRegistered(SerializerIds.MemoryPack).ShouldBeFalse();
		registry.IsRegistered(SerializerIds.SystemTextJson).ShouldBeTrue();

		var (id, serializer) = registry.GetCurrent();
		serializer.Name.ShouldBe("System.Text.Json");
	}

	#endregion AutoRegisterMemoryPack Tests

	#region Serializer Registration Tests

	[Fact]
	public void RegisterMultipleSerializers()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();
		_ = services.AddPluggableSerializer(SerializerIds.SystemTextJson, new SystemTextJsonPluggableSerializer());

		// Act
		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISerializerRegistry>();

		// Assert - Both should be registered
		registry.IsRegistered(SerializerIds.MemoryPack).ShouldBeTrue();
		registry.IsRegistered(SerializerIds.SystemTextJson).ShouldBeTrue();
	}

	[Fact]
	public void SwitchCurrentSerializer_ViaAddPluggableSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();
		_ = services.AddPluggableSerializer(
			SerializerIds.SystemTextJson,
			new SystemTextJsonPluggableSerializer(),
			setAsCurrent: true);

		// Act
		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISerializerRegistry>();

		// Assert
		var (id, serializer) = registry.GetCurrent();
		serializer.Name.ShouldBe("System.Text.Json");
		id.ShouldBe(SerializerIds.SystemTextJson);
	}

	#endregion Serializer Registration Tests

	#region IHttpSerializer Registration Tests

	[Fact]
	public void RegisterHttpSerializer_Automatically()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();

		// Act
		using var provider = services.BuildServiceProvider();
		var httpSerializer = provider.GetService<IHttpSerializer>();

		// Assert
		_ = httpSerializer.ShouldNotBeNull();
		_ = httpSerializer.ShouldBeOfType<HttpJsonSerializer>();
	}

	#endregion IHttpSerializer Registration Tests

	#region Options Tests

	[Fact]
	public void Options_DefaultsAutoRegisterToTrue()
	{
		// Arrange & Act
		var options = new PluggableSerializationOptions();

		// Assert
		options.AutoRegisterMemoryPack.ShouldBeTrue();
	}

	[Fact]
	public void Options_CurrentSerializerName_DefaultsToNull()
	{
		// Arrange & Act
		var options = new PluggableSerializationOptions();

		// Assert
		options.CurrentSerializerName.ShouldBeNull();
	}

	#endregion Options Tests
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

using MpkSerializer = Excalibur.Dispatch.Serialization.MessagePack.MessagePackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Integration tests for MessagePack DI registration and resolution.
/// Tests the complete dependency injection pipeline.
/// Updated for consolidated serializer: AddMessagePackSerialization now registers
/// ISerializer (not the old IMessageSerializer which has been deleted).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class MessagePackDIIntegrationShould : UnitTestBase
{
	#region Full DI Pipeline Tests

	[Fact]
	public void AddMessagePackSerialization_ResolvesWorkingSerializers()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerializer();
		var provider = services.BuildServiceProvider();

		// Act
		var serializer = provider.GetRequiredService<ISerializer>();
		var pluggable = provider.GetRequiredService<ISerializer>();

		// Assert - serializers should be functional
		var msg = new TestMessage { Id = 1, Name = "DI" };
		var bytes = serializer.SerializeToBytes(msg);
		var result = serializer.Deserialize<TestMessage>(bytes);
		result.Id.ShouldBe(1);

		pluggable.ShouldNotBeNull();
	}

	[Fact]
	public void AddMessagePackSerialization_WithOptions_AppliesOptionsToSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		var customOptions = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.Lz4Block);
		services.AddMessagePackSerializer(customOptions);
		var provider = services.BuildServiceProvider();

		// Act
		var serializer = provider.GetRequiredService<ISerializer>();

		// Assert - serializer should resolve successfully with custom options
		serializer.ShouldNotBeNull();
		serializer.ShouldBeOfType<MpkSerializer>();
	}

	[Fact]
	public void AddMessagePackSerialization_ResolvesConsolidatedSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerializer();
		var provider = services.BuildServiceProvider();

		// Act
		var serializer = provider.GetRequiredService<ISerializer>();

		// Assert
		serializer.ShouldBeOfType<MpkSerializer>();
		var msg = new TestMessage { Id = 99, Name = "Consolidated" };
		var bytes = serializer.SerializeToBytes(msg);
		var result = serializer.Deserialize<TestMessage>(bytes);
		result.Id.ShouldBe(99);
	}

	#endregion

	#region Singleton Behavior Tests

	[Fact]
	public void AddMessagePackSerialization_ISerializerIsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerializer();
		var provider = services.BuildServiceProvider();

		// Act
		var serializer1 = provider.GetRequiredService<ISerializer>();
		var serializer2 = provider.GetRequiredService<ISerializer>();

		// Assert - should be same singleton instance
		ReferenceEquals(serializer1, serializer2).ShouldBeTrue();
	}

	#endregion

	#region Pluggable Serialization DI Tests

	[Fact]
	public void AddMessagePackSerializer_RegistersSerializerWithPluggableSerialization()
	{
		// Arrange - AddPluggableSerialization registers ISerializerRegistry,
		// AddMessagePackSerializer registers ISerializer + PostConfigure
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();
		services.AddMessagePackSerializer();
		using var provider = services.BuildServiceProvider();

		// Act
		var registry = provider.GetService<ISerializerRegistry>();

		// Assert
		registry.ShouldNotBeNull();
	}

	[Fact]
	public void AddMessagePackSerializer_WithOptions_RegistersSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		var customOptions = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.Lz4Block);
		services.AddMessagePackSerializer(customOptions);
		using var provider = services.BuildServiceProvider();

		// Act
		var serializer = provider.GetService<ISerializer>();

		// Assert
		serializer.ShouldNotBeNull();
		serializer.ShouldBeOfType<MpkSerializer>();
	}

	[Fact]
	public void AddMessagePackSerializer_RegistersISerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerializer();
		var provider = services.BuildServiceProvider();

		// Act
		var serializer = provider.GetService<ISerializer>();

		// Assert
		serializer.ShouldNotBeNull();
		serializer.ShouldBeOfType<MpkSerializer>();
	}

	#endregion

	#region Scoped Resolution Tests

	[Fact]
	public void AddMessagePackSerialization_WorksWithScopedProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerializer();
		var provider = services.BuildServiceProvider();

		// Act - create scope and resolve
		using var scope = provider.CreateScope();
		var serializer = scope.ServiceProvider.GetRequiredService<ISerializer>();

		// Assert
		serializer.ShouldNotBeNull();
		var msg = new TestMessage { Id = 5, Name = "Scoped" };
		var bytes = serializer.SerializeToBytes(msg);
		var result = serializer.Deserialize<TestMessage>(bytes);
		result.Id.ShouldBe(5);
	}

	[Fact]
	public void AddMessagePackSerialization_SameInstanceAcrossScopes()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerializer();
		var provider = services.BuildServiceProvider();

		// Act
		ISerializer serializer1, serializer2;
		using (var scope1 = provider.CreateScope())
		{
			serializer1 = scope1.ServiceProvider.GetRequiredService<ISerializer>();
		}
		using (var scope2 = provider.CreateScope())
		{
			serializer2 = scope2.ServiceProvider.GetRequiredService<ISerializer>();
		}

		// Assert - singleton so same instance
		ReferenceEquals(serializer1, serializer2).ShouldBeTrue();
	}

	#endregion
}

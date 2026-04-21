// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MemoryPack;

using Microsoft.Extensions.DependencyInjection;

using MpSerializer = Excalibur.Dispatch.Serialization.MemoryPack.MemoryPackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MemoryPack;

/// <summary>
/// Unit tests for <see cref="MemoryPackSerializationServiceCollectionExtensions" />.
/// </summary>
[Trait(TraitNames.Component, TestComponents.Serialization)]
[Trait(TraitNames.Category, TestCategories.Unit)]
public sealed class MemoryPackSerializationServiceCollectionExtensionsShould
{
	#region AddMemoryPackSerializer Tests

	[Fact]
	public void AddMemoryPackSerializer_RegistersISerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMemoryPackSerializer();

		// Assert
		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<ISerializer>();
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<MpSerializer>();
	}

	[Fact]
	public void AddMemoryPackSerializer_RegistersIBinaryEnvelopeDeserializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMemoryPackSerializer();

		// Assert
		using var provider = services.BuildServiceProvider();
		var deserializer = provider.GetService<IBinaryEnvelopeDeserializer>();
		_ = deserializer.ShouldNotBeNull();
	}

	[Fact]
	public void AddMemoryPackSerializer_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddMemoryPackSerializer());
	}

	[Fact]
	public void AddMemoryPackSerializer_ReturnsServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddMemoryPackSerializer();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion AddMemoryPackSerializer Tests

	#region Serializer Functionality Tests

	[Fact]
	public void RegisteredSerializer_CanSerializeAndDeserialize_ViaServiceProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMemoryPackSerializer();
		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<ISerializer>();
		var original = new TestPayload { Id = 999, Name = "Integration Test" };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestPayload>(bytes.AsSpan());

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void AddMemoryPackSerializer_UseTryAdd_DoesNotOverrideExisting()
	{
		// Arrange - register a fake serializer first
		var services = new ServiceCollection();
		var fakeSerializer = A.Fake<ISerializer>();
		services.AddSingleton(fakeSerializer);

		// Act - MemoryPack should NOT override the existing registration (TryAdd)
		_ = services.AddMemoryPackSerializer();

		// Assert
		using var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<ISerializer>();
		resolved.ShouldBeSameAs(fakeSerializer);
	}

	#endregion Serializer Functionality Tests
}

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
[Trait("Component", "Serialization")]
[Trait("Category", "DependencyInjection")]
public sealed class MemoryPackSerializationServiceCollectionExtensionsShould
{
	#region GetPluggableSerializer Tests

	[Fact]
	public void GetPluggableSerializer_ReturnsMemoryPackPluggableSerializer()
	{
		// Act
		var serializer = MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer();

		// Assert
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<MpSerializer>();
	}

	[Fact]
	public void GetPluggableSerializer_ReturnsNewInstanceEachTime()
	{
		// Act
		var serializer1 = MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer();
		var serializer2 = MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer();

		// Assert - Should be different instances
		ReferenceEquals(serializer1, serializer2).ShouldBeFalse();
	}

	[Fact]
	public void GetPluggableSerializer_ReturnsWorkingSerializer()
	{
		// Arrange - Cast to ISerializer to use SerializeToBytes extension (avoids trimming warnings)
		var pluggable = MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer();
		var serializer = (ISerializer)pluggable;
		var value = new TestPayload { Id = 42, Name = "Test" };

		// Act
		var bytes = serializer.SerializeToBytes(value);
		var result = serializer.Deserialize<TestPayload>(bytes);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(42);
		result.Name.ShouldBe("Test");
	}

	[Fact]
	public void GetPluggableSerializer_ImplementsISerializer()
	{
		// Act
		var serializer = MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer();

		// Assert
		_ = serializer.ShouldBeAssignableTo<ISerializer>();
	}

	#endregion GetPluggableSerializer Tests

	#region DI Registration Tests

	[Fact]
	public void RegisteredSerializer_CanSerializeAndDeserialize_ViaServiceProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton<ISerializer>(MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer());
		var provider = services.BuildServiceProvider();
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
	public void RegisteredSerializer_ResolvesAsMemoryPackSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton<ISerializer>(MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer());

		// Assert
		var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<ISerializer>();
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<MpSerializer>();
	}

	#endregion DI Registration Tests
}

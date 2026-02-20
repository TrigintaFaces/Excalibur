// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.DependencyInjection;

using Excalibur.Dispatch.Serialization.MemoryPack;

namespace Excalibur.Dispatch.Serialization.Tests.MemoryPack;

/// <summary>
/// Unit tests for <see cref="MemoryPackSerializationServiceCollectionExtensions" />.
/// </summary>
[Trait("Component", "Serialization")]
[Trait("Category", "DependencyInjection")]
public sealed class MemoryPackSerializationServiceCollectionExtensionsShould
{
	#region AddMemoryPackInternalSerialization Tests

	[Fact]
	public void AddMemoryPackInternalSerialization_RegistersSerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMemoryPackInternalSerialization();

		// Assert
		var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<IInternalSerializer>();
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<MemoryPackInternalSerializer>();
	}

	[Fact]
	public void AddMemoryPackInternalSerialization_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddMemoryPackInternalSerialization();

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddMemoryPackInternalSerialization_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddMemoryPackInternalSerialization());
	}

	[Fact]
	public void AddMemoryPackInternalSerialization_DoesNotReplaceExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var customSerializer = new CustomInternalSerializer();
		_ = services.AddSingleton<IInternalSerializer>(customSerializer);

		// Act
		_ = services.AddMemoryPackInternalSerialization();

		// Assert - Original registration should remain
		var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<IInternalSerializer>();
		serializer.ShouldBe(customSerializer);
	}

	[Fact]
	public void AddMemoryPackInternalSerialization_CanBeCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMemoryPackInternalSerialization();
		_ = services.AddMemoryPackInternalSerialization();
		_ = services.AddMemoryPackInternalSerialization();

		// Assert - Should only have one registration
		var provider = services.BuildServiceProvider();
		var serializers = provider.GetServices<IInternalSerializer>().ToList();
		serializers.Count.ShouldBe(1);
	}

	#endregion AddMemoryPackInternalSerialization Tests

	#region AddInternalSerialization<T> Tests

	[Fact]
	public void AddInternalSerialization_Generic_RegistersCustomSerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddInternalSerialization<CustomInternalSerializer>();

		// Assert
		var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<IInternalSerializer>();
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<CustomInternalSerializer>();
	}

	[Fact]
	public void AddInternalSerialization_Generic_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddInternalSerialization<CustomInternalSerializer>();

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddInternalSerialization_Generic_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddInternalSerialization<CustomInternalSerializer>());
	}

	[Fact]
	public void AddInternalSerialization_Generic_ReplacesExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMemoryPackInternalSerialization();

		// Act
		_ = services.AddInternalSerialization<CustomInternalSerializer>();

		// Assert - Should have both registrations but resolve to custom
		var provider = services.BuildServiceProvider();
		var serializers = provider.GetServices<IInternalSerializer>().ToList();
		serializers.Count.ShouldBe(2);

		// GetService returns the last registered
		var serializer = provider.GetService<IInternalSerializer>();
		_ = serializer.ShouldBeOfType<CustomInternalSerializer>();
	}

	#endregion AddInternalSerialization<T> Tests

	#region AddInternalSerialization Instance Tests

	[Fact]
	public void AddInternalSerialization_Instance_RegistersSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		var customSerializer = new CustomInternalSerializer();

		// Act
		_ = services.AddInternalSerialization(customSerializer);

		// Assert
		var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<IInternalSerializer>();
		serializer.ShouldBe(customSerializer);
	}

	[Fact]
	public void AddInternalSerialization_Instance_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var customSerializer = new CustomInternalSerializer();

		// Act
		var result = services.AddInternalSerialization(customSerializer);

		// Assert
		result.ShouldBe(services);
	}

	[Fact]
	public void AddInternalSerialization_Instance_WithNullServices_ThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection services = null!;
		var customSerializer = new CustomInternalSerializer();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddInternalSerialization(customSerializer));
	}

	[Fact]
	public void AddInternalSerialization_Instance_WithNullSerializer_ThrowsArgumentNullException()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddInternalSerialization(null!));
	}

	#endregion AddInternalSerialization Instance Tests

	#region GetPluggableSerializer Tests

	[Fact]
	public void GetPluggableSerializer_ReturnsMemoryPackPluggableSerializer()
	{
		// Act
		var serializer = MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer();

		// Assert
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<MemoryPackPluggableSerializer>();
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
		// Arrange
		var serializer = MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer();
		var value = new TestPayload { Id = 42, Name = "Test" };

		// Act
		var bytes = serializer.Serialize(value);
		var result = serializer.Deserialize<TestPayload>(bytes);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(42);
		result.Name.ShouldBe("Test");
	}

	[Fact]
	public void GetPluggableSerializer_ImplementsIPluggableSerializer()
	{
		// Act
		var serializer = MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer();

		// Assert
		_ = serializer.ShouldBeAssignableTo<IPluggableSerializer>();
	}

	#endregion GetPluggableSerializer Tests

	#region Integration Tests

	[Fact]
	public void Serializer_CanSerializeAndDeserialize_ViaServiceProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMemoryPackInternalSerialization();
		var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<IInternalSerializer>();
		var original = new TestPayload { Id = 999, Name = "Integration Test" };

		// Act
		var bytes = serializer.Serialize(original);
		var result = serializer.Deserialize<TestPayload>(bytes.AsSpan());

		// Assert
		_ = result.ShouldNotBeNull();
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	#endregion Integration Tests

	#region Test Helpers

	/// <summary>
	/// Custom internal serializer for testing registration behavior.
	/// </summary>
	private sealed class CustomInternalSerializer : IInternalSerializer
	{
		public byte[] Serialize<T>(T value) => [];

		public void Serialize<T>(T value, System.Buffers.IBufferWriter<byte> bufferWriter) { }

		public T Deserialize<T>(System.Buffers.ReadOnlySequence<byte> buffer) => default!;

		public T Deserialize<T>(ReadOnlySpan<byte> buffer) => default!;
	}

	#endregion Test Helpers
}

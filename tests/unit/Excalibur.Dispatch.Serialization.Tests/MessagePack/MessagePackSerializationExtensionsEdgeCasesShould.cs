// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Additional edge-case and coverage tests for <see cref="MessagePackSerializationExtensions"/>.
/// Targets: DI resolution paths, generic overload with AotMessagePackSerializer,
/// pluggable serialization registration, and configuration delegate branches.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackSerializationExtensionsEdgeCasesShould : UnitTestBase
{
	#region AddMessagePackSerialization - DI Resolution

	[Fact]
	public void AddMessagePackSerialization_TryAddSingleton_DoesNotOverrideExisting()
	{
		// Arrange - register a custom IMessageSerializer first
		var services = new ServiceCollection();
		services.AddSingleton<IMessageSerializer, AotMessagePackSerializer>();

		// Act - this should NOT override existing registration (TryAdd)
		services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Assert - original registration should be preserved
		var serializer = provider.GetRequiredService<IMessageSerializer>();
		serializer.ShouldBeOfType<AotMessagePackSerializer>();
	}

	[Fact]
	public void AddMessagePackSerialization_TryAddSingleton_DoesNotOverrideExistingZeroCopy()
	{
		// Arrange - register a custom IZeroCopySerializer first
		var services = new ServiceCollection();
		services.AddSingleton<IZeroCopySerializer, MessagePackZeroCopySerializer>();

		// Act - this should NOT override existing registration (TryAdd)
		services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Assert - only one registration should exist
		var serializers = provider.GetServices<IZeroCopySerializer>().ToList();
		serializers.Count.ShouldBe(1);
	}

	[Fact]
	public void AddMessagePackSerialization_CalledTwice_DoesNotDuplicate()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMessagePackSerialization();
		services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Assert
		var serializer = provider.GetRequiredService<IMessageSerializer>();
		serializer.ShouldNotBeNull();
	}

	[Fact]
	public void AddMessagePackSerialization_WithConfigureDelegate_ConfiguresAllOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMessagePackSerialization(opts =>
		{
			opts.UseLz4Compression = true;
		});
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MessagePackSerializationOptions>>().Value;

		// Assert
		options.UseLz4Compression.ShouldBeTrue();
	}

	#endregion

	#region AddMessagePackSerialization<T> - Generic Overload

	[Fact]
	public void AddMessagePackSerialization_Generic_WithAotSerializer_RegistersCorrectType()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMessagePackSerialization<AotMessagePackSerializer>();
		var provider = services.BuildServiceProvider();

		// Assert
		var serializer = provider.GetRequiredService<IMessageSerializer>();
		serializer.ShouldBeOfType<AotMessagePackSerializer>();
	}

	[Fact]
	public void AddMessagePackSerialization_Generic_CalledTwice_DoesNotDuplicate()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMessagePackSerialization<DispatchMessagePackSerializer>();
		services.AddMessagePackSerialization<AotMessagePackSerializer>(); // TryAdd - should not replace

		var provider = services.BuildServiceProvider();

		// Assert - first registration wins (TryAdd semantics)
		var serializer = provider.GetRequiredService<IMessageSerializer>();
		serializer.ShouldBeOfType<DispatchMessagePackSerializer>();
	}

	[Fact]
	public void AddMessagePackSerialization_Generic_WithConfigure_AppliesAllOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMessagePackSerialization<DispatchMessagePackSerializer>(opts =>
		{
			opts.UseLz4Compression = true;
		});
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MessagePackSerializationOptions>>().Value;

		// Assert
		options.UseLz4Compression.ShouldBeTrue();
	}

	[Fact]
	public void AddMessagePackSerialization_Generic_ResolvesIZeroCopySerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerialization<DispatchMessagePackSerializer>();
		var provider = services.BuildServiceProvider();

		// Act
		var zeroCopy = provider.GetService<IZeroCopySerializer>();

		// Assert
		zeroCopy.ShouldNotBeNull();
		zeroCopy.ShouldBeOfType<MessagePackZeroCopySerializer>();
	}

	#endregion

	#region GetPluggableSerializer - Additional Paths

	[Fact]
	public void GetPluggableSerializer_WithLz4Options_ReturnsWorkingSerializer()
	{
		// Arrange
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

		// Act
		var serializer = MessagePackSerializationExtensions.GetPluggableSerializer(options);
		var message = new TestPluggableMessage { Value = 42, Text = "Lz4Test" };
		var bytes = serializer.Serialize(message);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(42);
		result.Text.ShouldBe("Lz4Test");
	}

	[Fact]
	public void GetPluggableSerializer_Default_CanSerializeObject()
	{
		// Arrange
		var serializer = MessagePackSerializationExtensions.GetPluggableSerializer();
		var message = new TestPluggableMessage { Value = 11, Text = "ObjTest" };

		// Act
		var bytes = serializer.SerializeObject(message, typeof(TestPluggableMessage));
		var result = (TestPluggableMessage)serializer.DeserializeObject(bytes, typeof(TestPluggableMessage));

		// Assert
		result.Value.ShouldBe(11);
		result.Text.ShouldBe("ObjTest");
	}

	#endregion

	#region AddMessagePackPluggableSerialization - Additional Paths

	[Fact]
	public void AddMessagePackPluggableSerialization_Default_RegistersSerializerRegistry()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddMessagePackPluggableSerialization();
		var provider = services.BuildServiceProvider();

		// Assert
		var registry = provider.GetService<ISerializerRegistry>();
		registry.ShouldNotBeNull();
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithSetAsCurrentTrue_ReturnsServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddMessagePackPluggableSerialization(setAsCurrent: true);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithSetAsCurrentFalse_ReturnsServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddMessagePackPluggableSerialization(setAsCurrent: false);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithCustomOptions_RegistersSerializerRegistry()
	{
		// Arrange
		var services = new ServiceCollection();
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

		// Act
		services.AddMessagePackPluggableSerialization(opts);
		var provider = services.BuildServiceProvider();

		// Assert
		var registry = provider.GetService<ISerializerRegistry>();
		registry.ShouldNotBeNull();
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithCustomOptionsAndSetAsCurrent_RegistersSerializerRegistry()
	{
		// Arrange
		var services = new ServiceCollection();
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

		// Act
		services.AddMessagePackPluggableSerialization(opts, setAsCurrent: true);
		var provider = services.BuildServiceProvider();

		// Assert
		var registry = provider.GetService<ISerializerRegistry>();
		registry.ShouldNotBeNull();
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithCustomOptions_SetAsCurrentFalse_ReturnsServices()
	{
		// Arrange
		var services = new ServiceCollection();
		var opts = MessagePackSerializerOptions.Standard;

		// Act
		var result = services.AddMessagePackPluggableSerialization(opts, setAsCurrent: false);

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion
}

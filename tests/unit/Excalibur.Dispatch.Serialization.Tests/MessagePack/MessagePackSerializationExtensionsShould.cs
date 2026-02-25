// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Unit tests for <see cref="MessagePackSerializationExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MessagePackSerializationExtensionsShould : UnitTestBase
{
	#region AddMessagePackSerialization Tests

	[Fact]
	public void AddMessagePackSerialization_RegistersIZeroCopySerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackSerialization();

		// Assert
		services.Any(static sd =>
			sd.ServiceType == typeof(IZeroCopySerializer) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddMessagePackSerialization_RegistersIMessageSerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackSerialization();

		// Assert
		services.Any(static sd =>
			sd.ServiceType == typeof(IMessageSerializer) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddMessagePackSerialization_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddMessagePackSerialization();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddMessagePackSerialization_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddMessagePackSerialization());
	}

	[Fact]
	public void AddMessagePackSerialization_WithConfigure_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackSerialization(options =>
		{
			options.UseLz4Compression = true;
		});

		// Assert
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<MessagePackSerializationOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddMessagePackSerialization_WithNullConfigure_DoesNotRegisterConfigureOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackSerialization(configure: null);

		// Assert
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<MessagePackSerializationOptions>)).ShouldBeFalse();
	}

	[Fact]
	public void AddMessagePackSerialization_WithConfigure_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackSerialization(options =>
		{
			options.UseLz4Compression = true;
		});
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MessagePackSerializationOptions>>().Value;

		// Assert
		options.UseLz4Compression.ShouldBeTrue();
	}

	[Fact]
	public void AddMessagePackSerialization_ResolvesIZeroCopySerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Act
		var serializer = provider.GetService<IZeroCopySerializer>();

		// Assert
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<MessagePackZeroCopySerializer>();
	}

	[Fact]
	public void AddMessagePackSerialization_ResolvesIMessageSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Act
		var serializer = provider.GetService<IMessageSerializer>();

		// Assert
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<DispatchMessagePackSerializer>();
	}

	#endregion AddMessagePackSerialization Tests

	#region AddMessagePackSerialization Generic Tests

	[Fact]
	public void AddMessagePackSerialization_Generic_RegistersCustomSerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackSerialization<DispatchMessagePackSerializer>();

		// Assert
		services.Any(static sd =>
			sd.ServiceType == typeof(IMessageSerializer) &&
			sd.ImplementationType == typeof(DispatchMessagePackSerializer) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddMessagePackSerialization_Generic_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddMessagePackSerialization<DispatchMessagePackSerializer>());
	}

	[Fact]
	public void AddMessagePackSerialization_Generic_RegistersIZeroCopySerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackSerialization<DispatchMessagePackSerializer>();

		// Assert
		services.Any(static sd =>
			sd.ServiceType == typeof(IZeroCopySerializer) &&
			sd.Lifetime == ServiceLifetime.Singleton).ShouldBeTrue();
	}

	[Fact]
	public void AddMessagePackSerialization_Generic_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddMessagePackSerialization<DispatchMessagePackSerializer>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddMessagePackSerialization_Generic_WithConfigure_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackSerialization<DispatchMessagePackSerializer>(options =>
		{
			options.UseLz4Compression = true;
		});

		// Assert
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<MessagePackSerializationOptions>)).ShouldBeTrue();
	}

	[Fact]
	public void AddMessagePackSerialization_Generic_WithNullConfigure_DoesNotRegisterConfigureOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackSerialization<DispatchMessagePackSerializer>(configure: null);

		// Assert
		services.Any(static sd =>
			sd.ServiceType == typeof(IConfigureOptions<MessagePackSerializationOptions>)).ShouldBeFalse();
	}

	[Fact]
	public void AddMessagePackSerialization_Generic_WithConfigure_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackSerialization<DispatchMessagePackSerializer>(options =>
		{
			options.UseLz4Compression = true;
		});
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<MessagePackSerializationOptions>>().Value;

		// Assert
		options.UseLz4Compression.ShouldBeTrue();
	}

	#endregion AddMessagePackSerialization Generic Tests

	#region GetPluggableSerializer Tests

	[Fact]
	public void GetPluggableSerializer_ReturnsMessagePackPluggableSerializer()
	{
		// Act
		var serializer = MessagePackSerializationExtensions.GetPluggableSerializer();

		// Assert
		_ = serializer.ShouldBeOfType<MessagePackPluggableSerializer>();
		serializer.Name.ShouldBe("MessagePack");
	}

	[Fact]
	public void GetPluggableSerializer_WithOptions_ReturnsConfiguredSerializer()
	{
		// Arrange
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

		// Act
		var serializer = MessagePackSerializationExtensions.GetPluggableSerializer(options);

		// Assert
		_ = serializer.ShouldBeOfType<MessagePackPluggableSerializer>();
	}

	[Fact]
	public void GetPluggableSerializer_WithNullOptions_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			MessagePackSerializationExtensions.GetPluggableSerializer(null!));
	}

	[Fact]
	public void GetPluggableSerializer_ReturnsNewInstanceEachTime()
	{
		// Act
		var serializer1 = MessagePackSerializationExtensions.GetPluggableSerializer();
		var serializer2 = MessagePackSerializationExtensions.GetPluggableSerializer();

		// Assert
		ReferenceEquals(serializer1, serializer2).ShouldBeFalse();
	}

	[Fact]
	public void GetPluggableSerializer_ReturnsWorkingSerializer()
	{
		// Arrange
		var serializer = MessagePackSerializationExtensions.GetPluggableSerializer();
		var value = new TestPluggableMessage { Value = 42, Text = "Test" };

		// Act
		var bytes = serializer.Serialize(value);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Value.ShouldBe(42);
		result.Text.ShouldBe("Test");
	}

	[Fact]
	public void GetPluggableSerializer_ImplementsIPluggableSerializer()
	{
		// Act
		var serializer = MessagePackSerializationExtensions.GetPluggableSerializer();

		// Assert
		_ = serializer.ShouldBeAssignableTo<IPluggableSerializer>();
	}

	#endregion GetPluggableSerializer Tests

	#region AddMessagePackPluggableSerialization Tests

	[Fact]
	public void AddMessagePackPluggableSerialization_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddMessagePackPluggableSerialization());
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddMessagePackPluggableSerialization();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_RegistersPluggableSerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackPluggableSerialization();
		var provider = services.BuildServiceProvider();

		// Assert
		var registry = provider.GetService<ISerializerRegistry>();
		_ = registry.ShouldNotBeNull();
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithSetAsCurrent_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddMessagePackPluggableSerialization(setAsCurrent: true);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithOptions_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		var options = MessagePackSerializerOptions.Standard;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddMessagePackPluggableSerialization(options));
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithOptions_ThrowsOnNullOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddMessagePackPluggableSerialization(null!));
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithOptions_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

		// Act
		var result = services.AddMessagePackPluggableSerialization(options);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithOptionsAndSetAsCurrent_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();
		var options = MessagePackSerializerOptions.Standard;

		// Act
		var result = services.AddMessagePackPluggableSerialization(options, setAsCurrent: true);

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion AddMessagePackPluggableSerialization Tests
}

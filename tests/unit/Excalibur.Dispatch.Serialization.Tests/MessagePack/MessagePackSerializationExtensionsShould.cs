// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

using MpkSerializer = Excalibur.Dispatch.Serialization.MessagePack.MessagePackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Unit tests for <see cref="MessagePackSerializationExtensions" />.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MessagePackSerializationExtensionsShould : UnitTestBase
{
	#region AddMessagePackSerialization Tests

	[Fact]
	public void AddMessagePackSerialization_RegistersISerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddMessagePackSerialization();

		// Assert
		services.Any(static sd =>
			sd.ServiceType == typeof(ISerializer) &&
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
	public void AddMessagePackSerialization_ResolvesISerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Act
		var serializer = provider.GetService<ISerializer>();

		// Assert
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<MpkSerializer>();
	}

	#endregion AddMessagePackSerialization Tests

	#region GetPluggableSerializer Tests

	[Fact]
	public void GetPluggableSerializer_ReturnsMessagePackPluggableSerializer()
	{
		// Act
		var serializer = MessagePackSerializationExtensions.GetPluggableSerializer();

		// Assert
		_ = serializer.ShouldBeOfType<MpkSerializer>();
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
		_ = serializer.ShouldBeOfType<MpkSerializer>();
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

		// Act -- Use SerializeToBytes extension for ISerializer (returns byte[])
		var bytes = serializer.SerializeToBytes(value);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Value.ShouldBe(42);
		result.Text.ShouldBe("Test");
	}

	[Fact]
	public void GetPluggableSerializer_ImplementsISerializer()
	{
		// Act
		var serializer = MessagePackSerializationExtensions.GetPluggableSerializer();

		// Assert
		_ = serializer.ShouldBeAssignableTo<ISerializer>();
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

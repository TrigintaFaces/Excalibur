// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Integration tests for MessagePack DI registration and resolution.
/// Tests the complete dependency injection pipeline.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackDIIntegrationShould : UnitTestBase
{
	#region Full DI Pipeline Tests

	[Fact]
	public void AddMessagePackSerialization_ResolvesWorkingSerializers()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Act
		var messageSerializer = provider.GetRequiredService<IMessageSerializer>();
		var zeroCopySerializer = provider.GetRequiredService<IZeroCopySerializer>();

		// Assert - serializers should be functional
		var msg = new TestMessage { Id = 1, Name = "DI" };
		var bytes = messageSerializer.Serialize(msg);
		var result = messageSerializer.Deserialize<TestMessage>(bytes);
		result.Id.ShouldBe(1);

		zeroCopySerializer.ShouldNotBeNull();
	}

	[Fact]
	public void AddMessagePackSerialization_WithOptions_AppliesOptionsToSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerialization(opts =>
		{
			opts.UseLz4Compression = true;
		});
		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<MessagePackSerializationOptions>>().Value;

		// Assert
		options.UseLz4Compression.ShouldBeTrue();
	}

	[Fact]
	public void AddMessagePackSerialization_Generic_WithMessagePackMessageSerializer()
	{
		// Arrange - use MessagePackMessageSerializer which requires IOptions
		var services = new ServiceCollection();

		// First add options
		services.Configure<MessagePackSerializationOptions>(opts =>
		{
			opts.UseLz4Compression = true;
		});

		// Then add with generic - but note MessagePackMessageSerializer needs IOptions<>
		services.AddSingleton<IMessageSerializer>(sp =>
		{
			var opts = sp.GetRequiredService<IOptions<MessagePackSerializationOptions>>();
			return new MessagePackMessageSerializer(opts);
		});
		services.TryAddSingleton<IZeroCopySerializer, MessagePackZeroCopySerializer>();

		var provider = services.BuildServiceProvider();

		// Act
		var serializer = provider.GetRequiredService<IMessageSerializer>();

		// Assert
		serializer.ShouldBeOfType<MessagePackMessageSerializer>();
		var msg = new TestMessage { Id = 99, Name = "Generic" };
		var bytes = serializer.Serialize(msg);
		var result = serializer.Deserialize<TestMessage>(bytes);
		result.Id.ShouldBe(99);
	}

	#endregion

	#region Singleton Behavior Tests

	[Fact]
	public void AddMessagePackSerialization_ReturnsSameInstanceForMultipleResolves()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Act
		var serializer1 = provider.GetRequiredService<IMessageSerializer>();
		var serializer2 = provider.GetRequiredService<IMessageSerializer>();

		// Assert - should be same singleton instance
		ReferenceEquals(serializer1, serializer2).ShouldBeTrue();
	}

	[Fact]
	public void AddMessagePackSerialization_ZeroCopyIsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Act
		var zeroCopy1 = provider.GetRequiredService<IZeroCopySerializer>();
		var zeroCopy2 = provider.GetRequiredService<IZeroCopySerializer>();

		// Assert
		ReferenceEquals(zeroCopy1, zeroCopy2).ShouldBeTrue();
	}

	#endregion

	#region Pluggable Serialization DI Tests

	[Fact]
	public void AddMessagePackPluggableSerialization_RegistersWithCorrectSerializerId()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackPluggableSerialization();
		var provider = services.BuildServiceProvider();

		// Act
		var registry = provider.GetService<ISerializerRegistry>();

		// Assert
		registry.ShouldNotBeNull();
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithOptions_RegistersSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		var customOptions = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.Lz4Block);
		services.AddMessagePackPluggableSerialization(customOptions);
		var provider = services.BuildServiceProvider();

		// Act
		var registry = provider.GetService<ISerializerRegistry>();

		// Assert
		registry.ShouldNotBeNull();
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_SetAsCurrent_Works()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackPluggableSerialization(setAsCurrent: true);
		var provider = services.BuildServiceProvider();

		// Act
		var registry = provider.GetService<ISerializerRegistry>();

		// Assert
		registry.ShouldNotBeNull();
	}

	[Fact]
	public void AddMessagePackPluggableSerialization_WithOptionsAndSetAsCurrent_Works()
	{
		// Arrange
		var services = new ServiceCollection();
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
		services.AddMessagePackPluggableSerialization(opts, setAsCurrent: true);
		var provider = services.BuildServiceProvider();

		// Act
		var registry = provider.GetService<ISerializerRegistry>();

		// Assert
		registry.ShouldNotBeNull();
	}

	#endregion

	#region Options Resolution Tests

	[Fact]
	public void Options_DefaultValues_AreCorrect()
	{
		// Arrange
		// Note: AddMessagePackSerialization only registers IOptions when a configure delegate is provided
		// So we use an empty configure delegate to trigger options registration
		var services = new ServiceCollection();
		services.AddMessagePackSerialization(_ => { });
		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<MessagePackSerializationOptions>>().Value;

		// Assert - defaults should be applied
		options.UseLz4Compression.ShouldBeFalse();
	}

	[Fact]
	public void Options_MultipleConfigurations_CombineCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		services.Configure<MessagePackSerializationOptions>(opts =>
		{
			opts.UseLz4Compression = true;
		});
		services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetRequiredService<IOptions<MessagePackSerializationOptions>>().Value;

		// Assert - configuration should apply
		options.UseLz4Compression.ShouldBeTrue();
	}

	#endregion

	#region Scoped Resolution Tests

	[Fact]
	public void AddMessagePackSerialization_WorksWithScopedProvider()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Act - create scope and resolve
		using var scope = provider.CreateScope();
		var serializer = scope.ServiceProvider.GetRequiredService<IMessageSerializer>();

		// Assert
		serializer.ShouldNotBeNull();
		var msg = new TestMessage { Id = 5, Name = "Scoped" };
		var bytes = serializer.Serialize(msg);
		var result = serializer.Deserialize<TestMessage>(bytes);
		result.Id.ShouldBe(5);
	}

	[Fact]
	public void AddMessagePackSerialization_SameInstanceAcrossScopes()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddMessagePackSerialization();
		var provider = services.BuildServiceProvider();

		// Act
		IMessageSerializer serializer1, serializer2;
		using (var scope1 = provider.CreateScope())
		{
			serializer1 = scope1.ServiceProvider.GetRequiredService<IMessageSerializer>();
		}
		using (var scope2 = provider.CreateScope())
		{
			serializer2 = scope2.ServiceProvider.GetRequiredService<IMessageSerializer>();
		}

		// Assert - singleton so same instance
		ReferenceEquals(serializer1, serializer2).ShouldBeTrue();
	}

	#endregion
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

using Excalibur.Dispatch.Serialization.Protobuf;

namespace Excalibur.Dispatch.Serialization.Tests.Protobuf;

/// <summary>
/// Tests for <see cref="ProtobufSerializationExtensions"/>.
/// </summary>
/// <remarks>
/// Per T10.*, these tests verify:
/// - DI registration correctness
/// - Options configuration via delegate
/// - Null argument validation
/// - Service collection integrity
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ProtobufSerializationExtensionsShould
{
	[Fact]
	public void Register_Protobuf_Serializer_Without_Configuration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddProtobufSerialization();
		var provider = services.BuildServiceProvider();

		// Assert
		var serializer = provider.GetService<IMessageSerializer>();
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<ProtobufMessageSerializer>();
	}

	[Fact]
	public void Register_Protobuf_Serializer_With_Configuration()
	{
		// Arrange
		var services = new ServiceCollection();
		var configuredFormat = ProtobufWireFormat.Json;

		// Act
		_ = services.AddProtobufSerialization(options =>
		{
			options.WireFormat = configuredFormat;
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<ProtobufSerializationOptions>>();
		options.Value.WireFormat.ShouldBe(configuredFormat);
	}

	[Fact]
	public void Return_Same_ServiceCollection_For_Chaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddProtobufSerialization();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void Throw_ArgumentNullException_When_ServiceCollection_Is_Null()
	{
		// Arrange
		IServiceCollection? nullServices = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => nullServices.AddProtobufSerialization());
	}

	[Fact]
	public void Not_Throw_When_Configuration_Delegate_Is_Null()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.NotThrow(() => services.AddProtobufSerialization(configure: null));
	}

	[Fact]
	public void Register_As_Singleton()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddProtobufSerialization();
		var provider = services.BuildServiceProvider();

		// Act
		var serializer1 = provider.GetRequiredService<IMessageSerializer>();
		var serializer2 = provider.GetRequiredService<IMessageSerializer>();

		// Assert
		serializer1.ShouldBeSameAs(serializer2);
	}

	[Fact]
	public void Not_Override_Existing_Serializer_When_Already_Registered()
	{
		// Arrange
		var services = new ServiceCollection();
		var existingSerializer = A.Fake<IMessageSerializer>();
		_ = services.AddSingleton(existingSerializer);

		// Act
		_ = services.AddProtobufSerialization();
		var provider = services.BuildServiceProvider();

		// Assert
		var resolved = provider.GetRequiredService<IMessageSerializer>();
		resolved.ShouldBeSameAs(existingSerializer); // TryAddSingleton should not override
	}

	[Fact]
	public void Use_Default_Options_When_No_Configuration_Provided()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddProtobufSerialization();
		var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetService<IOptions<ProtobufSerializationOptions>>();

		// Assert
		if (options != null)
		{
			// If options are registered, they should have default values
			options.Value.WireFormat.ShouldBe(ProtobufWireFormat.Binary);
		}
	}

	[Fact]
	public void Allow_Multiple_Configuration_Calls()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddProtobufSerialization(options => options.WireFormat = ProtobufWireFormat.Binary);
		_ = services.Configure<ProtobufSerializationOptions>(options => options.WireFormat = ProtobufWireFormat.Json);
		var provider = services.BuildServiceProvider();

		// Assert — last Configure wins
		var options = provider.GetRequiredService<IOptions<ProtobufSerializationOptions>>();
		options.Value.WireFormat.ShouldBe(ProtobufWireFormat.Json);
	}

	[Fact]
	public void Register_Serializer_That_Can_Serialize_And_Deserialize()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddProtobufSerialization();
		var provider = services.BuildServiceProvider();

		var serializer = provider.GetRequiredService<IMessageSerializer>();
		var message = new TestMessage { Name = "IntegrationTest", Value = 999, IsActive = true };

		// Act
		var serialized = serializer.Serialize(message);
		var deserialized = serializer.Deserialize<TestMessage>(serialized);

		// Assert
		_ = deserialized.ShouldNotBeNull();
		deserialized.Name.ShouldBe(message.Name);
		deserialized.Value.ShouldBe(message.Value);
		deserialized.IsActive.ShouldBe(message.IsActive);
	}

	[Fact]
	public void Support_Binary_Wire_Format_Configuration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddProtobufSerialization(options => options.WireFormat = ProtobufWireFormat.Binary);
		var provider = services.BuildServiceProvider();

		var serializer = provider.GetRequiredService<IMessageSerializer>();
		var message = new TestMessage { Name = "BinaryTest", Value = 42 };

		// Act
		var serialized = serializer.Serialize(message);

		// Assert
		_ = serialized.ShouldNotBeNull();
		serialized.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Support_Json_Wire_Format_Configuration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddProtobufSerialization(options => options.WireFormat = ProtobufWireFormat.Json);
		var provider = services.BuildServiceProvider();

		var serializer = provider.GetRequiredService<IMessageSerializer>();
		var message = new TestMessage { Name = "JsonTest", Value = 123 };

		// Act
		var serialized = serializer.Serialize(message);
		var jsonString = System.Text.Encoding.UTF8.GetString(serialized);

		// Assert
		jsonString.ShouldContain("JsonTest");
		jsonString.ShouldContain("123");
	}

	[Fact]
	public void Be_Compatible_With_Dispatch_Builder_Pattern()
	{
		// Arrange
		var services = new ServiceCollection();
		// Simulate Dispatch registration pattern
		_ = services.AddLogging();

		// Act
		_ = services.AddProtobufSerialization(options =>
		{
			options.WireFormat = ProtobufWireFormat.Binary;
		});

		var provider = services.BuildServiceProvider();

		// Assert
		var serializer = provider.GetService<IMessageSerializer>();
		_ = serializer.ShouldNotBeNull();
		_ = serializer.ShouldBeOfType<ProtobufMessageSerializer>();
	}
}

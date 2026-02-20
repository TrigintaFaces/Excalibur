// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;
using MessagePack.Resolvers;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Cross-serializer compatibility tests to verify data can be shared between
/// different MessagePack serializer implementations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackSerializerCompatibilityShould : UnitTestBase
{
	#region DispatchMessagePackSerializer Compatibility

	[Fact]
	public void DispatchSerializer_OutputCompatibleWithNativeMessagePack()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var original = new TestMessage { Id = 42, Name = "CrossCompat" };

		// Act - Serialize with DispatchMessagePackSerializer
		var bytes = serializer.Serialize(original);

		// Deserialize with native MessagePack (using same compression options)
		var options = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.Lz4BlockArray);
		var result = global::MessagePack.MessagePackSerializer.Deserialize<TestMessage>(bytes, options);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void NativeMessagePack_OutputCompatibleWithDispatchSerializer()
	{
		// Arrange
		var serializer = new DispatchMessagePackSerializer();
		var original = new TestMessage { Id = 55, Name = "NativeToDispatch" };

		// Serialize with native MessagePack (same compression)
		var options = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.Lz4BlockArray);
		var bytes = global::MessagePack.MessagePackSerializer.Serialize(original, options);

		// Act - Deserialize with DispatchMessagePackSerializer
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	#endregion

	#region MessagePackMessageSerializer Compatibility

	[Fact]
	public void MessagePackMessageSerializer_OutputCompatibleWithNativeMessagePack()
	{
		// Arrange
		var msgPackOptions = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var serializer = new MessagePackMessageSerializer(msgPackOptions);
		var original = new TestMessage { Id = 33, Name = "MessageSerializerCompat" };

		// Act - Serialize
		var bytes = serializer.Serialize(original);

		// Deserialize with native MessagePack (standard options, no compression)
		var result = global::MessagePack.MessagePackSerializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void MessagePackMessageSerializer_WithCompression_OutputCompatibleWithNative()
	{
		// Arrange
		var msgPackOptions = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions
		{
			UseLz4Compression = true
		});
		var serializer = new MessagePackMessageSerializer(msgPackOptions);
		var original = new TestMessage { Id = 44, Name = "CompressedCompat" };

		// Act - Serialize with compression
		var bytes = serializer.Serialize(original);

		// Deserialize with native MessagePack (Lz4Block compression)
		var nativeOptions = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.Lz4Block);
		var result = global::MessagePack.MessagePackSerializer.Deserialize<TestMessage>(bytes, nativeOptions);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	#endregion

	#region PluggableSerializer Compatibility

	[Fact]
	public void PluggableSerializer_OutputCompatibleWithNativeMessagePack()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var original = new TestPluggableMessage { Value = 88, Text = "PluggableCompat" };

		// Act - Serialize
		var bytes = serializer.Serialize(original);

		// Deserialize with native MessagePack
		var result = global::MessagePack.MessagePackSerializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(original.Value);
		result.Text.ShouldBe(original.Text);
	}

	[Fact]
	public void NativeMessagePack_OutputCompatibleWithPluggableSerializer()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var original = new TestPluggableMessage { Value = 77, Text = "NativeToPluggable" };

		// Serialize with native
		var bytes = global::MessagePack.MessagePackSerializer.Serialize(original);

		// Act - Deserialize with pluggable
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(original.Value);
		result.Text.ShouldBe(original.Text);
	}

	[Fact]
	public void PluggableSerializer_SerializeObject_CompatibleWithNative()
	{
		// Arrange
		var serializer = new MessagePackPluggableSerializer();
		var original = new TestPluggableMessage { Value = 66, Text = "ObjectCompat" };

		// Act - SerializeObject
		var bytes = serializer.SerializeObject(original, typeof(TestPluggableMessage));

		// Deserialize with native
		var result = global::MessagePack.MessagePackSerializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(original.Value);
		result.Text.ShouldBe(original.Text);
	}

	#endregion

	#region ZeroCopySerializer Compatibility

	[Fact]
	public void ZeroCopySerializer_DeserializeCompatibleWithContractlessNative()
	{
		// Arrange
		var serializer = new MessagePackZeroCopySerializer();
		var original = new TestMessage { Id = 99, Name = "ZeroCopyCompat" };

		// Serialize with ContractlessStandardResolver + Lz4 (what ZeroCopy uses)
		var options = MessagePackSerializerOptions.Standard
			.WithResolver(ContractlessStandardResolver.Instance)
			.WithCompression(MessagePackCompression.Lz4BlockArray);
		var bytes = global::MessagePack.MessagePackSerializer.Serialize(original, options);

		// Act - Deserialize with ZeroCopy
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	#endregion

	#region Cross-Serializer Interop

	[Fact]
	public void DispatchSerializer_CanDeserializeFromMessagePackMessageSerializer_NoCompression()
	{
		// Arrange - Both use standard options without compression
		var msgPackOptions = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions
		{
			UseLz4Compression = false
		});
		var messageSerializer = new MessagePackMessageSerializer(msgPackOptions);

		// Use no-compression dispatch serializer
		var dispatchOptions = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.None);
		var dispatchSerializer = new DispatchMessagePackSerializer(dispatchOptions);

		var original = new TestMessage { Id = 111, Name = "CrossSerializer" };

		// Act - Serialize with MessagePackMessageSerializer
		var bytes = messageSerializer.Serialize(original);

		// Deserialize with DispatchMessagePackSerializer
		var result = dispatchSerializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void PluggableSerializer_CanSerializeForAotSerializer()
	{
		// Arrange - Both use standard options
		var pluggable = new MessagePackPluggableSerializer();
		var aotOptions = MessagePackSerializerOptions.Standard;
		var aot = new AotMessagePackSerializer(aotOptions);

		var original = new TestPluggableMessage { Value = 222, Text = "PluggableToAot" };

		// Act - Serialize with Pluggable
		var bytes = pluggable.Serialize(original);

		// Deserialize with AOT serializer
		var result = aot.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(original.Value);
		result.Text.ShouldBe(original.Text);
	}

	[Fact]
	public void AotSerializer_CanSerializeForPluggableSerializer()
	{
		// Arrange
		var aotOptions = MessagePackSerializerOptions.Standard;
		var aot = new AotMessagePackSerializer(aotOptions);
		var pluggable = new MessagePackPluggableSerializer();

		var original = new TestPluggableMessage { Value = 333, Text = "AotToPluggable" };

		// Act - Serialize with AOT
		var bytes = aot.Serialize(original);

		// Deserialize with Pluggable
		var result = pluggable.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(original.Value);
		result.Text.ShouldBe(original.Text);
	}

	#endregion

	#region Data Format Verification

	[Fact]
	public void AllSerializers_ProduceNonEmptyOutput()
	{
		// Arrange
		var message = new TestMessage { Id = 1, Name = "Test" };
		var dispatch = new DispatchMessagePackSerializer();
		var msgPackOptions = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var messageSerializer = new MessagePackMessageSerializer(msgPackOptions);
		var aotOptions = MessagePackSerializerOptions.Standard;
		var aot = new AotMessagePackSerializer(aotOptions);

		// Act & Assert
		dispatch.Serialize(message).Length.ShouldBeGreaterThan(0);
		messageSerializer.Serialize(message).Length.ShouldBeGreaterThan(0);
		aot.Serialize(message).Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void PluggableSerializer_ProducesNonEmptyOutput()
	{
		// Arrange
		var pluggable = new MessagePackPluggableSerializer();
		var message = new TestPluggableMessage { Value = 1, Text = "Test" };

		// Act
		var bytes = pluggable.Serialize(message);
		var objectBytes = pluggable.SerializeObject(message, typeof(TestPluggableMessage));

		// Assert
		bytes.Length.ShouldBeGreaterThan(0);
		objectBytes.Length.ShouldBeGreaterThan(0);
	}

	#endregion
}

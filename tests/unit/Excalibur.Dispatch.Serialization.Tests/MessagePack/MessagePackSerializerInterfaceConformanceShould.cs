// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Interface conformance tests for all MessagePack serializer implementations.
/// Ensures all serializers correctly implement their respective interfaces.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackSerializerInterfaceConformanceShould : UnitTestBase
{
	#region IMessageSerializer Conformance

	[Fact]
	public void DispatchMessagePackSerializer_ImplementsIMessageSerializer()
	{
		// Arrange & Act
		IMessageSerializer serializer = new DispatchMessagePackSerializer();

		// Assert
		serializer.ShouldBeAssignableTo<IMessageSerializer>();
		serializer.SerializerName.ShouldNotBeNullOrWhiteSpace();
		serializer.SerializerVersion.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void AotMessagePackSerializer_ImplementsIMessageSerializer()
	{
		// Arrange & Act
		IMessageSerializer serializer = new AotMessagePackSerializer();

		// Assert
		serializer.ShouldBeAssignableTo<IMessageSerializer>();
		serializer.SerializerName.ShouldNotBeNullOrWhiteSpace();
		serializer.SerializerVersion.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void MessagePackMessageSerializer_ImplementsIMessageSerializer()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());

		// Act
		IMessageSerializer serializer = new MessagePackMessageSerializer(options);

		// Assert
		serializer.ShouldBeAssignableTo<IMessageSerializer>();
		serializer.SerializerName.ShouldNotBeNullOrWhiteSpace();
		serializer.SerializerVersion.ShouldNotBeNullOrWhiteSpace();
	}

	#endregion

	#region IPluggableSerializer Conformance

	[Fact]
	public void MessagePackPluggableSerializer_ImplementsIPluggableSerializer()
	{
		// Arrange & Act
		IPluggableSerializer serializer = new MessagePackPluggableSerializer();

		// Assert
		serializer.ShouldBeAssignableTo<IPluggableSerializer>();
		serializer.Name.ShouldNotBeNullOrWhiteSpace();
		serializer.Version.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void MessagePackPluggableSerializer_WithOptions_ImplementsIPluggableSerializer()
	{
		// Arrange
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

		// Act
		IPluggableSerializer serializer = new MessagePackPluggableSerializer(options);

		// Assert
		serializer.ShouldBeAssignableTo<IPluggableSerializer>();
		serializer.Name.ShouldBe("MessagePack");
	}

	#endregion

	#region IZeroCopySerializer Conformance

	[Fact]
	public void MessagePackZeroCopySerializer_ImplementsIZeroCopySerializer()
	{
		// Arrange & Act
		IZeroCopySerializer serializer = new MessagePackZeroCopySerializer();

		// Assert
		serializer.ShouldBeAssignableTo<IZeroCopySerializer>();
	}

	#endregion

	#region Cross-Serializer Compatibility

	[Fact]
	public void AllMessageSerializers_HaveConsistentSerializerName()
	{
		// Arrange
		var dispatchSerializer = new DispatchMessagePackSerializer();
		var msgPackOptions = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var messageSerializer = new MessagePackMessageSerializer(msgPackOptions);

		// Act & Assert - Both should return "MessagePack"
		dispatchSerializer.SerializerName.ShouldBe("MessagePack");
		messageSerializer.SerializerName.ShouldBe("MessagePack");
	}

	[Fact]
	public void AllMessageSerializers_HaveConsistentSerializerVersion()
	{
		// Arrange
		var dispatchSerializer = new DispatchMessagePackSerializer();
		var msgPackOptions = Microsoft.Extensions.Options.Options.Create(new MessagePackSerializationOptions());
		var messageSerializer = new MessagePackMessageSerializer(msgPackOptions);

		// Act & Assert - Both should return "1.0.0"
		dispatchSerializer.SerializerVersion.ShouldBe("1.0.0");
		messageSerializer.SerializerVersion.ShouldBe("1.0.0");
	}

	[Fact]
	public void AotMessagePackSerializer_HasDistinctSerializerName()
	{
		// Arrange
		var aotSerializer = new AotMessagePackSerializer();

		// Assert - AOT serializer has a distinct name
		aotSerializer.SerializerName.ShouldBe("MessagePack-AOT");
	}

	[Fact]
	public void PluggableSerializer_Name_MatchesIMessageSerializerName()
	{
		// Arrange
		var pluggable = new MessagePackPluggableSerializer();
		var dispatch = new DispatchMessagePackSerializer();

		// Assert - Both should identify as "MessagePack"
		pluggable.Name.ShouldBe(dispatch.SerializerName);
	}

	#endregion

	#region Serialize-Deserialize Contract

	[Fact]
	public void IMessageSerializer_SerializeDeserialize_ContractIsHonored()
	{
		// Arrange
		IMessageSerializer serializer = new DispatchMessagePackSerializer();
		var original = new TestMessage { Id = 42, Name = "Contract" };

		// Act
		var bytes = serializer.Serialize(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void IPluggableSerializer_SerializeDeserialize_ContractIsHonored()
	{
		// Arrange
		IPluggableSerializer serializer = new MessagePackPluggableSerializer();
		var original = new TestPluggableMessage { Value = 99, Text = "Contract" };

		// Act
		var bytes = serializer.Serialize(original);
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(original.Value);
		result.Text.ShouldBe(original.Text);
	}

	[Fact]
	public void IPluggableSerializer_SerializeObjectDeserializeObject_ContractIsHonored()
	{
		// Arrange
		IPluggableSerializer serializer = new MessagePackPluggableSerializer();
		var original = new TestPluggableMessage { Value = 77, Text = "ObjectContract" };

		// Act
		var bytes = serializer.SerializeObject(original, typeof(TestPluggableMessage));
		var result = (TestPluggableMessage)serializer.DeserializeObject(bytes, typeof(TestPluggableMessage));

		// Assert
		result.Value.ShouldBe(original.Value);
		result.Text.ShouldBe(original.Text);
	}

	#endregion

	#region Null Handling Contract

	[Fact]
	public void IPluggableSerializer_Serialize_ThrowsOnNull()
	{
		// Arrange
		IPluggableSerializer serializer = new MessagePackPluggableSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.Serialize<TestPluggableMessage>(null!));
	}

	[Fact]
	public void IPluggableSerializer_SerializeObject_ThrowsOnNullValue()
	{
		// Arrange
		IPluggableSerializer serializer = new MessagePackPluggableSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(null!, typeof(TestPluggableMessage)));
	}

	[Fact]
	public void IPluggableSerializer_SerializeObject_ThrowsOnNullType()
	{
		// Arrange
		IPluggableSerializer serializer = new MessagePackPluggableSerializer();
		var value = new TestPluggableMessage();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(value, null!));
	}

	[Fact]
	public void IPluggableSerializer_DeserializeObject_ThrowsOnNullType()
	{
		// Arrange
		IPluggableSerializer serializer = new MessagePackPluggableSerializer();
		var data = new byte[] { 0x01 };

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.DeserializeObject(data, null!));
	}

	#endregion
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

using MpkSerializer = Excalibur.Dispatch.Serialization.MessagePack.MessagePackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Interface conformance tests for the consolidated <see cref="MpkSerializer"/>.
/// Ensures it correctly implements ISerializer.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackSerializerInterfaceConformanceShould : UnitTestBase
{
	#region ISerializer Conformance

	[Fact]
	public void MessagePackSerializer_ImplementsISerializer()
	{
		// Arrange & Act
		ISerializer serializer = new MpkSerializer();

		// Assert
		serializer.ShouldBeAssignableTo<ISerializer>();
		serializer.Name.ShouldNotBeNullOrWhiteSpace();
		serializer.Version.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void MessagePackSerializer_WithOptions_ImplementsISerializer()
	{
		// Arrange
		var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

		// Act
		ISerializer serializer = new MpkSerializer(options);

		// Assert
		serializer.ShouldBeAssignableTo<ISerializer>();
		serializer.Name.ShouldBe("MessagePack");
	}

	#endregion

	#region Cross-Serializer Compatibility

	[Fact]
	public void AllInstances_HaveConsistentSerializerName()
	{
		// Arrange
		var serializer1 = new MpkSerializer();
		var serializer2 = new MpkSerializer(MessagePackSerializerOptions.Standard);

		// Act & Assert - Both should return "MessagePack"
		serializer1.Name.ShouldBe("MessagePack");
		serializer2.Name.ShouldBe("MessagePack");
	}

	[Fact]
	public void AllInstances_HaveConsistentSerializerVersion()
	{
		// Arrange
		var serializer1 = new MpkSerializer();
		var serializer2 = new MpkSerializer(MessagePackSerializerOptions.Standard);

		// Act & Assert
		serializer1.Version.ShouldBe(serializer2.Version);
	}

	[Fact]
	public void Serializer_Name_IsConsistentAcrossInterfaces()
	{
		// Arrange
		var instance = new MpkSerializer();
		ISerializer asSerializer = instance;
		ISerializer asPluggable = instance;

		// Assert - Both interfaces should identify as "MessagePack"
		asSerializer.Name.ShouldBe(asPluggable.Name);
	}

	#endregion

	#region Serialize-Deserialize Contract

	[Fact]
	public void ISerializer_SerializeDeserialize_ContractIsHonored()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 42, Name = "Contract" };

		// Act
		var bytes = serializer.SerializeToBytes(original);
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void ISerializer_SerializeObjectDeserializeObject_ContractIsHonored()
	{
		// Arrange
		ISerializer serializer = new MpkSerializer();
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
	public void ISerializer_Serialize_HandlesNull()
	{
		// Arrange — MessagePack serializes null as nil (valid behavior)
		ISerializer serializer = new MpkSerializer();

		// Act
		var result = serializer.SerializeToBytes<TestPluggableMessage>(null!);

		// Assert — should produce valid MessagePack nil
		result.ShouldNotBeNull();
		result.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ISerializer_SerializeObject_ThrowsOnNullValue()
	{
		// Arrange
		ISerializer serializer = new MpkSerializer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(null!, typeof(TestPluggableMessage)));
	}

	[Fact]
	public void ISerializer_SerializeObject_ThrowsOnNullType()
	{
		// Arrange
		ISerializer serializer = new MpkSerializer();
		var value = new TestPluggableMessage();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.SerializeObject(value, null!));
	}

	[Fact]
	public void ISerializer_DeserializeObject_ThrowsOnNullType()
	{
		// Arrange
		ISerializer serializer = new MpkSerializer();
		var data = new byte[] { 0x01 };

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			serializer.DeserializeObject(data, null!));
	}

	#endregion
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Serialization.MessagePack;

using MessagePack;

using MpkSerializer = Excalibur.Dispatch.Serialization.MessagePack.MessagePackSerializer;

namespace Excalibur.Dispatch.Serialization.Tests.MessagePack;

/// <summary>
/// Cross-serializer compatibility tests to verify data can be shared between
/// the consolidated <see cref="MpkSerializer"/> and native MessagePack.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class MessagePackSerializerCompatibilityShould : UnitTestBase
{
	#region Serializer-to-Native Compatibility

	[Fact]
	public void Serializer_OutputCompatibleWithNativeMessagePack()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 42, Name = "CrossCompat" };

		// Act - Serialize with consolidated serializer
		var bytes = serializer.SerializeToBytes(original);

		// Deserialize with native MessagePack (using same Standard options)
		var result = global::MessagePack.MessagePackSerializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void NativeMessagePack_OutputCompatibleWithSerializer()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var original = new TestMessage { Id = 55, Name = "NativeToSerializer" };

		// Serialize with native MessagePack (same Standard options)
		var bytes = global::MessagePack.MessagePackSerializer.Serialize(original);

		// Act - Deserialize with consolidated serializer
		var result = serializer.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	#endregion

	#region Compression Compatibility

	[Fact]
	public void Serializer_WithCompression_OutputCompatibleWithNative()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
		var serializer = new MpkSerializer(opts);
		var original = new TestMessage { Id = 44, Name = "CompressedCompat" };

		// Act - Serialize with compression
		var bytes = serializer.SerializeToBytes(original);

		// Deserialize with native MessagePack (Lz4Block compression)
		var result = global::MessagePack.MessagePackSerializer.Deserialize<TestMessage>(bytes, opts);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Serializer_NoCompression_OutputCompatibleWithNative()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None);
		var serializer = new MpkSerializer(opts);
		var original = new TestMessage { Id = 33, Name = "NoCompressCompat" };

		// Act - Serialize
		var bytes = serializer.SerializeToBytes(original);

		// Deserialize with native MessagePack (standard options, no compression)
		var result = global::MessagePack.MessagePackSerializer.Deserialize<TestMessage>(bytes);

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
		var serializer = new MpkSerializer();
		var original = new TestPluggableMessage { Value = 88, Text = "PluggableCompat" };

		// Act - Serialize
		var bytes = serializer.SerializeToBytes(original);

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
		var serializer = new MpkSerializer();
		var original = new TestPluggableMessage { Value = 77, Text = "NativeToPluggable" };

		// Serialize with native
		var bytes = global::MessagePack.MessagePackSerializer.Serialize(original);

		// Act - Deserialize with consolidated serializer
		var result = serializer.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(original.Value);
		result.Text.ShouldBe(original.Text);
	}

	[Fact]
	public void PluggableSerializer_SerializeObject_CompatibleWithNative()
	{
		// Arrange
		var serializer = new MpkSerializer();
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

	#region Cross-Instance Interop

	[Fact]
	public void Serializer_NoCompression_CanDeserializeFromNoCompression()
	{
		// Arrange - Both use standard options without compression
		var noCompressionOpts = MessagePackSerializerOptions.Standard
			.WithCompression(MessagePackCompression.None);
		var serializer1 = new MpkSerializer(noCompressionOpts);
		var serializer2 = new MpkSerializer(noCompressionOpts);

		var original = new TestMessage { Id = 111, Name = "CrossSerializer" };

		// Act - Serialize with one, deserialize with another
		var bytes = serializer1.SerializeToBytes(original);
		var result = serializer2.Deserialize<TestMessage>(bytes);

		// Assert
		result.Id.ShouldBe(original.Id);
		result.Name.ShouldBe(original.Name);
	}

	[Fact]
	public void Serializer_WithSameOptions_CanInteroperate()
	{
		// Arrange - Both use standard options
		var opts = MessagePackSerializerOptions.Standard;
		var serializer1 = new MpkSerializer(opts);
		var serializer2 = new MpkSerializer(opts);

		var original = new TestPluggableMessage { Value = 222, Text = "Interop" };

		// Act - Serialize with first, deserialize with second
		var bytes = serializer1.SerializeToBytes(original);
		var result = serializer2.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(original.Value);
		result.Text.ShouldBe(original.Text);
	}

	[Fact]
	public void Serializer_ReverseDirection_CanInteroperate()
	{
		// Arrange
		var opts = MessagePackSerializerOptions.Standard;
		var serializer1 = new MpkSerializer(opts);
		var serializer2 = new MpkSerializer();

		var original = new TestPluggableMessage { Value = 333, Text = "Reverse" };

		// Act - Serialize with second, deserialize with first
		var bytes = serializer2.SerializeToBytes(original);
		var result = serializer1.Deserialize<TestPluggableMessage>(bytes);

		// Assert
		result.Value.ShouldBe(original.Value);
		result.Text.ShouldBe(original.Text);
	}

	#endregion

	#region Data Format Verification

	[Fact]
	public void AllSerializerInstances_ProduceNonEmptyOutput()
	{
		// Arrange
		var message = new TestMessage { Id = 1, Name = "Test" };
		var defaultSerializer = new MpkSerializer();
		var standardSerializer = new MpkSerializer(MessagePackSerializerOptions.Standard);
		var lz4Serializer = new MpkSerializer(
			MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));

		// Act & Assert
		defaultSerializer.SerializeToBytes(message).Length.ShouldBeGreaterThan(0);
		standardSerializer.SerializeToBytes(message).Length.ShouldBeGreaterThan(0);
		lz4Serializer.SerializeToBytes(message).Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Serializer_ProducesNonEmptyOutput_ForPluggableMessage()
	{
		// Arrange
		var serializer = new MpkSerializer();
		var message = new TestPluggableMessage { Value = 1, Text = "Test" };

		// Act
		var bytes = serializer.SerializeToBytes(message);
		var objectBytes = serializer.SerializeObject(message, typeof(TestPluggableMessage));

		// Assert
		bytes.Length.ShouldBeGreaterThan(0);
		objectBytes.Length.ShouldBeGreaterThan(0);
	}

	#endregion
}

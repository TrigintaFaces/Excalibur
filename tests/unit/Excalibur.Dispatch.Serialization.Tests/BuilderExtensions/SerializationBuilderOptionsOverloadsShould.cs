// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Serialization.Avro;
using Excalibur.Dispatch.Serialization.MessagePack;
using Excalibur.Dispatch.Serialization.Protobuf;

namespace Excalibur.Dispatch.Serialization.Tests.BuilderExtensions;

/// <summary>
/// Tests for the new ISerializationBuilder Action&lt;TOptions&gt; overloads
/// added in Sprint 720 (vj8za8): RegisterAvro, RegisterMessagePack, RegisterProtobuf.
/// Sprint 721 additions: verify options are actually applied to serializer instances.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
public sealed class SerializationBuilderOptionsOverloadsShould
{
	#region RegisterAvro(configure)

	[Fact]
	public void RegisterAvro_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		ISerializationBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.RegisterAvro(opts => { }))
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void RegisterAvro_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.RegisterAvro((Action<AvroSerializationOptions>)null!))
			.ParamName.ShouldBe("configure");
	}

	[Fact]
	public void RegisterAvro_InvokesConfigureDelegate()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<Abstractions.Serialization.ISerializer>._, A<byte>._))
			.Returns(builder);
		var delegateInvoked = false;

		// Act
		builder.RegisterAvro(opts =>
		{
			delegateInvoked = true;
			opts.BufferSize = 8192;
		});

		// Assert
		delegateInvoked.ShouldBeTrue();
	}

	[Fact]
	public void RegisterAvro_ReturnsBuilderForFluentChaining()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<Abstractions.Serialization.ISerializer>._, A<byte>._))
			.Returns(builder);

		// Act
		var result = builder.RegisterAvro(opts => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterAvro_RegistersSerializerWithCorrectId()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<Abstractions.Serialization.ISerializer>._, A<byte>._))
			.Returns(builder);

		// Act
		builder.RegisterAvro(opts => { });

		// Assert
		A.CallTo(() => builder.Register(
			A<Abstractions.Serialization.ISerializer>.That.IsInstanceOf(typeof(AvroSerializer)),
			SerializerIds.Avro))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void RegisterAvro_WithOptions_AppliesBufferSize()
	{
		// Arrange
		const int customBufferSize = 16384;
		ISerializer? capturedSerializer = null;
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<ISerializer>._, A<byte>._))
			.Invokes((ISerializer s, byte _) => capturedSerializer = s)
			.Returns(builder);

		// Act
		builder.RegisterAvro(opts => opts.BufferSize = customBufferSize);

		// Assert -- verify via reflection that the private _bufferSize field was set
		capturedSerializer.ShouldNotBeNull();
		capturedSerializer.ShouldBeOfType<AvroSerializer>();

		var bufferSizeField = typeof(AvroSerializer)
			.GetField("_bufferSize", BindingFlags.NonPublic | BindingFlags.Instance);
		bufferSizeField.ShouldNotBeNull("AvroSerializer should have a _bufferSize field");

		var actualBufferSize = (int)bufferSizeField.GetValue(capturedSerializer)!;
		actualBufferSize.ShouldBe(customBufferSize);
	}

	[Fact]
	public void RegisterAvro_WithoutOptions_UsesDefaults()
	{
		// Arrange
		const int defaultBufferSize = 4096;
		ISerializer? capturedSerializer = null;
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<ISerializer>._, A<byte>._))
			.Invokes((ISerializer s, byte _) => capturedSerializer = s)
			.Returns(builder);

		// Act -- parameterless overload
		builder.RegisterAvro();

		// Assert -- default buffer size should be used
		capturedSerializer.ShouldNotBeNull();
		capturedSerializer.ShouldBeOfType<AvroSerializer>();

		var bufferSizeField = typeof(AvroSerializer)
			.GetField("_bufferSize", BindingFlags.NonPublic | BindingFlags.Instance);
		bufferSizeField.ShouldNotBeNull("AvroSerializer should have a _bufferSize field");

		var actualBufferSize = (int)bufferSizeField.GetValue(capturedSerializer)!;
		actualBufferSize.ShouldBe(defaultBufferSize);
	}

	#endregion

	#region RegisterMessagePack(configure)

	[Fact]
	public void RegisterMessagePack_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		ISerializationBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.RegisterMessagePack(opts => { }))
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void RegisterMessagePack_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.RegisterMessagePack((Action<MessagePackSerializationOptions>)null!))
			.ParamName.ShouldBe("configure");
	}

	[Fact]
	public void RegisterMessagePack_InvokesConfigureDelegate()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<Abstractions.Serialization.ISerializer>._, A<byte>._))
			.Returns(builder);
		var delegateInvoked = false;

		// Act
		builder.RegisterMessagePack(opts =>
		{
			delegateInvoked = true;
		});

		// Assert
		delegateInvoked.ShouldBeTrue();
	}

	[Fact]
	public void RegisterMessagePack_ReturnsBuilderForFluentChaining()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<Abstractions.Serialization.ISerializer>._, A<byte>._))
			.Returns(builder);

		// Act
		var result = builder.RegisterMessagePack(opts => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterMessagePack_RegistersSerializerWithCorrectId()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<Abstractions.Serialization.ISerializer>._, A<byte>._))
			.Returns(builder);

		// Act
		builder.RegisterMessagePack(opts => { });

		// Assert
		A.CallTo(() => builder.Register(
			A<Abstractions.Serialization.ISerializer>.That.IsInstanceOf(typeof(MessagePackSerializer)),
			SerializerIds.MessagePack))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region RegisterProtobuf(configure)

	[Fact]
	public void RegisterProtobuf_ThrowsArgumentNullException_WhenBuilderIsNull()
	{
		// Arrange
		ISerializationBuilder builder = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.RegisterProtobuf(opts => { }))
			.ParamName.ShouldBe("builder");
	}

	[Fact]
	public void RegisterProtobuf_ThrowsArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			builder.RegisterProtobuf((Action<ProtobufSerializationOptions>)null!))
			.ParamName.ShouldBe("configure");
	}

	[Fact]
	public void RegisterProtobuf_InvokesConfigureDelegate()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<Abstractions.Serialization.ISerializer>._, A<byte>._))
			.Returns(builder);
		var delegateInvoked = false;

		// Act
		builder.RegisterProtobuf(opts =>
		{
			delegateInvoked = true;
			opts.WireFormat = ProtobufWireFormat.Json;
		});

		// Assert
		delegateInvoked.ShouldBeTrue();
	}

	[Fact]
	public void RegisterProtobuf_ReturnsBuilderForFluentChaining()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<Abstractions.Serialization.ISerializer>._, A<byte>._))
			.Returns(builder);

		// Act
		var result = builder.RegisterProtobuf(opts => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void RegisterProtobuf_RegistersSerializerWithCorrectId()
	{
		// Arrange
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<Abstractions.Serialization.ISerializer>._, A<byte>._))
			.Returns(builder);

		// Act
		builder.RegisterProtobuf(opts => { });

		// Assert
		A.CallTo(() => builder.Register(
			A<Abstractions.Serialization.ISerializer>.That.IsInstanceOf(typeof(ProtobufSerializer)),
			SerializerIds.Protobuf))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void RegisterProtobuf_WithOptions_AppliesWireFormat()
	{
		// Arrange
		ISerializer? capturedSerializer = null;
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<ISerializer>._, A<byte>._))
			.Invokes((ISerializer s, byte _) => capturedSerializer = s)
			.Returns(builder);

		// Act
		builder.RegisterProtobuf(opts => opts.WireFormat = ProtobufWireFormat.Json);

		// Assert -- ContentType is the public observable behavior that changes with WireFormat
		capturedSerializer.ShouldNotBeNull();
		capturedSerializer.ShouldBeOfType<ProtobufSerializer>();
		capturedSerializer.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void RegisterProtobuf_WithoutOptions_UsesDefaults()
	{
		// Arrange
		ISerializer? capturedSerializer = null;
		var builder = A.Fake<ISerializationBuilder>();
		A.CallTo(() => builder.Register(A<ISerializer>._, A<byte>._))
			.Invokes((ISerializer s, byte _) => capturedSerializer = s)
			.Returns(builder);

		// Act -- parameterless overload
		builder.RegisterProtobuf();

		// Assert -- default wire format is Binary, so ContentType should be protobuf
		capturedSerializer.ShouldNotBeNull();
		capturedSerializer.ShouldBeOfType<ProtobufSerializer>();
		capturedSerializer.ContentType.ShouldBe("application/x-protobuf");
	}

	#endregion
}

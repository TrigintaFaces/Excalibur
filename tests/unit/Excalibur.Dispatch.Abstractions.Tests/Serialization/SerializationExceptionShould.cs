// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="SerializationException"/> and <see cref="SerializationOperation"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Serialization")]
[Trait("Priority", "0")]
public sealed class SerializationExceptionShould
{
	#region SerializationOperation Enum Tests

	[Fact]
	public void SerializationOperation_None_HasExpectedValue()
	{
		// Assert
		((int)SerializationOperation.None).ShouldBe(0);
	}

	[Fact]
	public void SerializationOperation_Serialize_HasExpectedValue()
	{
		// Assert
		((int)SerializationOperation.Serialize).ShouldBe(1);
	}

	[Fact]
	public void SerializationOperation_Deserialize_HasExpectedValue()
	{
		// Assert
		((int)SerializationOperation.Deserialize).ShouldBe(2);
	}

	[Fact]
	public void SerializationOperation_HasExactlyThreeValues()
	{
		// Arrange
		var values = Enum.GetValues<SerializationOperation>();

		// Assert
		values.Length.ShouldBe(3);
	}

	#endregion

	#region Default Constructor Tests

	[Fact]
	public void DefaultConstructor_SetsDefaultMessage()
	{
		// Act
		var ex = new SerializationException();

		// Assert
		ex.Message.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void DefaultConstructor_SetsStatusCode()
	{
		// Act
		var ex = new SerializationException();

		// Assert
		ex.StatusCode.ShouldBe(400);
	}

	#endregion

	#region Message Constructor Tests

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		// Act
		var ex = new SerializationException("Custom message");

		// Assert
		ex.Message.ShouldBe("Custom message");
	}

	[Fact]
	public void MessageConstructor_SetsStatusCode()
	{
		// Act
		var ex = new SerializationException("Custom message");

		// Assert
		ex.StatusCode.ShouldBe(400);
	}

	#endregion

	#region InnerException Constructor Tests

	[Fact]
	public void InnerExceptionConstructor_SetsMessage()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Inner");

		// Act
		var ex = new SerializationException("Outer", innerEx);

		// Assert
		ex.Message.ShouldBe("Outer");
	}

	[Fact]
	public void InnerExceptionConstructor_SetsInnerException()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Inner");

		// Act
		var ex = new SerializationException("Outer", innerEx);

		// Assert
		ex.InnerException.ShouldBe(innerEx);
	}

	[Fact]
	public void InnerExceptionConstructor_SetsStatusCode()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Inner");

		// Act
		var ex = new SerializationException("Outer", innerEx);

		// Assert
		ex.StatusCode.ShouldBe(400);
	}

	#endregion

	#region Property Init Tests

	[Fact]
	public void SerializerId_CanBeInitialized()
	{
		// Act
		var ex = new SerializationException("Test") { SerializerId = 0x42 };

		// Assert
		ex.SerializerId.ShouldBe((byte)0x42);
	}

	[Fact]
	public void TargetType_CanBeInitialized()
	{
		// Act
		var ex = new SerializationException("Test") { TargetType = typeof(string) };

		// Assert
		ex.TargetType.ShouldBe(typeof(string));
	}

	[Fact]
	public void SerializerName_CanBeInitialized()
	{
		// Act
		var ex = new SerializationException("Test") { SerializerName = "JsonSerializer" };

		// Assert
		ex.SerializerName.ShouldBe("JsonSerializer");
	}

	[Fact]
	public void Operation_CanBeInitialized()
	{
		// Act
		var ex = new SerializationException("Test") { Operation = SerializationOperation.Serialize };

		// Assert
		ex.Operation.ShouldBe(SerializationOperation.Serialize);
	}

	#endregion

	#region UnknownSerializerId Factory Tests

	[Fact]
	public void UnknownSerializerId_SetsMessage()
	{
		// Act
		var ex = SerializationException.UnknownSerializerId(0x42);

		// Assert
		ex.Message.ShouldContain("0x42");
	}

	[Fact]
	public void UnknownSerializerId_SetsSerializerId()
	{
		// Act
		var ex = SerializationException.UnknownSerializerId(0x42);

		// Assert
		ex.SerializerId.ShouldBe((byte)0x42);
	}

	[Fact]
	public void UnknownSerializerId_WithRegisteredSerializers_IncludesInMessage()
	{
		// Act
		var ex = SerializationException.UnknownSerializerId(0x42, "Json, MessagePack");

		// Assert
		ex.Message.ShouldContain("Json, MessagePack");
	}

	#endregion

	#region EmptyPayload Factory Tests

	[Fact]
	public void EmptyPayload_ReturnsException()
	{
		// Act
		var ex = SerializationException.EmptyPayload();

		// Assert
		_ = ex.ShouldNotBeNull();
		ex.Message.ShouldNotBeNullOrEmpty();
	}

	#endregion

	#region NullResult Factory Tests

	[Fact]
	public void NullResult_SetsMessage()
	{
		// Act
		var ex = SerializationException.NullResult<string>();

		// Assert
		ex.Message.ShouldContain("System.String");
	}

	[Fact]
	public void NullResult_SetsTargetType()
	{
		// Act
		var ex = SerializationException.NullResult<string>();

		// Assert
		ex.TargetType.ShouldBe(typeof(string));
	}

	[Fact]
	public void NullResultForType_SetsMessage()
	{
		// Act
		var ex = SerializationException.NullResultForType(typeof(int));

		// Assert
		ex.Message.ShouldContain("System.Int32");
	}

	[Fact]
	public void NullResultForType_SetsTargetType()
	{
		// Act
		var ex = SerializationException.NullResultForType(typeof(int));

		// Assert
		ex.TargetType.ShouldBe(typeof(int));
	}

	#endregion

	#region Wrap Factory Tests

	[Fact]
	public void Wrap_SetsMessage()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Inner error");

		// Act
		var ex = SerializationException.Wrap<string>("serialize", innerEx);

		// Assert
		ex.Message.ShouldContain("serialize");
		ex.Message.ShouldContain("System.String");
		ex.Message.ShouldContain("Inner error");
	}

	[Fact]
	public void Wrap_SetsInnerException()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Inner error");

		// Act
		var ex = SerializationException.Wrap<string>("serialize", innerEx);

		// Assert
		ex.InnerException.ShouldBe(innerEx);
	}

	[Fact]
	public void Wrap_SetsTargetType()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Inner error");

		// Act
		var ex = SerializationException.Wrap<string>("serialize", innerEx);

		// Assert
		ex.TargetType.ShouldBe(typeof(string));
	}

	[Fact]
	public void WrapObject_SetsMessage()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Inner error");

		// Act
		var ex = SerializationException.WrapObject(typeof(int), "deserialize", innerEx);

		// Assert
		ex.Message.ShouldContain("deserialize");
		ex.Message.ShouldContain("System.Int32");
	}

	[Fact]
	public void WrapObject_SetsTargetType()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Inner error");

		// Act
		var ex = SerializationException.WrapObject(typeof(int), "deserialize", innerEx);

		// Assert
		ex.TargetType.ShouldBe(typeof(int));
	}

	#endregion

	#region UnknownFormat Factory Tests

	[Fact]
	public void UnknownFormat_SetsMessage()
	{
		// Act
		var ex = SerializationException.UnknownFormat(0xAB);

		// Assert
		ex.Message.ShouldContain("0xAB");
		ex.Message.ShouldContain("Unknown payload format");
	}

	[Fact]
	public void UnknownFormat_SetsSerializerId()
	{
		// Act
		var ex = SerializationException.UnknownFormat(0xAB);

		// Assert
		ex.SerializerId.ShouldBe((byte)0xAB);
	}

	#endregion

	#region FormatNotSupported Factory Tests

	[Fact]
	public void FormatNotSupported_SetsMessage()
	{
		// Act
		var ex = SerializationException.FormatNotSupported("Avro");

		// Assert
		ex.Message.ShouldContain("Avro");
		ex.Message.ShouldContain("not supported");
	}

	[Fact]
	public void FormatNotSupported_WithSuggestion_IncludesSuggestion()
	{
		// Act
		var ex = SerializationException.FormatNotSupported("Avro", "Consider using JSON instead.");

		// Assert
		ex.Message.ShouldContain("Consider using JSON instead.");
	}

	#endregion

	#region SerializationFailed Factory Tests

	[Fact]
	public void SerializationFailed_SetsMessage()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Failed");

		// Act
		var ex = SerializationException.SerializationFailed(typeof(string), 0x01, "JsonSerializer", innerEx);

		// Assert
		ex.Message.ShouldContain("serialize");
		ex.Message.ShouldContain("System.String");
		ex.Message.ShouldContain("JsonSerializer");
	}

	[Fact]
	public void SerializationFailed_SetsAllProperties()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Failed");

		// Act
		var ex = SerializationException.SerializationFailed(typeof(string), 0x01, "JsonSerializer", innerEx);

		// Assert
		ex.TargetType.ShouldBe(typeof(string));
		ex.SerializerId.ShouldBe((byte)0x01);
		ex.SerializerName.ShouldBe("JsonSerializer");
		ex.Operation.ShouldBe(SerializationOperation.Serialize);
		ex.InnerException.ShouldBe(innerEx);
	}

	#endregion

	#region DeserializationFailed Factory Tests

	[Fact]
	public void DeserializationFailed_SetsMessage()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Failed");

		// Act
		var ex = SerializationException.DeserializationFailed(typeof(string), 0x02, "MessagePackSerializer", innerEx);

		// Assert
		ex.Message.ShouldContain("deserialize");
		ex.Message.ShouldContain("System.String");
		ex.Message.ShouldContain("MessagePackSerializer");
	}

	[Fact]
	public void DeserializationFailed_SetsAllProperties()
	{
		// Arrange
		var innerEx = new InvalidOperationException("Failed");

		// Act
		var ex = SerializationException.DeserializationFailed(typeof(string), 0x02, "MessagePackSerializer", innerEx);

		// Assert
		ex.TargetType.ShouldBe(typeof(string));
		ex.SerializerId.ShouldBe((byte)0x02);
		ex.SerializerName.ShouldBe("MessagePackSerializer");
		ex.Operation.ShouldBe(SerializationOperation.Deserialize);
		ex.InnerException.ShouldBe(innerEx);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromApiException()
	{
		// Arrange
		var ex = new SerializationException();

		// Assert
		_ = ex.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void InheritsFromException()
	{
		// Arrange
		var ex = new SerializationException();

		// Assert
		_ = ex.ShouldBeAssignableTo<Exception>();
	}

	#endregion
}

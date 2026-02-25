// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Unit tests for <see cref="DispatchSerializationException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchSerializationExceptionShould
{
	[Fact]
	public void InheritFromDispatchException()
	{
		// Arrange & Act
		var exception = new DispatchSerializationException();

		// Assert
		_ = exception.ShouldBeAssignableTo<DispatchException>();
		_ = exception.ShouldBeAssignableTo<ApiException>();
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void HaveDefaultConstructor()
	{
		// Arrange & Act
		var exception = new DispatchSerializationException();

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.SerializationFailed);
		exception.StatusCode.ShouldBe(400);
		exception.Message.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void AcceptMessage()
	{
		// Arrange
		const string message = "Custom serialization error";

		// Act
		var exception = new DispatchSerializationException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.ErrorCode.ShouldBe(ErrorCodes.SerializationFailed);
		exception.StatusCode.ShouldBe(400);
	}

	[Fact]
	public void AcceptMessageAndInnerException()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");
		const string message = "Serialization error";

		// Act
		var exception = new DispatchSerializationException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
		exception.ErrorCode.ShouldBe(ErrorCodes.SerializationFailed);
		exception.StatusCode.ShouldBe(400);
	}

	[Fact]
	public void AcceptErrorCodeAndMessage()
	{
		// Arrange
		const string errorCode = "CUSTOM_SERIALIZATION";
		const string message = "Custom serialization error";

		// Act
		var exception = new DispatchSerializationException(errorCode, message);

		// Assert
		exception.ErrorCode.ShouldBe(errorCode);
		exception.Message.ShouldBe(message);
		exception.StatusCode.ShouldBe(400);
	}

	[Fact]
	public void AcceptErrorCodeMessageAndInnerException()
	{
		// Arrange
		const string errorCode = "CUSTOM_SERIALIZATION";
		const string message = "Custom serialization error";
		var innerException = new ArgumentNullException("param");

		// Act
		var exception = new DispatchSerializationException(errorCode, message, innerException);

		// Assert
		exception.ErrorCode.ShouldBe(errorCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
		exception.StatusCode.ShouldBe(400);
	}

	[Fact]
	public void HaveNullPropertiesByDefault()
	{
		// Arrange & Act
		var exception = new DispatchSerializationException();

		// Assert
		exception.SerializerId.ShouldBeNull();
		exception.TargetType.ShouldBeNull();
		exception.SerializerName.ShouldBeNull();
		exception.Operation.ShouldBe(default(SerializationOperation));
	}

	[Fact]
	public void AllowSettingSerializerId()
	{
		// Arrange & Act
		var exception = new DispatchSerializationException
		{
			SerializerId = 5,
		};

		// Assert
		exception.SerializerId.ShouldBe((byte)5);
	}

	[Fact]
	public void AllowSettingTargetType()
	{
		// Arrange & Act
		var exception = new DispatchSerializationException
		{
			TargetType = typeof(string),
		};

		// Assert
		exception.TargetType.ShouldBe(typeof(string));
	}

	[Fact]
	public void AllowSettingSerializerName()
	{
		// Arrange & Act
		var exception = new DispatchSerializationException
		{
			SerializerName = "SystemTextJson",
		};

		// Assert
		exception.SerializerName.ShouldBe("SystemTextJson");
	}

	[Fact]
	public void AllowSettingOperation()
	{
		// Arrange & Act
		var exception = new DispatchSerializationException
		{
			Operation = SerializationOperation.Serialize,
		};

		// Assert
		exception.Operation.ShouldBe(SerializationOperation.Serialize);
	}

	[Fact]
	public void SupportAllSerializationOperations()
	{
		// Arrange & Act & Assert
		var serializeException = new DispatchSerializationException { Operation = SerializationOperation.Serialize };
		serializeException.Operation.ShouldBe(SerializationOperation.Serialize);

		var deserializeException = new DispatchSerializationException { Operation = SerializationOperation.Deserialize };
		deserializeException.Operation.ShouldBe(SerializationOperation.Deserialize);
	}

	[Fact]
	public void SupportFullInitialization()
	{
		// Arrange & Act
		var exception = new DispatchSerializationException("Failed to serialize")
		{
			SerializerId = 1,
			TargetType = typeof(TestPayload),
			SerializerName = "MessagePack",
			Operation = SerializationOperation.Serialize,
		};

		// Assert
		exception.Message.ShouldBe("Failed to serialize");
		exception.SerializerId.ShouldBe((byte)1);
		exception.TargetType.ShouldBe(typeof(TestPayload));
		exception.SerializerName.ShouldBe("MessagePack");
		exception.Operation.ShouldBe(SerializationOperation.Serialize);
		exception.StatusCode.ShouldBe(400);
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(DispatchSerializationException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void BeCatchableAsDispatchException()
	{
		// Arrange
		var exception = new DispatchSerializationException("Test error");

		// Act & Assert
		try
		{
			throw exception;
		}
		catch (DispatchException caught)
		{
			caught.ShouldBe(exception);
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(127)]
	[InlineData(255)]
	public void AcceptVariousSerializerIds(byte serializerId)
	{
		// Arrange & Act
		var exception = new DispatchSerializationException
		{
			SerializerId = serializerId,
		};

		// Assert
		exception.SerializerId.ShouldBe(serializerId);
	}

	[Theory]
	[InlineData(typeof(string))]
	[InlineData(typeof(int))]
	[InlineData(typeof(TestPayload))]
	[InlineData(typeof(List<string>))]
	public void AcceptVariousTargetTypes(Type targetType)
	{
		// Arrange & Act
		var exception = new DispatchSerializationException
		{
			TargetType = targetType,
		};

		// Assert
		exception.TargetType.ShouldBe(targetType);
	}

	[Fact]
	public void PreserveInnerExceptionDetails()
	{
		// Arrange
		var jsonException = new System.Text.Json.JsonException("Invalid JSON at position 42");
		const string message = "Failed to deserialize payload";

		// Act
		var exception = new DispatchSerializationException(message, jsonException)
		{
			SerializerName = "SystemTextJson",
			Operation = SerializationOperation.Deserialize,
		};

		// Assert
		exception.InnerException.ShouldBe(jsonException);
		exception.InnerException.Message.ShouldContain("position 42");
	}

	[Fact]
	public void AllowNullInnerException()
	{
		// Arrange & Act
		var exception = new DispatchSerializationException("Error", (Exception?)null);

		// Assert
		exception.InnerException.ShouldBeNull();
		exception.Message.ShouldBe("Error");
	}

	// Helper class for testing
	private sealed class TestPayload
	{
		public string? Name { get; set; }
		public int Value { get; set; }
	}
}

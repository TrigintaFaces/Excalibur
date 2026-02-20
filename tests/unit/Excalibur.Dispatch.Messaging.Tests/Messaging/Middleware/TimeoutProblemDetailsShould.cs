// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for TimeoutProblemDetails.
/// </summary>
/// <remarks>
/// Tests the problem details for timeout errors.
/// Note: TimeoutProblemDetails is internal, so we use reflection to test it.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class TimeoutProblemDetailsShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithException_SetsTitle()
	{
		// Arrange
		var exception = new MessageTimeoutException("Test timeout message");
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		var title = GetProperty<string>(problemDetails, "Title");

		// Assert
		title.ShouldBe("Operation Timeout");
	}

	[Fact]
	public void Constructor_WithException_SetsType()
	{
		// Arrange
		var exception = new MessageTimeoutException("Test timeout message");
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		var type = GetProperty<string>(problemDetails, "Type");

		// Assert
		type.ShouldBe("timeout");
	}

	[Fact]
	public void Constructor_WithException_SetsDetailFromMessage()
	{
		// Arrange
		var exception = new MessageTimeoutException("Processing exceeded 30 seconds");
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		var detail = GetProperty<string>(problemDetails, "Detail");

		// Assert
		detail.ShouldBe("Processing exceeded 30 seconds");
	}

	[Fact]
	public void Constructor_WithException_SetsErrorCodeTo504()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		var errorCode = GetProperty<int>(problemDetails, "ErrorCode");

		// Assert
		errorCode.ShouldBe(504);
	}

	[Fact]
	public void Constructor_WithException_SetsInstanceFromMessageId()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout")
		{
			MessageId = "msg-12345",
		};
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		var instance = GetProperty<string>(problemDetails, "Instance");

		// Assert
		instance.ShouldBe("/message/msg-12345");
	}

	[Fact]
	public void Constructor_WithNullMessageId_SetsInstanceWithNull()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout")
		{
			MessageId = null,
		};
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		var instance = GetProperty<string>(problemDetails, "Instance");

		// Assert
		instance.ShouldBe("/message/");
	}

	#endregion

	#region Extensions Tests

	[Fact]
	public void Constructor_WithException_SetsTimeoutExceededExtension()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		var extensions = GetProperty<IDictionary<string, object?>>(problemDetails, "Extensions");

		// Assert
		extensions.ShouldContainKey("TimeoutExceeded");
		extensions["TimeoutExceeded"].ShouldBe(true);
	}

	[Fact]
	public void Constructor_WithException_SetsResultTypeExtension()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		var extensions = GetProperty<IDictionary<string, object?>>(problemDetails, "Extensions");

		// Assert
		extensions.ShouldContainKey("ResultType");
		extensions["ResultType"].ShouldBe("Timeout");
	}

	[Fact]
	public void Constructor_WithException_SetsElapsedTimeExtension()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout")
		{
			ElapsedTime = TimeSpan.FromSeconds(35),
		};
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		var extensions = GetProperty<IDictionary<string, object?>>(problemDetails, "Extensions");

		// Assert
		extensions.ShouldContainKey("ElapsedTime");
		extensions["ElapsedTime"].ShouldBe(TimeSpan.FromSeconds(35));
	}

	[Fact]
	public void Constructor_WithException_SetsTimeoutDurationExtension()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout")
		{
			TimeoutDuration = TimeSpan.FromSeconds(30),
		};
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		var extensions = GetProperty<IDictionary<string, object?>>(problemDetails, "Extensions");

		// Assert
		extensions.ShouldContainKey("TimeoutDuration");
		extensions["TimeoutDuration"].ShouldBe(TimeSpan.FromSeconds(30));
	}

	#endregion

	#region ToString Tests

	[Fact]
	public void ToString_ReturnsExceptionMessage()
	{
		// Arrange
		var exception = new MessageTimeoutException("Operation timed out after 30 seconds");
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		var result = problemDetails.ToString();

		// Assert
		result.ShouldBe("Operation timed out after 30 seconds");
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIMessageProblemDetails()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Assert
		_ = problemDetails.ShouldBeAssignableTo<IMessageProblemDetails>();
	}

	#endregion

	#region Property Modification Tests

	[Fact]
	public void Title_CanBeModified()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		SetProperty(problemDetails, "Title", "Custom Timeout Title");
		var title = GetProperty<string>(problemDetails, "Title");

		// Assert
		title.ShouldBe("Custom Timeout Title");
	}

	[Fact]
	public void Type_CanBeModified()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		SetProperty(problemDetails, "Type", "custom-timeout");
		var type = GetProperty<string>(problemDetails, "Type");

		// Assert
		type.ShouldBe("custom-timeout");
	}

	[Fact]
	public void ErrorCode_CanBeModified()
	{
		// Arrange
		var exception = new MessageTimeoutException("Timeout");
		var problemDetails = CreateTimeoutProblemDetails(exception);

		// Act
		SetProperty(problemDetails, "ErrorCode", 408);
		var errorCode = GetProperty<int>(problemDetails, "ErrorCode");

		// Assert
		errorCode.ShouldBe(408);
	}

	#endregion

	#region Helper Methods

	private static object CreateTimeoutProblemDetails(MessageTimeoutException exception)
	{
		var type = typeof(MessageTimeoutException).Assembly
			.GetType("Excalibur.Dispatch.Middleware.TimeoutProblemDetails");
		_ = type.ShouldNotBeNull();

		var instance = Activator.CreateInstance(type, exception);
		_ = instance.ShouldNotBeNull();
		return instance;
	}

	private static T GetProperty<T>(object obj, string propertyName)
	{
		var property = obj.GetType().GetProperty(propertyName);
		_ = property.ShouldNotBeNull();
		return (T)property.GetValue(obj)!;
	}

	private static void SetProperty(object obj, string propertyName, object value)
	{
		var property = obj.GetType().GetProperty(propertyName);
		_ = property.ShouldNotBeNull();
		property.SetValue(obj, value);
	}

	#endregion
}

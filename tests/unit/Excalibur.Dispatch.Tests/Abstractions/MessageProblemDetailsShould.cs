// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Abstractions;

/// <summary>
/// Unit tests for <see cref="MessageProblemDetails"/> covering RFC 7807 compliance,
/// factory methods, and extension handling.
/// </summary>
/// <remarks>
/// Sprint 410 - Foundation Coverage Tests (T410.7).
/// Target: Increase MessageProblemDetails coverage from 41.1% to 80%.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class MessageProblemDetailsShould
{
	#region Default Constructor Tests

	[Fact]
	public void DefaultConstructor_Should_Set_Type_To_AboutBlank()
	{
		// Act
		var problemDetails = new MessageProblemDetails();

		// Assert
		problemDetails.Type.ShouldBe("about:blank");
	}

	[Fact]
	public void DefaultConstructor_Should_Set_Title_To_Error()
	{
		// Act
		var problemDetails = new MessageProblemDetails();

		// Assert
		problemDetails.Title.ShouldBe("Error");
	}

	[Fact]
	public void DefaultConstructor_Should_Set_ErrorCode_To_Zero()
	{
		// Act
		var problemDetails = new MessageProblemDetails();

		// Assert
		problemDetails.ErrorCode.ShouldBe(0);
	}

	[Fact]
	public void DefaultConstructor_Should_Set_Status_To_Null()
	{
		// Act
		var problemDetails = new MessageProblemDetails();

		// Assert
		problemDetails.Status.ShouldBeNull();
	}

	[Fact]
	public void DefaultConstructor_Should_Set_Detail_To_Empty()
	{
		// Act
		var problemDetails = new MessageProblemDetails();

		// Assert
		problemDetails.Detail.ShouldBeEmpty();
	}

	[Fact]
	public void DefaultConstructor_Should_Set_Instance_To_Empty()
	{
		// Act
		var problemDetails = new MessageProblemDetails();

		// Assert
		problemDetails.Instance.ShouldBeEmpty();
	}

	[Fact]
	public void DefaultConstructor_Should_Initialize_Extensions_Dictionary()
	{
		// Act
		var problemDetails = new MessageProblemDetails();

		// Assert
		_ = problemDetails.Extensions.ShouldNotBeNull();
		problemDetails.Extensions.ShouldBeEmpty();
	}

	#endregion

	#region Property Get/Set Tests

	[Fact]
	public void Should_Get_And_Set_Type_Property()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails();

		// Act
		problemDetails.Type = "custom-error-type";

		// Assert
		problemDetails.Type.ShouldBe("custom-error-type");
	}

	[Fact]
	public void Should_Get_And_Set_Title_Property()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails();

		// Act
		problemDetails.Title = "Custom Error Title";

		// Assert
		problemDetails.Title.ShouldBe("Custom Error Title");
	}

	[Fact]
	public void Should_Get_And_Set_ErrorCode_Property()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails();

		// Act
		problemDetails.ErrorCode = 1001;

		// Assert
		problemDetails.ErrorCode.ShouldBe(1001);
	}

	[Fact]
	public void Should_Get_And_Set_Status_Property()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails();

		// Act
		problemDetails.Status = 422;

		// Assert
		problemDetails.Status.ShouldBe(422);
	}

	[Fact]
	public void Should_Get_And_Set_Detail_Property()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails();

		// Act
		problemDetails.Detail = "A detailed error description.";

		// Assert
		problemDetails.Detail.ShouldBe("A detailed error description.");
	}

	[Fact]
	public void Should_Get_And_Set_Instance_Property()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails();

		// Act
		problemDetails.Instance = "/api/orders/12345";

		// Assert
		problemDetails.Instance.ShouldBe("/api/orders/12345");
	}

	[Fact]
	public void Should_Get_And_Set_Extensions_Property()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails();
		var extensions = new Dictionary<string, object?> { ["customField"] = "customValue" };

		// Act
		problemDetails.Extensions = extensions;

		// Assert
		problemDetails.Extensions.ShouldBe(extensions);
	}

	#endregion

	#region Extensions Dictionary Tests

	[Fact]
	public void Extensions_Should_Allow_Adding_Custom_Properties()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails();

		// Act
		problemDetails.Extensions["traceId"] = "abc-123";
		problemDetails.Extensions["errorCount"] = 5;

		// Assert
		problemDetails.Extensions.Count.ShouldBe(2);
		problemDetails.Extensions["traceId"].ShouldBe("abc-123");
		problemDetails.Extensions["errorCount"].ShouldBe(5);
	}

	[Fact]
	public void Extensions_Should_Allow_Null_Values()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails();

		// Act
		problemDetails.Extensions["nullableField"] = null;

		// Assert
		problemDetails.Extensions.ShouldContainKey("nullableField");
		problemDetails.Extensions["nullableField"].ShouldBeNull();
	}

	[Fact]
	public void Extensions_Should_Use_Ordinal_String_Comparer()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails();

		// Act
		problemDetails.Extensions["Key"] = "value1";
		problemDetails.Extensions["key"] = "value2";

		// Assert - Ordinal comparer means case-sensitive keys
		problemDetails.Extensions.Count.ShouldBe(2);
		problemDetails.Extensions["Key"].ShouldBe("value1");
		problemDetails.Extensions["key"].ShouldBe("value2");
	}

	#endregion

	#region ValidationError Factory Method Tests

	[Fact]
	public void ValidationError_Should_Set_Type_To_ValidationError()
	{
		// Act
		var problemDetails = MessageProblemDetails.ValidationError("Field is required");

		// Assert
		problemDetails.Type.ShouldBe("validation-error");
	}

	[Fact]
	public void ValidationError_Should_Set_Title_To_ValidationError()
	{
		// Act
		var problemDetails = MessageProblemDetails.ValidationError("Field is required");

		// Assert
		problemDetails.Title.ShouldBe("Validation Error");
	}

	[Fact]
	public void ValidationError_Should_Set_Detail_From_Parameter()
	{
		// Act
		var problemDetails = MessageProblemDetails.ValidationError("Email format is invalid");

		// Assert
		problemDetails.Detail.ShouldBe("Email format is invalid");
	}

	[Fact]
	public void ValidationError_Should_Set_Status_To_400()
	{
		// Act
		var problemDetails = (MessageProblemDetails)MessageProblemDetails.ValidationError("Field is required");

		// Assert
		problemDetails.Status.ShouldBe(400);
	}

	[Fact]
	public void ValidationError_Should_Return_IMessageProblemDetails_Interface()
	{
		// Act
		var problemDetails = MessageProblemDetails.ValidationError("Detail");

		// Assert
		_ = problemDetails.ShouldBeAssignableTo<IMessageProblemDetails>();
	}

	#endregion

	#region AuthorizationError Factory Method Tests

	[Fact]
	public void AuthorizationError_Should_Set_Type_To_AuthorizationError()
	{
		// Act
		var problemDetails = MessageProblemDetails.AuthorizationError("Access denied");

		// Assert
		problemDetails.Type.ShouldBe("authorization-error");
	}

	[Fact]
	public void AuthorizationError_Should_Set_Title_To_AuthorizationError()
	{
		// Act
		var problemDetails = MessageProblemDetails.AuthorizationError("Access denied");

		// Assert
		problemDetails.Title.ShouldBe("Authorization Error");
	}

	[Fact]
	public void AuthorizationError_Should_Set_Detail_From_Parameter()
	{
		// Act
		var problemDetails = MessageProblemDetails.AuthorizationError("User lacks admin role");

		// Assert
		problemDetails.Detail.ShouldBe("User lacks admin role");
	}

	[Fact]
	public void AuthorizationError_Should_Set_Status_To_403()
	{
		// Act
		var problemDetails = (MessageProblemDetails)MessageProblemDetails.AuthorizationError("Access denied");

		// Assert
		problemDetails.Status.ShouldBe(403);
	}

	#endregion

	#region NotFound Factory Method Tests

	[Fact]
	public void NotFound_Should_Set_Type_To_NotFound()
	{
		// Act
		var problemDetails = MessageProblemDetails.NotFound("Resource not found");

		// Assert
		problemDetails.Type.ShouldBe("not-found");
	}

	[Fact]
	public void NotFound_Should_Set_Title_To_NotFound()
	{
		// Act
		var problemDetails = MessageProblemDetails.NotFound("Resource not found");

		// Assert
		problemDetails.Title.ShouldBe("Not Found");
	}

	[Fact]
	public void NotFound_Should_Set_Detail_From_Parameter()
	{
		// Act
		var problemDetails = MessageProblemDetails.NotFound("Order with ID 12345 not found");

		// Assert
		problemDetails.Detail.ShouldBe("Order with ID 12345 not found");
	}

	[Fact]
	public void NotFound_Should_Set_Status_To_404()
	{
		// Act
		var problemDetails = (MessageProblemDetails)MessageProblemDetails.NotFound("Resource not found");

		// Assert
		problemDetails.Status.ShouldBe(404);
	}

	#endregion

	#region InternalError Factory Method Tests

	[Fact]
	public void InternalError_Should_Set_Type_To_InternalError()
	{
		// Act
		var problemDetails = MessageProblemDetails.InternalError("Server error");

		// Assert
		problemDetails.Type.ShouldBe("internal-error");
	}

	[Fact]
	public void InternalError_Should_Set_Title_To_InternalError()
	{
		// Act
		var problemDetails = MessageProblemDetails.InternalError("Server error");

		// Assert
		problemDetails.Title.ShouldBe("Internal Error");
	}

	[Fact]
	public void InternalError_Should_Set_Detail_From_Parameter()
	{
		// Act
		var problemDetails = MessageProblemDetails.InternalError("Database connection failed");

		// Assert
		problemDetails.Detail.ShouldBe("Database connection failed");
	}

	[Fact]
	public void InternalError_Should_Set_Status_To_500()
	{
		// Act
		var problemDetails = (MessageProblemDetails)MessageProblemDetails.InternalError("Server error");

		// Assert
		problemDetails.Status.ShouldBe(500);
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void Should_Implement_IMessageProblemDetails_Interface()
	{
		// Arrange & Act
		var problemDetails = new MessageProblemDetails();

		// Assert
		_ = problemDetails.ShouldBeAssignableTo<IMessageProblemDetails>();
	}

	[Fact]
	public void Interface_Should_Expose_All_RFC7807_Properties()
	{
		// Arrange
		var concreteDetails = new MessageProblemDetails
		{
			Type = "test-type",
			Title = "Test Title",
			Status = 418,
			Detail = "I'm a teapot",
			Instance = "/api/teapot"
		};
		IMessageProblemDetails problemDetails = concreteDetails;

		// Assert - interface properties
		problemDetails.Type.ShouldBe("test-type");
		problemDetails.Title.ShouldBe("Test Title");
		problemDetails.Detail.ShouldBe("I'm a teapot");
		problemDetails.Instance.ShouldBe("/api/teapot");
		// Status is not on the interface, verify via concrete type
		concreteDetails.Status.ShouldBe(418);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void Should_Support_Object_Initializer_Syntax()
	{
		// Act
		var problemDetails = new MessageProblemDetails
		{
			Type = "custom-type",
			Title = "Custom Title",
			ErrorCode = 2001,
			Status = 502,
			Detail = "Bad gateway",
			Instance = "/api/gateway/123",
			Extensions = new Dictionary<string, object?> { ["region"] = "us-west-2" }
		};

		// Assert
		problemDetails.Type.ShouldBe("custom-type");
		problemDetails.Title.ShouldBe("Custom Title");
		problemDetails.ErrorCode.ShouldBe(2001);
		problemDetails.Status.ShouldBe(502);
		problemDetails.Detail.ShouldBe("Bad gateway");
		problemDetails.Instance.ShouldBe("/api/gateway/123");
		problemDetails.Extensions["region"].ShouldBe("us-west-2");
	}

	#endregion
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Hosting.AspNetCore;

using FakeItEasy;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Excalibur.Dispatch.Hosting.Tests.AspNetCore;

/// <summary>
/// Unit tests for <see cref="MessageResultExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
public sealed class MessageResultExtensionsShould : UnitTestBase
{
	#region ToHttpResult (IMessageResult) Tests

	[Fact]
	public void ToHttpResult_WithNullMessageResult_ThrowsArgumentNullException()
	{
		// Arrange
		IMessageResult? messageResult = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => messageResult.ToHttpResult());
	}

	[Fact]
	public void ToHttpResult_WhenSucceeded_ReturnsAccepted()
	{
		// Arrange
		var messageResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(true);

		// Act
		var result = messageResult.ToHttpResult();

		// Assert
		result.ShouldBeOfType<Accepted>();
	}

	[Fact]
	public void ToHttpResult_WhenAuthorizationFailed_ReturnsForbid()
	{
		// Arrange
		var authResult = A.Fake<IAuthorizationResult>();
		_ = A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var messageResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(authResult);

		// Act
		var result = messageResult.ToHttpResult();

		// Assert
		result.ShouldBeOfType<ForbidHttpResult>();
	}

	[Fact]
	public void ToHttpResult_WhenValidationFailed_ReturnsBadRequest()
	{
		// Arrange
		var validationResult = new TestValidationResult { IsValid = false };

		var messageResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(null);
		_ = A.CallTo(() => messageResult.ValidationResult).Returns(validationResult);

		// Act
		var result = messageResult.ToHttpResult();

		// Assert - Check it's a BadRequest (generic type is IValidationResult but we can't use interface as type param)
		result.GetType().Name.ShouldStartWith("BadRequest");
	}

	[Fact]
	public void ToHttpResult_WhenFailedWithoutValidation_ReturnsProblem()
	{
		// Arrange
		var messageResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(null);
		_ = A.CallTo(() => messageResult.ValidationResult).Returns(null);

		// Act
		var result = messageResult.ToHttpResult();

		// Assert
		result.ShouldBeOfType<ProblemHttpResult>();
	}

	#endregion

	#region ToHttpResult<TResult> (IMessageResult<TResult>) Tests

	[Fact]
	public void ToHttpResultGeneric_WithNullMessageResult_ThrowsArgumentNullException()
	{
		// Arrange
		IMessageResult<string>? messageResult = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => messageResult.ToHttpResult());
	}

	[Fact]
	public void ToHttpResultGeneric_WhenSucceeded_ReturnsOkWithValue()
	{
		// Arrange
		var expectedValue = "test-result";
		var messageResult = A.Fake<IMessageResult<string>>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(true);
		_ = A.CallTo(() => messageResult.ReturnValue).Returns(expectedValue);

		// Act
		var result = messageResult.ToHttpResult();

		// Assert
		var okResult = result.ShouldBeOfType<Ok<string>>();
		okResult.Value.ShouldBe(expectedValue);
	}

	[Fact]
	public void ToHttpResultGeneric_WhenAuthorizationFailed_ReturnsForbid()
	{
		// Arrange
		var authResult = A.Fake<IAuthorizationResult>();
		_ = A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var messageResult = A.Fake<IMessageResult<string>>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(authResult);

		// Act
		var result = messageResult.ToHttpResult();

		// Assert
		result.ShouldBeOfType<ForbidHttpResult>();
	}

	[Fact]
	public void ToHttpResultGeneric_WhenValidationFailed_ReturnsBadRequest()
	{
		// Arrange
		var validationResult = new TestValidationResult { IsValid = false };

		var messageResult = A.Fake<IMessageResult<string>>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(null);
		_ = A.CallTo(() => messageResult.ValidationResult).Returns(validationResult);

		// Act
		var result = messageResult.ToHttpResult();

		// Assert - Check it's a BadRequest (generic type is IValidationResult but we can't use interface as type param)
		result.GetType().Name.ShouldStartWith("BadRequest");
	}

	[Fact]
	public void ToHttpResultGeneric_WhenFailedWithoutValidation_ReturnsProblem()
	{
		// Arrange
		var messageResult = A.Fake<IMessageResult<string>>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(null);
		_ = A.CallTo(() => messageResult.ValidationResult).Returns(null);

		// Act
		var result = messageResult.ToHttpResult();

		// Assert
		result.ShouldBeOfType<ProblemHttpResult>();
	}

	#endregion

	#region ToHttpActionResult (IMessageResult) Tests

	[Fact]
	public void ToHttpActionResult_WithNullController_ThrowsArgumentNullException()
	{
		// Arrange
		ControllerBase? controller = null;
		var messageResult = A.Fake<IMessageResult>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => controller.ToHttpActionResult(messageResult));
	}

	[Fact]
	public void ToHttpActionResult_WithNullMessageResult_ThrowsArgumentNullException()
	{
		// Arrange
		var controller = CreateFakeController();
		IMessageResult? messageResult = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => controller.ToHttpActionResult(messageResult));
	}

	[Fact]
	public void ToHttpActionResult_WhenSucceeded_ReturnsAccepted()
	{
		// Arrange
		var controller = CreateFakeController();
		var messageResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(true);

		// Act
		var result = controller.ToHttpActionResult(messageResult);

		// Assert
		result.ShouldBeOfType<AcceptedResult>();
	}

	[Fact]
	public void ToHttpActionResult_WhenAuthorizationFailed_ReturnsForbid()
	{
		// Arrange
		var controller = CreateFakeController();
		var authResult = A.Fake<IAuthorizationResult>();
		_ = A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var messageResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(authResult);

		// Act
		var result = controller.ToHttpActionResult(messageResult);

		// Assert
		result.ShouldBeOfType<ForbidResult>();
	}

	[Fact]
	public void ToHttpActionResult_WhenValidationFailed_ReturnsBadRequest()
	{
		// Arrange
		var controller = CreateFakeController();
		var errors = new List<object> { new ValidationError("field", "error") };
		var validationResult = new TestValidationResult { IsValid = false, Errors = errors };

		var messageResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(null);
		_ = A.CallTo(() => messageResult.ValidationResult).Returns(validationResult);

		// Act
		var result = controller.ToHttpActionResult(messageResult);

		// Assert
		result.ShouldBeOfType<BadRequestObjectResult>();
	}

	[Fact]
	public void ToHttpActionResult_WhenFailedWithoutValidation_ReturnsProblem()
	{
		// Arrange
		var controller = CreateFakeController();
		var messageResult = A.Fake<IMessageResult>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(null);
		_ = A.CallTo(() => messageResult.ValidationResult).Returns(null);

		// Act
		var result = controller.ToHttpActionResult(messageResult);

		// Assert
		result.ShouldBeOfType<ObjectResult>();
		var objectResult = (ObjectResult)result;
		objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
	}

	#endregion

	#region ToHttpActionResult<TResult> (IMessageResult<TResult>) Tests

	[Fact]
	public void ToHttpActionResultGeneric_WithNullController_ThrowsArgumentNullException()
	{
		// Arrange
		ControllerBase? controller = null;
		var messageResult = A.Fake<IMessageResult<string>>();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => controller.ToHttpActionResult(messageResult));
	}

	[Fact]
	public void ToHttpActionResultGeneric_WithNullMessageResult_ThrowsArgumentNullException()
	{
		// Arrange
		var controller = CreateFakeController();
		IMessageResult<string>? messageResult = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => controller.ToHttpActionResult(messageResult));
	}

	[Fact]
	public void ToHttpActionResultGeneric_WhenSucceeded_ReturnsOkWithValue()
	{
		// Arrange
		var controller = CreateFakeController();
		var expectedValue = "test-result";
		var messageResult = A.Fake<IMessageResult<string>>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(true);
		_ = A.CallTo(() => messageResult.ReturnValue).Returns(expectedValue);

		// Act
		var result = controller.ToHttpActionResult(messageResult);

		// Assert
		var okResult = result.ShouldBeOfType<OkObjectResult>();
		okResult.Value.ShouldBe(expectedValue);
	}

	[Fact]
	public void ToHttpActionResultGeneric_WhenAuthorizationFailed_ReturnsForbid()
	{
		// Arrange
		var controller = CreateFakeController();
		var authResult = A.Fake<IAuthorizationResult>();
		_ = A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var messageResult = A.Fake<IMessageResult<string>>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(authResult);

		// Act
		var result = controller.ToHttpActionResult(messageResult);

		// Assert
		result.ShouldBeOfType<ForbidResult>();
	}

	[Fact]
	public void ToHttpActionResultGeneric_WhenValidationFailed_ReturnsBadRequest()
	{
		// Arrange
		var controller = CreateFakeController();
		var errors = new List<object> { new ValidationError("field", "error") };
		var validationResult = new TestValidationResult { IsValid = false, Errors = errors };

		var messageResult = A.Fake<IMessageResult<string>>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(null);
		_ = A.CallTo(() => messageResult.ValidationResult).Returns(validationResult);

		// Act
		var result = controller.ToHttpActionResult(messageResult);

		// Assert
		result.ShouldBeOfType<BadRequestObjectResult>();
	}

	[Fact]
	public void ToHttpActionResultGeneric_WhenFailedWithoutValidation_ReturnsProblem()
	{
		// Arrange
		var controller = CreateFakeController();
		var messageResult = A.Fake<IMessageResult<string>>();
		_ = A.CallTo(() => messageResult.Succeeded).Returns(false);
		_ = A.CallTo(() => messageResult.AuthorizationResult).Returns(null);
		_ = A.CallTo(() => messageResult.ValidationResult).Returns(null);

		// Act
		var result = controller.ToHttpActionResult(messageResult);

		// Assert
		result.ShouldBeOfType<ObjectResult>();
		var objectResult = (ObjectResult)result;
		objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
	}

	#endregion

	#region Helpers

	private static TestController CreateFakeController()
	{
		var controller = new TestController();
		controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext()
		};
		return controller;
	}

	private sealed class TestController : ControllerBase;

	/// <summary>
	/// Test stub for IValidationResult since interfaces with static abstract members cannot be faked.
	/// </summary>
	private sealed class TestValidationResult : IValidationResult
	{
		public IReadOnlyCollection<object> Errors { get; set; } = Array.Empty<object>();
		public bool IsValid { get; set; } = true;

		public static IValidationResult Failed(params object[] errors) =>
			new TestValidationResult { IsValid = false, Errors = errors };

		public static IValidationResult Success() =>
			new TestValidationResult { IsValid = true };
	}

	#endregion
}

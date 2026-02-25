// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Hosting.AspNetCore;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Excalibur.Dispatch.Hosting.AspNetCore.Tests;

/// <summary>
/// Tests for <see cref="MessageResultExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class MessageResultExtensionsShould : UnitTestBase
{
	#region ToHttpResult (non-generic)

	[Fact]
	public void ToHttpResult_ThrowWhenMessageResultIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IMessageResult)null!).ToHttpResult());
	}

	[Fact]
	public void ToHttpResult_ReturnAccepted_WhenSucceeded()
	{
		// Arrange
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(true);

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		httpResult.ShouldNotBeNull();
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
	}

	[Fact]
	public void ToHttpResult_ReturnForbid_WhenAuthorizationFailed()
	{
		// Arrange
		var authResult = A.Fake<IAuthorizationResult>();
		A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(authResult);

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		httpResult.ShouldNotBeNull();
		httpResult.ShouldBeOfType<ForbidHttpResult>();
	}

	[Fact]
	public void ToHttpResult_ReturnBadRequest_WhenValidationFailed()
	{
		// Arrange — IValidationResult has static abstract members, cannot be faked
		var validationResult = new TestValidationResult { IsValid = false };

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(validationResult);

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		httpResult.ShouldNotBeNull();
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(400);
	}

	[Fact]
	public void ToHttpResult_ReturnProblem_WhenFailedWithoutValidation()
	{
		// Arrange
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		httpResult.ShouldNotBeNull();
	}

	#endregion

	#region ToHttpResult<T> (generic)

	[Fact]
	public void ToHttpResultGeneric_ThrowWhenMessageResultIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IMessageResult<string>)null!).ToHttpResult());
	}

	[Fact]
	public void ToHttpResultGeneric_ReturnOk_WhenSucceeded()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(true);
		A.CallTo(() => result.ReturnValue).Returns("test-value");

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		httpResult.ShouldNotBeNull();
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(200);
	}

	[Fact]
	public void ToHttpResultGeneric_ReturnForbid_WhenAuthorizationFailed()
	{
		// Arrange
		var authResult = A.Fake<IAuthorizationResult>();
		A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(authResult);

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		httpResult.ShouldNotBeNull();
		httpResult.ShouldBeOfType<ForbidHttpResult>();
	}

	[Fact]
	public void ToHttpResultGeneric_ReturnBadRequest_WhenValidationFailed()
	{
		// Arrange — IValidationResult has static abstract members, cannot be faked
		var validationResult = new TestValidationResult { IsValid = false };

		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(validationResult);

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		httpResult.ShouldNotBeNull();
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(400);
	}

	[Fact]
	public void ToHttpResultGeneric_ReturnProblem_WhenFailedWithoutValidation()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		httpResult.ShouldNotBeNull();
	}

	#endregion

	#region ToHttpActionResult (non-generic, MVC)

	[Fact]
	public void ToHttpActionResult_ThrowWhenMessageResultIsNull()
	{
		// Arrange
		var controller = new TestController();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			controller.ToHttpActionResult((IMessageResult)null!));
	}

	[Fact]
	public void ToHttpActionResult_ThrowWhenControllerIsNull()
	{
		// Arrange
		var result = A.Fake<IMessageResult>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ControllerBase)null!).ToHttpActionResult(result));
	}

	[Fact]
	public void ToHttpActionResult_ReturnAccepted_WhenSucceeded()
	{
		// Arrange
		var controller = new TestController();
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(true);

		// Act
		var actionResult = controller.ToHttpActionResult(result);

		// Assert
		actionResult.ShouldNotBeNull();
		actionResult.ShouldBeOfType<AcceptedResult>();
	}

	[Fact]
	public void ToHttpActionResult_ReturnForbid_WhenAuthorizationFailed()
	{
		// Arrange
		var controller = new TestController();
		var authResult = A.Fake<IAuthorizationResult>();
		A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(authResult);

		// Act
		var actionResult = controller.ToHttpActionResult(result);

		// Assert
		actionResult.ShouldNotBeNull();
		actionResult.ShouldBeOfType<ForbidResult>();
	}

	[Fact]
	public void ToHttpActionResult_ReturnBadRequest_WhenValidationFailed()
	{
		// Arrange
		var controller = new TestController();
		var validationResult = new TestValidationResult { IsValid = false, Errors = ["error1"] };

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(validationResult);

		// Act
		var actionResult = controller.ToHttpActionResult(result);

		// Assert
		actionResult.ShouldNotBeNull();
		actionResult.ShouldBeOfType<BadRequestObjectResult>();
	}

	[Fact]
	public void ToHttpActionResult_ReturnProblem_WhenFailedWithoutValidation()
	{
		// Arrange
		var controller = new TestController();
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);

		// Act
		var actionResult = controller.ToHttpActionResult(result);

		// Assert
		actionResult.ShouldNotBeNull();
		actionResult.ShouldBeOfType<ObjectResult>();
	}

	#endregion

	#region ToHttpActionResult<T> (generic, MVC)

	[Fact]
	public void ToHttpActionResultGeneric_ThrowWhenMessageResultIsNull()
	{
		// Arrange
		var controller = new TestController();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			controller.ToHttpActionResult<string>((IMessageResult<string>)null!));
	}

	[Fact]
	public void ToHttpActionResultGeneric_ThrowWhenControllerIsNull()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((ControllerBase)null!).ToHttpActionResult(result));
	}

	[Fact]
	public void ToHttpActionResultGeneric_ReturnOk_WhenSucceeded()
	{
		// Arrange
		var controller = new TestController();
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(true);
		A.CallTo(() => result.ReturnValue).Returns("test-value");

		// Act
		var actionResult = controller.ToHttpActionResult(result);

		// Assert
		actionResult.ShouldNotBeNull();
		actionResult.ShouldBeOfType<OkObjectResult>();
		((OkObjectResult)actionResult).Value.ShouldBe("test-value");
	}

	[Fact]
	public void ToHttpActionResultGeneric_ReturnForbid_WhenAuthorizationFailed()
	{
		// Arrange
		var controller = new TestController();
		var authResult = A.Fake<IAuthorizationResult>();
		A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(authResult);

		// Act
		var actionResult = controller.ToHttpActionResult(result);

		// Assert
		actionResult.ShouldNotBeNull();
		actionResult.ShouldBeOfType<ForbidResult>();
	}

	[Fact]
	public void ToHttpActionResultGeneric_ReturnBadRequest_WhenValidationFailed()
	{
		// Arrange
		var controller = new TestController();
		var validationResult = new TestValidationResult { IsValid = false, Errors = ["error1"] };

		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(validationResult);

		// Act
		var actionResult = controller.ToHttpActionResult(result);

		// Assert
		actionResult.ShouldNotBeNull();
		actionResult.ShouldBeOfType<BadRequestObjectResult>();
	}

	[Fact]
	public void ToHttpActionResultGeneric_ReturnProblem_WhenFailedWithoutValidation()
	{
		// Arrange
		var controller = new TestController();
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);

		// Act
		var actionResult = controller.ToHttpActionResult(result);

		// Assert
		actionResult.ShouldNotBeNull();
		actionResult.ShouldBeOfType<ObjectResult>();
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Concrete test implementation since IValidationResult has static abstract members and cannot be faked.
	/// </summary>
	private sealed class TestValidationResult : IValidationResult
	{
		public bool IsValid { get; set; }

		public IReadOnlyCollection<object> Errors { get; set; } = [];

		public static IValidationResult Failed(params object[] errors) =>
			new TestValidationResult { IsValid = false, Errors = errors };

		public static IValidationResult Success() =>
			new TestValidationResult { IsValid = true };
	}

	private sealed class TestController : ControllerBase;

	#endregion
}

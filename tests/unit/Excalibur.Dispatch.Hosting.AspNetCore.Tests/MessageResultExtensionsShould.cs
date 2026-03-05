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

	#region ToApiResult (non-generic async)

	[Fact]
	public async Task ToApiResult_ThrowWhenTaskIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await ((Task<IMessageResult>)null!).ToApiResult());
	}

	[Fact]
	public async Task ToApiResult_ReturnAccepted_WhenSucceeded()
	{
		// Arrange
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(true);
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToApiResult();

		// Assert
		httpResult.ShouldNotBeNull();
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(202);
	}

	[Fact]
	public async Task ToApiResult_ReturnForbid_WhenAuthorizationFailed()
	{
		// Arrange
		var authResult = A.Fake<IAuthorizationResult>();
		A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(authResult);
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToApiResult();

		// Assert
		httpResult.ShouldBeOfType<ForbidHttpResult>();
	}

	[Fact]
	public async Task ToApiResult_ReturnBadRequest_WhenValidationFailed()
	{
		// Arrange
		var validationResult = new TestValidationResult { IsValid = false };

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(validationResult);
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToApiResult();

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(400);
	}

	[Fact]
	public async Task ToApiResult_ReturnProblem_WhenFailedWithoutProblemDetails()
	{
		// Arrange
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);
		A.CallTo(() => result.ProblemDetails).Returns(null);
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToApiResult();

		// Assert
		httpResult.ShouldNotBeNull();
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(500);
	}

	#endregion

	#region ToApiResult<T> (generic async)

	[Fact]
	public async Task ToApiResultGeneric_ThrowWhenTaskIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await ((Task<IMessageResult<string>>)null!).ToApiResult());
	}

	[Fact]
	public async Task ToApiResultGeneric_ReturnOk_WhenSucceeded()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(true);
		A.CallTo(() => result.ReturnValue).Returns("test-value");
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToApiResult();

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(200);
	}

	[Fact]
	public async Task ToApiResultGeneric_ReturnForbid_WhenAuthorizationFailed()
	{
		// Arrange
		var authResult = A.Fake<IAuthorizationResult>();
		A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(authResult);
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToApiResult();

		// Assert
		httpResult.ShouldBeOfType<ForbidHttpResult>();
	}

	[Fact]
	public async Task ToApiResultGeneric_ReturnBadRequest_WhenValidationFailed()
	{
		// Arrange
		var validationResult = new TestValidationResult { IsValid = false };

		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(validationResult);
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToApiResult();

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(400);
	}

	[Fact]
	public async Task ToApiResultGeneric_ReturnProblem_WhenFailedWithoutProblemDetails()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);
		A.CallTo(() => result.ProblemDetails).Returns(null);
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToApiResult();

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(500);
	}

	#endregion

	#region ToNoContentResult

	[Fact]
	public void ToNoContentResult_ThrowWhenMessageResultIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IMessageResult)null!).ToNoContentResult());
	}

	[Fact]
	public void ToNoContentResult_ReturnNoContent_WhenSucceeded()
	{
		// Arrange
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(true);

		// Act
		var httpResult = result.ToNoContentResult();

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(204);
	}

	[Fact]
	public void ToNoContentResult_ReturnForbid_WhenAuthorizationFailed()
	{
		// Arrange
		var authResult = A.Fake<IAuthorizationResult>();
		A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(authResult);

		// Act
		var httpResult = result.ToNoContentResult();

		// Assert
		httpResult.ShouldBeOfType<ForbidHttpResult>();
	}

	[Fact]
	public async Task ToNoContentResultAsync_ThrowWhenTaskIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await ((Task<IMessageResult>)null!).ToNoContentResult());
	}

	[Fact]
	public async Task ToNoContentResultAsync_ReturnNoContent_WhenSucceeded()
	{
		// Arrange
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(true);
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToNoContentResult();

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(204);
	}

	#endregion

	#region ToCreatedResult<T>

	[Fact]
	public void ToCreatedResult_ThrowWhenMessageResultIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IMessageResult<string>)null!).ToCreatedResult("/api/items/1"));
	}

	[Fact]
	public void ToCreatedResult_ReturnCreated_WhenSucceeded()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(true);
		A.CallTo(() => result.ReturnValue).Returns("new-item");

		// Act
		var httpResult = result.ToCreatedResult("/api/items/1");

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(201);
	}

	[Fact]
	public void ToCreatedResult_ReturnForbid_WhenAuthorizationFailed()
	{
		// Arrange
		var authResult = A.Fake<IAuthorizationResult>();
		A.CallTo(() => authResult.IsAuthorized).Returns(false);

		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(authResult);

		// Act
		var httpResult = result.ToCreatedResult("/api/items/1");

		// Assert
		httpResult.ShouldBeOfType<ForbidHttpResult>();
	}

	[Fact]
	public void ToCreatedResult_ReturnProblem_WhenFailed()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);
		A.CallTo(() => result.ProblemDetails).Returns(null);

		// Act
		var httpResult = result.ToCreatedResult("/api/items/1");

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(500);
	}

	[Fact]
	public async Task ToCreatedResultAsync_ThrowWhenTaskIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await ((Task<IMessageResult<string>>)null!).ToCreatedResult("/api/items/1"));
	}

	[Fact]
	public async Task ToCreatedResultAsync_ReturnCreated_WhenSucceeded()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(true);
		A.CallTo(() => result.ReturnValue).Returns("new-item");
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToCreatedResult("/api/items/1");

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(201);
	}

	[Fact]
	public async Task ToCreatedResultAsync_ReturnError_WhenFailed()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);
		A.CallTo(() => result.ProblemDetails).Returns(null);
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToCreatedResult("/api/items/1");

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(500);
	}

	[Fact]
	public async Task ToCreatedResultWithFactory_ThrowWhenTaskIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await ((Task<IMessageResult<string>>)null!).ToCreatedResult(v => $"/api/items/{v}"));
	}

	[Fact]
	public async Task ToCreatedResultWithFactory_ThrowWhenFactoryIsNull()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();
		var task = Task.FromResult(result);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await task.ToCreatedResult((Func<string, string>)null!));
	}

	[Fact]
	public async Task ToCreatedResultWithFactory_ReturnCreated_WhenSucceeded()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(true);
		A.CallTo(() => result.ReturnValue).Returns("item-42");
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToCreatedResult(v => $"/api/items/{v}");

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(201);
	}

	[Fact]
	public async Task ToCreatedResultWithFactory_ReturnError_WhenFailed()
	{
		// Arrange
		var result = A.Fake<IMessageResult<string>>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);
		A.CallTo(() => result.ProblemDetails).Returns(null);
		var task = Task.FromResult(result);

		// Act
		var httpResult = await task.ToCreatedResult(v => $"/api/items/{v}");

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(500);
	}

	#endregion

	#region ProblemDetails-Aware Mapping

	[Fact]
	public void ToHttpResult_ReturnProblemWith404_WhenProblemDetailsHasNotFoundStatus()
	{
		// Arrange
		var problemDetails = MessageProblemDetails.NotFound("Order not found");

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);
		A.CallTo(() => result.ProblemDetails).Returns(problemDetails);

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(404);
	}

	[Fact]
	public void ToHttpResult_ReturnProblemWith409_WhenProblemDetailsHasConflictStatus()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails
		{
			Type = "conflict",
			Title = "Conflict",
			Detail = "Resource already exists",
			Status = 409
		};

		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);
		A.CallTo(() => result.ProblemDetails).Returns(problemDetails);

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(409);
	}

	[Fact]
	public void ToHttpResult_IncludeErrorMessage_WhenProblemDetailsIsNull()
	{
		// Arrange
		var result = A.Fake<IMessageResult>();
		A.CallTo(() => result.Succeeded).Returns(false);
		A.CallTo(() => result.AuthorizationResult).Returns(null);
		A.CallTo(() => result.ValidationResult).Returns(null);
		A.CallTo(() => result.ProblemDetails).Returns(null);
		A.CallTo(() => result.ErrorMessage).Returns("Something went wrong");

		// Act
		var httpResult = result.ToHttpResult();

		// Assert
		httpResult.ShouldNotBeNull();
		httpResult.ShouldBeAssignableTo<IStatusCodeHttpResult>();
		((IStatusCodeHttpResult)httpResult).StatusCode.ShouldBe(500);
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

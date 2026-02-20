// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Authentication;
using Excalibur.A3.Exceptions;

using FakeItEasy;

namespace Excalibur.Tests.A3.Exceptions;

/// <summary>
/// Unit tests for <see cref="NotAuthorizedException"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class NotAuthorizedExceptionShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithDefaultConstructor_CreatesException()
	{
		// Arrange & Act
		var exception = new NotAuthorizedException();

		// Assert
		exception.ShouldNotBeNull();
	}

	[Fact]
	public void Create_WithMessageOnly_SetsMessage()
	{
		// Arrange
		var message = "Access denied";

		// Act
		var exception = new NotAuthorizedException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Create_WithMessageAndInnerException_SetsBoth()
	{
		// Arrange
		var message = "Authorization failed";
		var inner = new InvalidOperationException("Inner error");

		// Act
		var exception = new NotAuthorizedException(message, inner);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void Create_WithStatusCodeMessageAndInner_SetsAll()
	{
		// Arrange
		var statusCode = 403;
		var message = "Forbidden";
		var inner = new InvalidOperationException("Reason");

		// Act
		var exception = new NotAuthorizedException(statusCode, message, inner);

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void Create_WithUserDetails_SetsUserProperties()
	{
		// Arrange
		var user = A.Fake<IAuthenticationToken>();
		A.CallTo(() => user.Login).Returns("user@example.com");
		A.CallTo(() => user.UserId).Returns("user-123");
		A.CallTo(() => user.FullName).Returns("John Doe");

		// Act
		var exception = new NotAuthorizedException(user, 403, "Forbidden");

		// Assert
		exception.Login.ShouldBe("user@example.com");
		exception.UserId.ShouldBe("user-123");
		exception.UserName.ShouldBe("John Doe");
	}

	[Fact]
	public void Create_WithNullUser_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new NotAuthorizedException(null!, 403, "Forbidden"));
	}

	#endregion

	#region BecauseNotAuthenticated Tests

	[Fact]
	public void BecauseNotAuthenticated_Returns401()
	{
		// Act
		var exception = NotAuthorizedException.BecauseNotAuthenticated();

		// Assert
		exception.StatusCode.ShouldBe(401);
	}

	[Fact]
	public void BecauseNotAuthenticated_HasAnonymousAccessMessage()
	{
		// Act
		var exception = NotAuthorizedException.BecauseNotAuthenticated();

		// Assert
		exception.Message.ShouldContain("Anonymous");
		exception.Message.ShouldContain("not allowed");
	}

	[Fact]
	public void BecauseNotAuthenticated_WithInnerException_IncludesInner()
	{
		// Arrange
		var inner = new InvalidOperationException("Token validation failed");

		// Act
		var exception = NotAuthorizedException.BecauseNotAuthenticated(inner);

		// Assert
		exception.InnerException.ShouldBe(inner);
	}

	#endregion

	#region BecauseForbidden Tests

	[Fact]
	public void BecauseForbidden_Returns403()
	{
		// Arrange
		var user = A.Fake<IAuthenticationToken>();
		A.CallTo(() => user.FullName).Returns("Jane Smith");

		// Act
		var exception = NotAuthorizedException.BecauseForbidden(user, "DeleteUser", null);

		// Assert
		exception.StatusCode.ShouldBe(403);
	}

	[Fact]
	public void BecauseForbidden_IncludesActivityName()
	{
		// Arrange
		var user = A.Fake<IAuthenticationToken>();
		A.CallTo(() => user.FullName).Returns("Test User");

		// Act
		var exception = NotAuthorizedException.BecauseForbidden(user, "CreateOrder", null);

		// Assert
		exception.Message.ShouldContain("CreateOrder");
	}

	[Fact]
	public void BecauseForbidden_IncludesResourceId_WhenProvided()
	{
		// Arrange
		var user = A.Fake<IAuthenticationToken>();
		A.CallTo(() => user.FullName).Returns("Test User");

		// Act
		var exception = NotAuthorizedException.BecauseForbidden(user, "UpdateOrder", "order-123");

		// Assert
		exception.Message.ShouldContain("order-123");
	}

	[Fact]
	public void BecauseForbidden_ExcludesResourceId_WhenNull()
	{
		// Arrange
		var user = A.Fake<IAuthenticationToken>();
		A.CallTo(() => user.FullName).Returns("Test User");

		// Act
		var exception = NotAuthorizedException.BecauseForbidden(user, "DeleteAll", null);

		// Assert
		exception.Message.ShouldNotContain(" on ");
	}

	[Fact]
	public void BecauseForbidden_ExcludesResourceId_WhenEmpty()
	{
		// Arrange
		var user = A.Fake<IAuthenticationToken>();
		A.CallTo(() => user.FullName).Returns("Test User");

		// Act
		var exception = NotAuthorizedException.BecauseForbidden(user, "DeleteAll", "");

		// Assert
		exception.Message.ShouldNotContain(" on ");
	}

	[Fact]
	public void BecauseForbidden_WithNullUser_ThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			NotAuthorizedException.BecauseForbidden(null!, "Activity", null));
	}

	[Fact]
	public void BecauseForbidden_WithInnerException_IncludesInner()
	{
		// Arrange
		var user = A.Fake<IAuthenticationToken>();
		A.CallTo(() => user.FullName).Returns("Test User");
		var inner = new InvalidOperationException("Policy evaluation failed");

		// Act
		var exception = NotAuthorizedException.BecauseForbidden(user, "Activity", null, inner);

		// Assert
		exception.InnerException.ShouldBe(inner);
	}

	#endregion

	#region Because Tests

	[Fact]
	public void Because_Returns403()
	{
		// Arrange
		var user = A.Fake<IAuthenticationToken>();

		// Act
		var exception = NotAuthorizedException.Because(user, "Custom reason");

		// Assert
		exception.StatusCode.ShouldBe(403);
	}

	[Fact]
	public void Because_UsesCustomMessage()
	{
		// Arrange
		var user = A.Fake<IAuthenticationToken>();
		var message = "You do not have permission to access this resource";

		// Act
		var exception = NotAuthorizedException.Because(user, message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Because_WithInnerException_IncludesInner()
	{
		// Arrange
		var user = A.Fake<IAuthenticationToken>();
		var inner = new InvalidOperationException("Original error");

		// Act
		var exception = NotAuthorizedException.Because(user, "Reason", inner);

		// Assert
		exception.InnerException.ShouldBe(inner);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromApiException()
	{
		// Arrange
		var exception = new NotAuthorizedException();

		// Assert
		exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void IsSerializable()
	{
		// Assert
		typeof(NotAuthorizedException).GetCustomAttributes(typeof(SerializableAttribute), false)
			.Length.ShouldBe(1);
	}

	#endregion
}

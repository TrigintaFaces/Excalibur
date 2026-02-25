// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Exceptions;

namespace Excalibur.Tests.A3.Exceptions;

/// <summary>
/// Unit tests for <see cref="NotAuthenticatedException"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class NotAuthenticatedExceptionShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_WithDefaultConstructor_UsesDefaultValues()
	{
		// Arrange & Act
		var exception = new NotAuthenticatedException();

		// Assert
		exception.StatusCode.ShouldBe(NotAuthenticatedException.DefaultStatusCode);
		exception.Message.ShouldBe(NotAuthenticatedException.DefaultMessage);
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void Create_WithMessageOnly_SetsMessage()
	{
		// Arrange
		var message = "Custom authentication error";

		// Act
		var exception = new NotAuthenticatedException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.StatusCode.ShouldBe(NotAuthenticatedException.DefaultStatusCode);
	}

	[Fact]
	public void Create_WithMessageAndInnerException_SetsBoth()
	{
		// Arrange
		var message = "Outer error";
		var inner = new InvalidOperationException("Inner error");

		// Act
		var exception = new NotAuthenticatedException(message, inner);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void Create_WithAllParameters_SetsAllValues()
	{
		// Arrange
		var statusCode = 403;
		var message = "Custom message";
		var inner = new InvalidOperationException("Inner");

		// Act
		var exception = new NotAuthenticatedException(statusCode, message, inner);

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void Create_WithNullStatusCode_UsesDefaultStatusCode()
	{
		// Arrange & Act
		var exception = new NotAuthenticatedException(null, "Custom message", null);

		// Assert
		exception.StatusCode.ShouldBe(NotAuthenticatedException.DefaultStatusCode);
	}

	[Fact]
	public void Create_WithNullMessage_UsesDefaultMessage()
	{
		// Arrange & Act
		var exception = new NotAuthenticatedException(null, null, null);

		// Assert
		exception.Message.ShouldBe(NotAuthenticatedException.DefaultMessage);
	}

	#endregion

	#region Default Constants

	[Fact]
	public void DefaultStatusCode_Is401()
	{
		// Assert
		NotAuthenticatedException.DefaultStatusCode.ShouldBe(401);
	}

	[Fact]
	public void DefaultMessage_IsExpected()
	{
		// Assert
		NotAuthenticatedException.DefaultMessage.ShouldBe("Authentication failed.");
	}

	#endregion

	#region BecauseMissingClaim Tests

	[Fact]
	public void BecauseMissingClaim_CreatesExceptionWithClaimInMessage()
	{
		// Arrange
		var claimName = "email";

		// Act
		var exception = NotAuthenticatedException.BecauseMissingClaim(claimName);

		// Assert
		exception.StatusCode.ShouldBe(NotAuthenticatedException.DefaultStatusCode);
		exception.Message.ShouldContain(claimName);
		exception.Message.ShouldContain("missing");
	}

	[Theory]
	[InlineData("upn")]
	[InlineData("name")]
	[InlineData("email")]
	[InlineData("sub")]
	public void BecauseMissingClaim_IncludesClaimName(string claimName)
	{
		// Act
		var exception = NotAuthenticatedException.BecauseMissingClaim(claimName);

		// Assert
		exception.Message.ShouldContain($"'{claimName}'");
	}

	#endregion

	#region ThrowIf Tests

	[Fact]
	public void ThrowIf_WhenConditionIsTrue_ThrowsException()
	{
		// Arrange & Act & Assert
		Should.Throw<NotAuthenticatedException>(() =>
			NotAuthenticatedException.ThrowIf(true));
	}

	[Fact]
	public void ThrowIf_WhenConditionIsFalse_DoesNotThrow()
	{
		// Arrange & Act & Assert
		Should.NotThrow(() => NotAuthenticatedException.ThrowIf(false));
	}

	[Fact]
	public void ThrowIf_WhenTrue_WithCustomStatusCode_UsesStatusCode()
	{
		// Arrange & Act
		var exception = Should.Throw<NotAuthenticatedException>(() =>
			NotAuthenticatedException.ThrowIf(true, 403));

		// Assert
		exception.StatusCode.ShouldBe(403);
	}

	[Fact]
	public void ThrowIf_WhenTrue_WithCustomMessage_UsesMessage()
	{
		// Arrange
		var message = "Token expired";

		// Act
		var exception = Should.Throw<NotAuthenticatedException>(() =>
			NotAuthenticatedException.ThrowIf(true, message: message));

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void ThrowIf_WhenTrue_WithInnerException_IncludesInner()
	{
		// Arrange
		var inner = new InvalidOperationException("Original error");

		// Act
		var exception = Should.Throw<NotAuthenticatedException>(() =>
			NotAuthenticatedException.ThrowIf(true, innerException: inner));

		// Assert
		exception.InnerException.ShouldBe(inner);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromApiException()
	{
		// Arrange
		var exception = new NotAuthenticatedException();

		// Assert
		exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void IsSerializable()
	{
		// Assert
		typeof(NotAuthenticatedException).GetCustomAttributes(typeof(SerializableAttribute), false)
			.Length.ShouldBe(1);
	}

	#endregion
}

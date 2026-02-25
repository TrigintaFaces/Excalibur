// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

/// <summary>
/// Unit tests for <see cref="EncryptionException"/> and <see cref="EncryptionErrorCode"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Encryption")]
public sealed class EncryptionExceptionShould : UnitTestBase
{
	[Fact]
	public void CreateWithDefaultConstructor()
	{
		// Act
		var exception = new EncryptionException();

		// Assert
		exception.Message.ShouldNotBeNullOrWhiteSpace();
		exception.ErrorCode.ShouldBe(EncryptionErrorCode.Unknown);
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessage()
	{
		// Arrange
		var message = "Encryption operation failed";

		// Act
		var exception = new EncryptionException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.ErrorCode.ShouldBe(EncryptionErrorCode.Unknown);
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		// Arrange
		var message = "Key not found";
		var inner = new InvalidOperationException("Key storage unavailable");

		// Act
		var exception = new EncryptionException(message, inner);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void SupportErrorCodeProperty()
	{
		// Act
		var exception = new EncryptionException("Key expired")
		{
			ErrorCode = EncryptionErrorCode.KeyExpired
		};

		// Assert
		exception.ErrorCode.ShouldBe(EncryptionErrorCode.KeyExpired);
	}

	[Theory]
	[InlineData(EncryptionErrorCode.Unknown)]
	[InlineData(EncryptionErrorCode.KeyNotFound)]
	[InlineData(EncryptionErrorCode.KeyExpired)]
	[InlineData(EncryptionErrorCode.KeySuspended)]
	[InlineData(EncryptionErrorCode.InvalidCiphertext)]
	[InlineData(EncryptionErrorCode.AuthenticationFailed)]
	[InlineData(EncryptionErrorCode.FipsComplianceViolation)]
	[InlineData(EncryptionErrorCode.AccessDenied)]
	[InlineData(EncryptionErrorCode.ServiceUnavailable)]
	[InlineData(EncryptionErrorCode.UnsupportedAlgorithm)]
	public void SupportAllErrorCodes(EncryptionErrorCode errorCode)
	{
		// Act
		var exception = new EncryptionException("Test") { ErrorCode = errorCode };

		// Assert
		exception.ErrorCode.ShouldBe(errorCode);
	}

	[Theory]
	[InlineData(EncryptionErrorCode.Unknown, 0)]
	[InlineData(EncryptionErrorCode.KeyNotFound, 1)]
	[InlineData(EncryptionErrorCode.KeyExpired, 2)]
	[InlineData(EncryptionErrorCode.KeySuspended, 3)]
	[InlineData(EncryptionErrorCode.InvalidCiphertext, 4)]
	[InlineData(EncryptionErrorCode.AuthenticationFailed, 5)]
	[InlineData(EncryptionErrorCode.FipsComplianceViolation, 6)]
	[InlineData(EncryptionErrorCode.AccessDenied, 7)]
	[InlineData(EncryptionErrorCode.ServiceUnavailable, 8)]
	[InlineData(EncryptionErrorCode.UnsupportedAlgorithm, 9)]
	public void HaveCorrectErrorCodeValues(EncryptionErrorCode errorCode, int expectedValue)
	{
		// Assert
		((int)errorCode).ShouldBe(expectedValue);
	}

	[Fact]
	public void Have10ErrorCodes()
	{
		// Act
		var errorCodes = Enum.GetValues<EncryptionErrorCode>();

		// Assert
		errorCodes.Length.ShouldBe(10);
	}

	[Fact]
	public void BeDerivableFromException()
	{
		// Act
		var exception = new EncryptionException("Test");

		// Assert
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void DefaultToUnknownErrorCode()
	{
		// Arrange
		EncryptionErrorCode defaultValue = default;

		// Assert
		defaultValue.ShouldBe(EncryptionErrorCode.Unknown);
	}
}

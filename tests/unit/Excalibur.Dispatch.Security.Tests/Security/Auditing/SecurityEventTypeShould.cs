// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Auditing;

/// <summary>
/// Unit tests for <see cref="SecurityEventType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SecurityEventTypeShould
{
	[Fact]
	public void HaveAuthenticationSuccessAsZero()
	{
		// Assert
		((int)SecurityEventType.AuthenticationSuccess).ShouldBe(0);
	}

	[Fact]
	public void HaveAuthenticationFailureAsOne()
	{
		// Assert
		((int)SecurityEventType.AuthenticationFailure).ShouldBe(1);
	}

	[Fact]
	public void HaveAuthorizationSuccessAsTwo()
	{
		// Assert
		((int)SecurityEventType.AuthorizationSuccess).ShouldBe(2);
	}

	[Fact]
	public void HaveAuthorizationFailureAsThree()
	{
		// Assert
		((int)SecurityEventType.AuthorizationFailure).ShouldBe(3);
	}

	[Fact]
	public void HaveValidationFailureAsFour()
	{
		// Assert
		((int)SecurityEventType.ValidationFailure).ShouldBe(4);
	}

	[Fact]
	public void HaveValidationErrorAsFive()
	{
		// Assert
		((int)SecurityEventType.ValidationError).ShouldBe(5);
	}

	[Fact]
	public void HaveInjectionAttemptAsSix()
	{
		// Assert
		((int)SecurityEventType.InjectionAttempt).ShouldBe(6);
	}

	[Fact]
	public void HaveRateLimitExceededAsSeven()
	{
		// Assert
		((int)SecurityEventType.RateLimitExceeded).ShouldBe(7);
	}

	[Fact]
	public void HaveSuspiciousActivityAsEight()
	{
		// Assert
		((int)SecurityEventType.SuspiciousActivity).ShouldBe(8);
	}

	[Fact]
	public void HaveDataExfiltrationAttemptAsNine()
	{
		// Assert
		((int)SecurityEventType.DataExfiltrationAttempt).ShouldBe(9);
	}

	[Fact]
	public void HaveConfigurationChangeAsTen()
	{
		// Assert
		((int)SecurityEventType.ConfigurationChange).ShouldBe(10);
	}

	[Fact]
	public void HaveCredentialRotationAsEleven()
	{
		// Assert
		((int)SecurityEventType.CredentialRotation).ShouldBe(11);
	}

	[Fact]
	public void HaveAuditLogAccessAsTwelve()
	{
		// Assert
		((int)SecurityEventType.AuditLogAccess).ShouldBe(12);
	}

	[Fact]
	public void HaveSecurityPolicyViolationAsThirteen()
	{
		// Assert
		((int)SecurityEventType.SecurityPolicyViolation).ShouldBe(13);
	}

	[Fact]
	public void HaveEncryptionFailureAsFourteen()
	{
		// Assert
		((int)SecurityEventType.EncryptionFailure).ShouldBe(14);
	}

	[Fact]
	public void HaveDecryptionFailureAsFifteen()
	{
		// Assert
		((int)SecurityEventType.DecryptionFailure).ShouldBe(15);
	}

	[Fact]
	public void HaveSixteenDefinedValues()
	{
		// Arrange
		var values = Enum.GetValues<SecurityEventType>();

		// Assert
		values.Length.ShouldBe(16);
	}

	[Fact]
	public void DefaultToAuthenticationSuccess()
	{
		// Arrange & Act
		var defaultValue = default(SecurityEventType);

		// Assert
		defaultValue.ShouldBe(SecurityEventType.AuthenticationSuccess);
	}

	[Theory]
	[InlineData(SecurityEventType.AuthenticationSuccess, "AuthenticationSuccess")]
	[InlineData(SecurityEventType.AuthenticationFailure, "AuthenticationFailure")]
	[InlineData(SecurityEventType.InjectionAttempt, "InjectionAttempt")]
	[InlineData(SecurityEventType.RateLimitExceeded, "RateLimitExceeded")]
	public void HaveCorrectStringRepresentation(SecurityEventType eventType, string expected)
	{
		// Act
		var result = eventType.ToString();

		// Assert
		result.ShouldBe(expected);
	}

	[Theory]
	[InlineData("AuthenticationSuccess", SecurityEventType.AuthenticationSuccess)]
	[InlineData("AuthenticationFailure", SecurityEventType.AuthenticationFailure)]
	[InlineData("AuthorizationSuccess", SecurityEventType.AuthorizationSuccess)]
	[InlineData("InjectionAttempt", SecurityEventType.InjectionAttempt)]
	public void ParseFromString(string value, SecurityEventType expected)
	{
		// Act
		var result = Enum.Parse<SecurityEventType>(value);

		// Assert
		result.ShouldBe(expected);
	}
}

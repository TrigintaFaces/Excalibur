// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.Options;

/// <summary>
/// Unit tests for <see cref="JwtAuthenticationOptionsValidator"/> internal class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Options")]
public sealed class JwtAuthenticationOptionsValidatorShould
{
	private readonly JwtAuthenticationOptionsValidator _sut;

	public JwtAuthenticationOptionsValidatorShould()
	{
		_sut = new JwtAuthenticationOptionsValidator();
	}

	[Fact]
	public void ImplementIValidateOptions()
	{
		_sut.ShouldBeAssignableTo<IValidateOptions<JwtAuthenticationOptions>>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		typeof(JwtAuthenticationOptionsValidator).IsNotPublic.ShouldBeTrue();
		typeof(JwtAuthenticationOptionsValidator).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSuccessWhenDisabled()
	{
		// When Enabled = false, no cross-property checks apply
		var options = new JwtAuthenticationOptions { Enabled = false };

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSuccessWhenFullyConfigured()
	{
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			ValidateIssuer = true,
			ValidIssuer = "https://auth.example.com",
			ValidateAudience = true,
			ValidAudience = "my-api",
			ValidateSigningKey = true,
			SigningKey = "super-secret-key-that-is-long-enough"
		};

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSuccessWhenValidationDisabled()
	{
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			ValidateIssuer = false,
			ValidateAudience = false,
			ValidateSigningKey = false
		};

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenValidateIssuerWithoutIssuer()
	{
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			ValidateIssuer = true,
			// No ValidIssuer or ValidIssuers set
			ValidateAudience = false,
			ValidateSigningKey = false
		};

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("ValidIssuer");
	}

	[Fact]
	public void SucceedWhenValidateIssuerWithValidIssuersArray()
	{
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			ValidateIssuer = true,
			ValidIssuers = ["https://auth1.example.com", "https://auth2.example.com"],
			ValidateAudience = false,
			ValidateSigningKey = false
		};

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenValidateAudienceWithoutAudience()
	{
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			ValidateIssuer = false,
			ValidateAudience = true,
			// No ValidAudience or ValidAudiences set
			ValidateSigningKey = false
		};

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("ValidAudience");
	}

	[Fact]
	public void SucceedWhenValidateAudienceWithValidAudiencesArray()
	{
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			ValidateIssuer = false,
			ValidateAudience = true,
			ValidAudiences = ["api-1", "api-2"],
			ValidateSigningKey = false
		};

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenValidateSigningKeyWithoutAnyKey()
	{
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			ValidateIssuer = false,
			ValidateAudience = false,
			ValidateSigningKey = true
			// No SigningKey, RsaPublicKey, or SigningKeyCredentialName set
		};

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("SigningKey");
	}

	[Fact]
	public void SucceedWhenValidateSigningKeyWithRsaPublicKey()
	{
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			ValidateIssuer = false,
			ValidateAudience = false,
			ValidateSigningKey = true,
			RsaPublicKey = "MIIBCgKCAQEA..."
		};

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SucceedWhenValidateSigningKeyWithCredentialName()
	{
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			ValidateIssuer = false,
			ValidateAudience = false,
			ValidateSigningKey = true,
			SigningKeyCredentialName = "my-vault-key"
		};

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWithMultipleErrors()
	{
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateSigningKey = true,
			ClockSkewSeconds = -1
			// No issuer, audience, or signing key configured
		};

		var result = _sut.Validate(null, options);

		result.Succeeded.ShouldBeFalse();
		result.FailureMessage.ShouldContain("ClockSkewSeconds");
		result.FailureMessage.ShouldContain("ValidIssuer");
		result.FailureMessage.ShouldContain("ValidAudience");
		result.FailureMessage.ShouldContain("SigningKey");
	}

	[Fact]
	public void ReturnSuccessWithNamedOptions()
	{
		var options = new JwtAuthenticationOptions { Enabled = false };

		var result = _sut.Validate("Production", options);

		result.Succeeded.ShouldBeTrue();
	}
}

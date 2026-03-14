// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Authentication;

/// <summary>
/// Unit tests for <see cref="JwtAuthenticationOptions"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class JwtAuthenticationOptionsShould
{
	[Fact]
	public void HaveTrueEnabled_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueRequireAuthentication_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.RequireAuthentication.ShouldBeTrue();
	}

	[Fact]
	public void HaveAuthTokenAsTokenContextKey_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.TokenContextKey.ShouldBe("AuthToken");
	}

	[Fact]
	public void HaveAuthorizationAsTokenHeaderName_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.TokenHeaderName.ShouldBe("Authorization");
	}

	[Fact]
	public void HaveAuthTokenAsTokenPropertyName_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.TokenPropertyName.ShouldBe("AuthToken");
	}

	[Fact]
	public void HaveFalseEnablePropertyExtraction_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.EnablePropertyExtraction.ShouldBeFalse();
	}

	[Fact]
	public void HaveEmptyAllowAnonymousMessageTypes_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.AllowAnonymousMessageTypes.ShouldNotBeNull();
		options.AllowAnonymousMessageTypes.ShouldBeEmpty();
	}

	[Fact]
	public void HaveTrueValidateIssuer_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Validation.ValidateIssuer.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueValidateAudience_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Validation.ValidateAudience.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueValidateLifetime_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Validation.ValidateLifetime.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueValidateSigningKey_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Validation.ValidateSigningKey.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueRequireExpirationTime_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Validation.RequireExpirationTime.ShouldBeTrue();
	}

	[Fact]
	public void HaveTrueRequireSignedTokens_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Validation.RequireSignedTokens.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullValidIssuer_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Credentials.ValidIssuer.ShouldBeNull();
	}

	[Fact]
	public void HaveNullValidIssuers_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Credentials.ValidIssuers.ShouldBeNull();
	}

	[Fact]
	public void HaveNullValidAudience_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Credentials.ValidAudience.ShouldBeNull();
	}

	[Fact]
	public void HaveNullValidAudiences_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Credentials.ValidAudiences.ShouldBeNull();
	}

	[Fact]
	public void HaveNullSigningKey_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Credentials.SigningKey.ShouldBeNull();
	}

	[Fact]
	public void HaveNullRsaPublicKey_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Credentials.RsaPublicKey.ShouldBeNull();
	}

	[Fact]
	public void Have300ClockSkewSeconds_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.ClockSkewSeconds.ShouldBe(300);
	}

	[Fact]
	public void HaveFalseUseAsyncKeyRetrieval_ByDefault()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions();

		// Assert
		options.Credentials.UseAsyncKeyRetrieval.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingEnabled()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Enabled = false;

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingRequireAuthentication()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.RequireAuthentication = false;

		// Assert
		options.RequireAuthentication.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingTokenContextKey()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.TokenContextKey = "BearerToken";

		// Assert
		options.TokenContextKey.ShouldBe("BearerToken");
	}

	[Fact]
	public void AllowSettingTokenHeaderName()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.TokenHeaderName = "X-Auth-Token";

		// Assert
		options.TokenHeaderName.ShouldBe("X-Auth-Token");
	}

	[Fact]
	public void AllowSettingTokenPropertyName()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.TokenPropertyName = "Token";

		// Assert
		options.TokenPropertyName.ShouldBe("Token");
	}

	[Fact]
	public void AllowSettingEnablePropertyExtraction()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.EnablePropertyExtraction = true;

		// Assert
		options.EnablePropertyExtraction.ShouldBeTrue();
	}

	[Fact]
	public void AllowAddingToAllowAnonymousMessageTypes()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.AllowAnonymousMessageTypes.Add("HealthCheckQuery");
		options.AllowAnonymousMessageTypes.Add("PingCommand");

		// Assert
		options.AllowAnonymousMessageTypes.Count.ShouldBe(2);
		options.AllowAnonymousMessageTypes.ShouldContain("HealthCheckQuery");
		options.AllowAnonymousMessageTypes.ShouldContain("PingCommand");
	}

	[Fact]
	public void AllowSettingValidateIssuer()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Validation.ValidateIssuer = false;

		// Assert
		options.Validation.ValidateIssuer.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingValidateAudience()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Validation.ValidateAudience = false;

		// Assert
		options.Validation.ValidateAudience.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingValidateLifetime()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Validation.ValidateLifetime = false;

		// Assert
		options.Validation.ValidateLifetime.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingValidateSigningKey()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Validation.ValidateSigningKey = false;

		// Assert
		options.Validation.ValidateSigningKey.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingRequireExpirationTime()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Validation.RequireExpirationTime = false;

		// Assert
		options.Validation.RequireExpirationTime.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingRequireSignedTokens()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Validation.RequireSignedTokens = false;

		// Assert
		options.Validation.RequireSignedTokens.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingValidIssuer()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Credentials.ValidIssuer = "https://auth.example.com";

		// Assert
		options.Credentials.ValidIssuer.ShouldBe("https://auth.example.com");
	}

	[Fact]
	public void AllowSettingValidIssuers()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Credentials.ValidIssuers = ["https://auth1.example.com", "https://auth2.example.com"];

		// Assert
		options.Credentials.ValidIssuers.ShouldNotBeNull();
		options.Credentials.ValidIssuers.Length.ShouldBe(2);
		options.Credentials.ValidIssuers.ShouldContain("https://auth1.example.com");
	}

	[Fact]
	public void AllowSettingValidAudience()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Credentials.ValidAudience = "api://my-app";

		// Assert
		options.Credentials.ValidAudience.ShouldBe("api://my-app");
	}

	[Fact]
	public void AllowSettingValidAudiences()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Credentials.ValidAudiences = ["api://app1", "api://app2", "api://app3"];

		// Assert
		options.Credentials.ValidAudiences.ShouldNotBeNull();
		options.Credentials.ValidAudiences.Length.ShouldBe(3);
	}

	[Fact]
	public void AllowSettingSigningKey()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Credentials.SigningKey = "super-secret-signing-key-for-symmetric-algorithm";

		// Assert
		options.Credentials.SigningKey.ShouldBe("super-secret-signing-key-for-symmetric-algorithm");
	}

	[Fact]
	public void AllowSettingRsaPublicKey()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Credentials.RsaPublicKey = "-----BEGIN PUBLIC KEY-----...-----END PUBLIC KEY-----";

		// Assert
		options.Credentials.RsaPublicKey.ShouldBe("-----BEGIN PUBLIC KEY-----...-----END PUBLIC KEY-----");
	}

	[Fact]
	public void AllowSettingClockSkewSeconds()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.ClockSkewSeconds = 60;

		// Assert
		options.ClockSkewSeconds.ShouldBe(60);
	}

	[Fact]
	public void AllowSettingUseAsyncKeyRetrieval()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.Credentials.UseAsyncKeyRetrieval = true;

		// Assert
		options.Credentials.UseAsyncKeyRetrieval.ShouldBeTrue();
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange & Act
		var options = new JwtAuthenticationOptions
		{
			Enabled = true,
			RequireAuthentication = true,
			TokenContextKey = "CustomToken",
			TokenHeaderName = "X-Custom-Auth",
			TokenPropertyName = "CustomProp",
			EnablePropertyExtraction = true,
			Validation = new JwtTokenValidationOptions
			{
				ValidateIssuer = true,
				ValidateAudience = true,
				ValidateLifetime = true,
				ValidateSigningKey = true,
				RequireExpirationTime = true,
				RequireSignedTokens = true,
			},
			Credentials = new JwtTokenCredentialOptions
			{
				ValidIssuer = "https://issuer.example.com",
				ValidIssuers = ["https://issuer1.example.com"],
				ValidAudience = "api://audience",
				ValidAudiences = ["api://aud1", "api://aud2"],
				SigningKey = "my-secret-key",
				RsaPublicKey = "-----BEGIN PUBLIC KEY-----...",
				UseAsyncKeyRetrieval = true,
			},
			ClockSkewSeconds = 120,
		};

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RequireAuthentication.ShouldBeTrue();
		options.TokenContextKey.ShouldBe("CustomToken");
		options.TokenHeaderName.ShouldBe("X-Custom-Auth");
		options.TokenPropertyName.ShouldBe("CustomProp");
		options.EnablePropertyExtraction.ShouldBeTrue();
		options.Validation.ValidateIssuer.ShouldBeTrue();
		options.Validation.ValidateAudience.ShouldBeTrue();
		options.Validation.ValidateLifetime.ShouldBeTrue();
		options.Validation.ValidateSigningKey.ShouldBeTrue();
		options.Validation.RequireExpirationTime.ShouldBeTrue();
		options.Validation.RequireSignedTokens.ShouldBeTrue();
		options.Credentials.ValidIssuer.ShouldBe("https://issuer.example.com");
		options.Credentials.ValidIssuers.ShouldNotBeNull();
		options.Credentials.ValidAudience.ShouldBe("api://audience");
		options.Credentials.ValidAudiences.ShouldNotBeNull();
		options.Credentials.SigningKey.ShouldBe("my-secret-key");
		options.Credentials.RsaPublicKey.ShouldBe("-----BEGIN PUBLIC KEY-----...");
		options.ClockSkewSeconds.ShouldBe(120);
		options.Credentials.UseAsyncKeyRetrieval.ShouldBeTrue();
	}

	[Fact]
	public void AllowAnonymousMessageTypesUseOrdinalStringComparison()
	{
		// Arrange
		var options = new JwtAuthenticationOptions();

		// Act
		options.AllowAnonymousMessageTypes.Add("HealthCheck");

		// Assert - case-sensitive (Ordinal comparison)
		options.AllowAnonymousMessageTypes.Contains("HealthCheck").ShouldBeTrue();
		options.AllowAnonymousMessageTypes.Contains("healthcheck").ShouldBeFalse();
		options.AllowAnonymousMessageTypes.Contains("HEALTHCHECK").ShouldBeFalse();
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(JwtAuthenticationOptions).IsSealed.ShouldBeTrue();
	}
}

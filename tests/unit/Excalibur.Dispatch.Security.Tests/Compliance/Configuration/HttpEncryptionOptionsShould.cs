// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Configuration;

[Trait("Category", TestCategories.Unit)]
public sealed class HttpEncryptionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new HttpEncryptionOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		_ = options.Request.ShouldNotBeNull();
		_ = options.Response.ShouldNotBeNull();
		_ = options.Cookies.ShouldNotBeNull();
		_ = options.Headers.ShouldNotBeNull();
		options.ExcludedPaths.ShouldNotBeEmpty();
		options.EnableLogging.ShouldBeTrue();
		options.ProviderId.ShouldBeNull();
	}

	[Fact]
	public void HaveStandardExcludedPaths()
	{
		// Arrange & Act
		var options = new HttpEncryptionOptions();

		// Assert
		options.ExcludedPaths.ShouldContain("/health");
		options.ExcludedPaths.ShouldContain("/healthz");
		options.ExcludedPaths.ShouldContain("/ready");
		options.ExcludedPaths.ShouldContain("/metrics");
		options.ExcludedPaths.ShouldContain("/swagger*");
	}
}

[Trait("Category", TestCategories.Unit)]
public sealed class HttpRequestEncryptionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new HttpRequestEncryptionOptions();

		// Assert
		options.EnableDecryption.ShouldBeTrue();
		options.ValidateIntegrity.ShouldBeTrue();
		options.MaxDataAge.ShouldBe(TimeSpan.FromMinutes(5));
		options.SupportedContentTypes.ShouldContain("application/json");
		options.SupportedContentTypes.ShouldContain("application/x-www-form-urlencoded");
	}

	[Fact]
	public void AllowDisablingMaxDataAge()
	{
		// Arrange & Act
		var options = new HttpRequestEncryptionOptions
		{
			MaxDataAge = null,
		};

		// Assert
		options.MaxDataAge.ShouldBeNull();
	}
}

[Trait("Category", TestCategories.Unit)]
public sealed class HttpResponseEncryptionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new HttpResponseEncryptionOptions();

		// Assert
		options.EnableEncryption.ShouldBeFalse(); // Must be explicitly enabled
		options.EncryptedFields.ShouldBeEmpty();
		options.IncludeMetadata.ShouldBeTrue();
		options.SupportedContentTypes.ShouldContain("application/json");
	}

	[Fact]
	public void AllowConfiguringSensitiveFields()
	{
		// Arrange & Act
		var options = new HttpResponseEncryptionOptions
		{
			EnableEncryption = true,
			EncryptedFields = new List<string>
			{
				"$.password",
				"$.user.ssn",
				"$..creditCard",
			},
		};

		// Assert
		options.EnableEncryption.ShouldBeTrue();
		options.EncryptedFields.Count.ShouldBe(3);
		options.EncryptedFields.ShouldContain("$.password");
	}
}

[Trait("Category", TestCategories.Unit)]
public sealed class HttpCookieEncryptionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new HttpCookieEncryptionOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.AuthenticatedEncryption.ShouldBeTrue();
		options.EncryptedCookies.ShouldNotBeEmpty();
		options.ExcludedCookies.ShouldNotBeEmpty();
	}

	[Fact]
	public void HaveStandardEncryptedCookies()
	{
		// Arrange & Act
		var options = new HttpCookieEncryptionOptions();

		// Assert
		options.EncryptedCookies.ShouldContain("session");
		options.EncryptedCookies.ShouldContain("auth*");
		options.EncryptedCookies.ShouldContain("refresh_token");
	}

	[Fact]
	public void HaveStandardExcludedCookies()
	{
		// Arrange & Act
		var options = new HttpCookieEncryptionOptions();

		// Assert
		options.ExcludedCookies.ShouldContain("consent");
		options.ExcludedCookies.ShouldContain("locale");
		options.ExcludedCookies.ShouldContain("theme");
	}
}

[Trait("Category", TestCategories.Unit)]
public sealed class HttpHeaderEncryptionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new HttpHeaderEncryptionOptions();

		// Assert
		options.Enabled.ShouldBeFalse(); // Must be explicitly enabled
		options.EncryptedHeaders.ShouldNotBeEmpty();
		options.EncryptedValuePrefix.ShouldBe("enc:");
	}

	[Fact]
	public void HaveStandardEncryptedHeaders()
	{
		// Arrange & Act
		var options = new HttpHeaderEncryptionOptions();

		// Assert
		options.EncryptedHeaders.ShouldContain("X-Api-Key");
		options.EncryptedHeaders.ShouldContain("X-Auth-Token");
	}

	[Fact]
	public void AllowCustomPrefix()
	{
		// Arrange & Act
		var options = new HttpHeaderEncryptionOptions
		{
			EncryptedValuePrefix = "encrypted:",
		};

		// Assert
		options.EncryptedValuePrefix.ShouldBe("encrypted:");
	}
}

[Trait("Category", TestCategories.Unit)]
public sealed class ApiEncryptionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new ApiEncryptionOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.ApiPaths.ShouldContain("/api");
		options.RequireEncryptedPayloads.ShouldBeFalse();
		options.EncryptionRequiredEndpoints.ShouldBeEmpty();
		options.EncryptionIndicatorHeader.ShouldBe("X-Content-Encrypted");
		options.KeyIdHeader.ShouldBe("X-Encryption-Key-Id");
		options.ProviderHeader.ShouldBe("X-Encryption-Provider");
	}

	[Fact]
	public void AllowCustomHeaders()
	{
		// Arrange & Act
		var options = new ApiEncryptionOptions
		{
			EncryptionIndicatorHeader = "X-Custom-Encrypted",
			KeyIdHeader = "X-Custom-Key-Id",
			ProviderHeader = "X-Custom-Provider",
		};

		// Assert
		options.EncryptionIndicatorHeader.ShouldBe("X-Custom-Encrypted");
		options.KeyIdHeader.ShouldBe("X-Custom-Key-Id");
		options.ProviderHeader.ShouldBe("X-Custom-Provider");
	}

	[Fact]
	public void AllowConfiguringRequiredEndpoints()
	{
		// Arrange & Act
		var options = new ApiEncryptionOptions
		{
			RequireEncryptedPayloads = true,
			EncryptionRequiredEndpoints = new List<string>
			{
				"/api/auth/login",
				"/api/users/*/password",
				"/api/payments/*",
			},
		};

		// Assert
		options.RequireEncryptedPayloads.ShouldBeTrue();
		options.EncryptionRequiredEndpoints.Count.ShouldBe(3);
	}
}

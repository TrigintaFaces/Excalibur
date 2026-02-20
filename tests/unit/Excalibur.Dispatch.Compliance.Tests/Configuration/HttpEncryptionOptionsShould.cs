using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class HttpEncryptionOptionsShould
{
	[Fact]
	public void Be_enabled_by_default()
	{
		var options = new HttpEncryptionOptions();

		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Have_non_null_request_options_by_default()
	{
		var options = new HttpEncryptionOptions();

		options.Request.ShouldNotBeNull();
	}

	[Fact]
	public void Have_non_null_response_options_by_default()
	{
		var options = new HttpEncryptionOptions();

		options.Response.ShouldNotBeNull();
	}

	[Fact]
	public void Have_non_null_cookies_options_by_default()
	{
		var options = new HttpEncryptionOptions();

		options.Cookies.ShouldNotBeNull();
	}

	[Fact]
	public void Have_non_null_headers_options_by_default()
	{
		var options = new HttpEncryptionOptions();

		options.Headers.ShouldNotBeNull();
	}

	[Fact]
	public void Have_expected_excluded_paths()
	{
		var options = new HttpEncryptionOptions();

		options.ExcludedPaths.ShouldNotBeNull();
		options.ExcludedPaths.ShouldContain("/health");
		options.ExcludedPaths.ShouldContain("/healthz");
		options.ExcludedPaths.ShouldContain("/ready");
		options.ExcludedPaths.ShouldContain("/readyz");
		options.ExcludedPaths.ShouldContain("/live");
		options.ExcludedPaths.ShouldContain("/livez");
		options.ExcludedPaths.ShouldContain("/metrics");
		options.ExcludedPaths.ShouldContain("/swagger*");
	}

	[Fact]
	public void Enable_logging_by_default()
	{
		var options = new HttpEncryptionOptions();

		options.EnableLogging.ShouldBeTrue();
	}

	[Fact]
	public void Have_null_provider_id_by_default()
	{
		var options = new HttpEncryptionOptions();

		options.ProviderId.ShouldBeNull();
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class HttpRequestEncryptionOptionsShould
{
	[Fact]
	public void Enable_decryption_by_default()
	{
		var options = new HttpRequestEncryptionOptions();

		options.EnableDecryption.ShouldBeTrue();
	}

	[Fact]
	public void Validate_integrity_by_default()
	{
		var options = new HttpRequestEncryptionOptions();

		options.ValidateIntegrity.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_max_data_age_of_5_minutes()
	{
		var options = new HttpRequestEncryptionOptions();

		options.MaxDataAge.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Have_expected_supported_content_types()
	{
		var options = new HttpRequestEncryptionOptions();

		options.SupportedContentTypes.ShouldContain("application/json");
		options.SupportedContentTypes.ShouldContain("application/x-www-form-urlencoded");
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class HttpResponseEncryptionOptionsShould
{
	[Fact]
	public void Not_enable_encryption_by_default()
	{
		var options = new HttpResponseEncryptionOptions();

		options.EnableEncryption.ShouldBeFalse();
	}

	[Fact]
	public void Have_empty_encrypted_fields_by_default()
	{
		var options = new HttpResponseEncryptionOptions();

		options.EncryptedFields.ShouldNotBeNull();
		options.EncryptedFields.ShouldBeEmpty();
	}

	[Fact]
	public void Include_metadata_by_default()
	{
		var options = new HttpResponseEncryptionOptions();

		options.IncludeMetadata.ShouldBeTrue();
	}

	[Fact]
	public void Have_json_as_supported_content_type()
	{
		var options = new HttpResponseEncryptionOptions();

		options.SupportedContentTypes.ShouldContain("application/json");
		options.SupportedContentTypes.Count.ShouldBe(1);
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class HttpCookieEncryptionOptionsShould
{
	[Fact]
	public void Be_enabled_by_default()
	{
		var options = new HttpCookieEncryptionOptions();

		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Have_expected_encrypted_cookies()
	{
		var options = new HttpCookieEncryptionOptions();

		options.EncryptedCookies.ShouldContain("session");
		options.EncryptedCookies.ShouldContain("auth*");
		options.EncryptedCookies.ShouldContain("*_token");
		options.EncryptedCookies.ShouldContain("refresh_token");
	}

	[Fact]
	public void Have_expected_excluded_cookies()
	{
		var options = new HttpCookieEncryptionOptions();

		options.ExcludedCookies.ShouldContain("consent");
		options.ExcludedCookies.ShouldContain("locale");
		options.ExcludedCookies.ShouldContain("theme");
	}

	[Fact]
	public void Use_authenticated_encryption_by_default()
	{
		var options = new HttpCookieEncryptionOptions();

		options.AuthenticatedEncryption.ShouldBeTrue();
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class HttpHeaderEncryptionOptionsShould
{
	[Fact]
	public void Not_be_enabled_by_default()
	{
		var options = new HttpHeaderEncryptionOptions();

		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Have_expected_encrypted_headers()
	{
		var options = new HttpHeaderEncryptionOptions();

		options.EncryptedHeaders.ShouldContain("X-Api-Key");
		options.EncryptedHeaders.ShouldContain("X-Auth-Token");
	}

	[Fact]
	public void Have_enc_prefix_by_default()
	{
		var options = new HttpHeaderEncryptionOptions();

		options.EncryptedValuePrefix.ShouldBe("enc:");
	}
}

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class ApiEncryptionOptionsShould
{
	[Fact]
	public void Be_enabled_by_default()
	{
		var options = new ApiEncryptionOptions();

		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Have_api_as_default_path()
	{
		var options = new ApiEncryptionOptions();

		options.ApiPaths.ShouldContain("/api");
		options.ApiPaths.Count.ShouldBe(1);
	}

	[Fact]
	public void Not_require_encrypted_payloads_by_default()
	{
		var options = new ApiEncryptionOptions();

		options.RequireEncryptedPayloads.ShouldBeFalse();
	}

	[Fact]
	public void Have_empty_encryption_required_endpoints_by_default()
	{
		var options = new ApiEncryptionOptions();

		options.EncryptionRequiredEndpoints.ShouldNotBeNull();
		options.EncryptionRequiredEndpoints.ShouldBeEmpty();
	}

	[Fact]
	public void Have_expected_default_header_names()
	{
		var options = new ApiEncryptionOptions();

		options.EncryptionIndicatorHeader.ShouldBe("X-Content-Encrypted");
		options.KeyIdHeader.ShouldBe("X-Encryption-Key-Id");
		options.ProviderHeader.ShouldBe("X-Encryption-Provider");
	}
}

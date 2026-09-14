// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;
using System.Text.Json;

using Excalibur.Data.ElasticSearch.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Tests.Shared.Categories;

using Excalibur.Data.ElasticSearch;
namespace Excalibur.Data.Tests.ElasticSearch.Security.Authentication;

/// <summary>
/// Unit tests for credential rotation methods in <see cref="SecureElasticsearchAuthenticationProvider"/>.
/// Covers T398.6: RotateApiKeyAsync, RotateServiceAccountAsync, RotateOAuth2ClientAsync functionality.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "ElasticSearch.Security")]
[Trait("Sprint", "398")]
public sealed class SecureElasticsearchAuthenticationProviderCredentialRotationShould : IDisposable
{
	private readonly IElasticsearchKeyStorage _mockKeyProvider;
	private readonly IHttpClientFactory _mockHttpClientFactory;
	private readonly ILogger<SecureElasticsearchAuthenticationProvider> _logger;
	private readonly MockHttpMessageHandler _mockHttpHandler;
	private SecureElasticsearchAuthenticationProvider? _sut;
	private bool _disposed;

	public SecureElasticsearchAuthenticationProviderCredentialRotationShould()
	{
		_mockKeyProvider = A.Fake<IElasticsearchKeyStorage>();
		_mockHttpClientFactory = A.Fake<IHttpClientFactory>();
		_logger = NullLogger<SecureElasticsearchAuthenticationProvider>.Instance;
		_mockHttpHandler = new MockHttpMessageHandler();
	}

	#region RotateApiKeyAsync Tests

	[Fact]
	public async Task RotateApiKeyAsync_ReturnSuccess_WhenKeyGenerationSucceeds()
	{
		// Arrange
		var settings = CreateApiKeySettingsWithRotation();
		ConfigureSuccessfulApiKeyRotationScenario();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		_ = result.NextRotationDue.ShouldNotBeNull();
		result.Message.ShouldContain("API key rotation completed");

		// Verify a freshly generated key was stored
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:apikey:test-api-key-id",
				A<string>.Ignored,
				A<SecretMetadata?>.That.Matches(m => m.Description == "Elasticsearch API Key"),
				A<CancellationToken>.Ignored))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RotateApiKeyAsync_PreservePreviousKey_WhenExistingKeyPresent()
	{
		// Arrange
		var settings = CreateApiKeySettingsWithRotation();
		ConfigureExistingApiKey("old-api-key-secret");

		_sut = CreateProvider(settings);

		// Act
		_ = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert - Verify previous key was preserved
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:apikey:test-api-key-id:previous",
				"old-api-key-secret",
				A<SecretMetadata?>.That.Matches(m => m.Description.Contains("Previous API key")),
				A<CancellationToken>.Ignored))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RotateApiKeyAsync_ReturnFailure_WhenApiKeyIdNotConfigured()
	{
		// Arrange
		// When ApiKeyId is null, AuthenticationType becomes None, so SupportsRotation is false
		var settings = CreateApiKeySettingsWithoutApiKeyId();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert - Rotation not supported because without ApiKeyId, authentication type is None
		result.Success.ShouldBeFalse();
		result.Message.ShouldContain("not supported");
	}

	[Fact]
	public async Task RotateApiKeyAsync_ReturnFailure_WhenKeyGenerationFails()
	{
		// Arrange
		var settings = CreateApiKeySettingsWithRotation();
		ConfigureExistingApiKey("old-key");
		ConfigureFailedApiKeyWrite();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.Message.ShouldContain("the credential store rejected the write");
	}

	[Fact]
	public async Task RotateApiKeyAsync_RaiseCredentialsRotatedEvent_OnSuccess()
	{
		// Arrange
		var settings = CreateApiKeySettingsWithRotation();
		ConfigureSuccessfulApiKeyRotationScenario();

		_sut = CreateProvider(settings);
		var eventRaised = false;
		AuthenticationRotatedEventArgs? capturedArgs = null;

		_sut.CredentialsRotated += (sender, args) =>
		{
			eventRaised = true;
			capturedArgs = args;
		};

		// Act
		_ = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		eventRaised.ShouldBeTrue();
		_ = capturedArgs.ShouldNotBeNull();
		capturedArgs.AuthenticationType.ShouldBe(ElasticsearchAuthenticationType.ApiKey);
	}

	#endregion

	#region RotateServiceAccountAsync Tests

	[Fact]
	public async Task RotateServiceAccountAsync_ReturnSuccess_WhenRotationSucceeds()
	{
		// Arrange
		var settings = CreateServiceAccountOptionsWithRotation();
		ConfigureSuccessfulServiceAccountRotationScenario();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		_ = result.NextRotationDue.ShouldNotBeNull();
		result.Message.ShouldContain("Service account rotation completed");

		// Verify a freshly generated secret was stored
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:serviceaccount:secret",
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RotateServiceAccountAsync_PreservePreviousSecret_WhenExisting()
	{
		// Arrange
		var settings = CreateServiceAccountOptionsWithRotation();
		ConfigureExistingServiceAccountSecret("old-service-secret");
		ConfigureSuccessfulServiceAccountTokenRefresh();

		_sut = CreateProvider(settings);

		// Act
		_ = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:serviceaccount:secret:previous",
				"old-service-secret",
				A<SecretMetadata?>.That.Matches(m => m.Description.Contains("Previous service account")),
				A<CancellationToken>.Ignored))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RotateServiceAccountAsync_ReturnFailure_WhenAccountIdNotConfigured()
	{
		// Arrange
		var settings = CreateServiceAccountOptionsWithoutAccountId();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.Message.ShouldContain("not configured");
	}

	[Fact]
	public async Task RotateServiceAccountAsync_ReturnFailure_WhenKeyRotationFails()
	{
		// Arrange
		var settings = CreateServiceAccountOptionsWithRotation();
		ConfigureExistingServiceAccountSecret("old-secret");
		ConfigureFailedServiceAccountWrite();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.Message.ShouldContain("the credential store rejected the write");
	}

	[Fact]
	public async Task RotateServiceAccountAsync_RefreshToken_AfterSecretRotation()
	{
		// Arrange
		var settings = CreateServiceAccountOptionsWithRotation();
		ConfigureExistingServiceAccountSecret("old-secret");
		ConfigureSuccessfulServiceAccountTokenRefresh();

		_sut = CreateProvider(settings);

		// Act
		_ = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert - Verify token refresh was attempted after rotation
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:serviceaccount:token",
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.MustHaveHappened();
	}

	#endregion

	#region RotateOAuth2ClientAsync Tests

	[Fact]
	public async Task RotateOAuth2ClientAsync_ReturnSuccess_WhenRotationSucceeds()
	{
		// Arrange
		var settings = CreateOAuth2OptionsWithRotation();
		ConfigureSuccessfulOAuth2ClientRotationScenario();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		result.Success.ShouldBeTrue();
		_ = result.NextRotationDue.ShouldNotBeNull();
		result.Message.ShouldContain("OAuth2 client rotation completed");

		// Verify a freshly generated secret was stored
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:oauth2:clientsecret",
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RotateOAuth2ClientAsync_PreservePreviousSecret_WhenExisting()
	{
		// Arrange
		var settings = CreateOAuth2OptionsWithRotation();
		ConfigureExistingOAuth2ClientSecret("old-client-secret");
		ConfigureSuccessfulOAuth2TokenRefresh();

		_sut = CreateProvider(settings);

		// Act
		_ = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:oauth2:clientsecret:previous",
				"old-client-secret",
				A<SecretMetadata?>.That.Matches(m => m.Description.Contains("Previous OAuth2 client")),
				A<CancellationToken>.Ignored))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RotateOAuth2ClientAsync_ReturnFailure_WhenClientIdNotConfigured()
	{
		// Arrange
		var settings = CreateOAuth2OptionsWithoutClientId();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.Message.ShouldContain("not configured");
	}

	[Fact]
	public async Task RotateOAuth2ClientAsync_RefreshToken_AfterSecretRotation()
	{
		// Arrange
		var settings = CreateOAuth2OptionsWithRotation();
		ConfigureExistingOAuth2ClientSecret("old-secret");
		ConfigureSuccessfulOAuth2TokenRefresh();

		_sut = CreateProvider(settings);

		// Act
		_ = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert - Verify token refresh was attempted after rotation
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:oauth2:accesstoken",
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.MustHaveHappened();
	}

	#endregion

	#region SupportsRotation Tests

	[Fact]
	public void SupportsRotation_ReturnTrue_ForApiKeyWithRotationEnabled()
	{
		// Arrange
		var settings = CreateApiKeySettingsWithRotation();
		_sut = CreateProvider(settings);

		// Act & Assert
		_sut.SupportsRotation.ShouldBeTrue();
	}

	[Fact]
	public void SupportsRotation_ReturnTrue_ForServiceAccountWithRotationEnabled()
	{
		// Arrange
		var settings = CreateServiceAccountOptionsWithRotation();
		_sut = CreateProvider(settings);

		// Act & Assert
		_sut.SupportsRotation.ShouldBeTrue();
	}

	[Fact]
	public void SupportsRotation_ReturnTrue_ForOAuth2WithRotationEnabled()
	{
		// Arrange
		var settings = CreateOAuth2OptionsWithRotation();
		_sut = CreateProvider(settings);

		// Act & Assert
		_sut.SupportsRotation.ShouldBeTrue();
	}

	[Fact]
	public void SupportsRotation_ReturnFalse_ForBasicAuthentication()
	{
		// Arrange
		var settings = CreateBasicAuthSettingsWithRotation();
		_sut = CreateProvider(settings);

		// Act & Assert
		_sut.SupportsRotation.ShouldBeFalse();
	}

	[Fact]
	public void SupportsRotation_ReturnFalse_WhenRotationDisabled()
	{
		// Arrange
		var settings = CreateApiKeySettingsWithoutRotation();
		_sut = CreateProvider(settings);

		// Act & Assert
		_sut.SupportsRotation.ShouldBeFalse();
	}

	#endregion

	#region RotateCredentialsAsync Concurrent Access Tests

	[Fact]
	public async Task RotateCredentialsAsync_ReturnFailure_WhenRotationNotSupported()
	{
		// Arrange
		var settings = CreateBasicAuthSettingsWithRotation();
		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.Message.ShouldContain("not supported");
	}

	[Fact]
	public async Task RotateCredentialsAsync_HandleException_Gracefully()
	{
		// Arrange
		var settings = CreateApiKeySettingsWithRotation();

		// Configure key generation to throw
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				A<string>.Ignored,
				A<CancellationToken>.Ignored))
			.Throws(new InvalidOperationException("Unexpected error"));

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RotateCredentialsAsync(CancellationToken.None);

		// Assert
		result.Success.ShouldBeFalse();
		result.Message.ShouldContain("Unexpected error");
	}

	#endregion

	#region Helper Methods

	private SecureElasticsearchAuthenticationProvider CreateProvider(ElasticsearchSecurityOptions settings)
	{
		var options = Options.Create(settings);
		return new SecureElasticsearchAuthenticationProvider(options, _mockKeyProvider, _mockHttpClientFactory, _logger);
	}

	private static ElasticsearchSecurityOptions CreateApiKeySettingsWithRotation()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				ApiKeyId = "test-api-key-id",
				CredentialRotation = new CredentialRotationOptions
				{
					Enabled = true,
					RotationInterval = TimeSpan.FromDays(30),
					WarningThreshold = TimeSpan.FromDays(7),
				},
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateApiKeySettingsWithoutRotation()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				ApiKeyId = "test-api-key-id",
				CredentialRotation = new CredentialRotationOptions
				{
					Enabled = false,
				},
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateApiKeySettingsWithoutApiKeyId()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				ApiKeyId = null,
				CredentialRotation = new CredentialRotationOptions
				{
					Enabled = true,
					RotationInterval = TimeSpan.FromDays(30),
				},
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateServiceAccountOptionsWithRotation()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				OAuth2 = new OAuth2Options
				{
					Authority = "https://auth.example.com",
				},
				ServiceAccount = new ServiceAccountOptions
				{
					Enabled = true,
					AccountId = "test-service-account",
				},
				CredentialRotation = new CredentialRotationOptions
				{
					Enabled = true,
					RotationInterval = TimeSpan.FromDays(30),
					WarningThreshold = TimeSpan.FromDays(7),
				},
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateServiceAccountOptionsWithoutAccountId()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				ServiceAccount = new ServiceAccountOptions
				{
					Enabled = true,
					AccountId = null,
				},
				CredentialRotation = new CredentialRotationOptions
				{
					Enabled = true,
					RotationInterval = TimeSpan.FromDays(30),
				},
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateOAuth2OptionsWithRotation()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				OAuth2 = new OAuth2Options
				{
					Enabled = true,
					Authority = "https://auth.example.com",
					ClientId = "test-client-id",
					Scope = "elasticsearch:read elasticsearch:write",
				},
				CredentialRotation = new CredentialRotationOptions
				{
					Enabled = true,
					RotationInterval = TimeSpan.FromDays(30),
					WarningThreshold = TimeSpan.FromDays(7),
				},
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateOAuth2OptionsWithoutClientId()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				OAuth2 = new OAuth2Options
				{
					Enabled = true,
					Authority = "https://auth.example.com",
					ClientId = null,
				},
				CredentialRotation = new CredentialRotationOptions
				{
					Enabled = true,
					RotationInterval = TimeSpan.FromDays(30),
				},
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateBasicAuthSettingsWithRotation()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				Username = "elastic",
				CredentialRotation = new CredentialRotationOptions
				{
					Enabled = true,
					RotationInterval = TimeSpan.FromDays(30),
				},
			},
		};
	}

	private void ConfigureSuccessfulApiKeyRotationScenario()
	{
		ConfigureExistingApiKey(null);
	}

	private void ConfigureExistingApiKey(string? existingKeySecret)
	{
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:apikey:test-api-key-id",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult(existingKeySecret));

		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				A<string>.Ignored,
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult(true));
	}

	private void ConfigureFailedApiKeyWrite() =>
		A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:apikey:test-api-key-id",
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult(false));

	private void ConfigureSuccessfulServiceAccountRotationScenario()
	{
		ConfigureExistingServiceAccountSecret(null);
		ConfigureSuccessfulServiceAccountTokenRefresh();
	}

	private void ConfigureExistingServiceAccountSecret(string? existingSecret)
	{
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:serviceaccount:secret",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult(existingSecret));

		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				A<string>.Ignored,
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult(true));
	}

	private void ConfigureFailedServiceAccountWrite() =>
		A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:serviceaccount:secret",
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult(false));

	private void ConfigureSuccessfulServiceAccountTokenRefresh()
	{
		// Configure the token endpoint response for service account refresh
		var tokenResponse = new
		{
			access_token = "new-service-token",
			expires_in = 3600,
			token_type = "Bearer",
		};

		_mockHttpHandler.ConfigureResponse(HttpStatusCode.OK, JsonSerializer.Serialize(tokenResponse));
		ConfigureHttpClientFactory();
	}

	private void ConfigureSuccessfulOAuth2ClientRotationScenario()
	{
		ConfigureExistingOAuth2ClientSecret(null);
		ConfigureSuccessfulOAuth2TokenRefresh();
	}

	private void ConfigureExistingOAuth2ClientSecret(string? existingSecret)
	{
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:oauth2:clientsecret",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult(existingSecret));

		// Also configure refresh token for token refresh after rotation
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:oauth2:refreshtoken",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>("existing-refresh-token"));

		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				A<string>.Ignored,
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult(true));
	}

	private void ConfigureSuccessfulOAuth2TokenRefresh()
	{
		var tokenResponse = new
		{
			access_token = "new-access-token",
			expires_in = 3600,
			token_type = "Bearer",
		};

		_mockHttpHandler.ConfigureResponse(HttpStatusCode.OK, JsonSerializer.Serialize(tokenResponse));
		ConfigureHttpClientFactory();
	}

	private void ConfigureHttpClientFactory()
	{
		var httpClient = new HttpClient(_mockHttpHandler)
		{
			BaseAddress = new Uri("https://auth.example.com"),
		};

		_ = A.CallTo(() => _mockHttpClientFactory.CreateClient("ElasticsearchOAuth2"))
			.Returns(httpClient);
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_sut?.Dispose();
			_mockHttpHandler.Dispose();
			_disposed = true;
		}
	}

	#endregion
}

/// <summary>
/// Mock HTTP message handler for testing HTTP operations without network calls.
/// </summary>
internal sealed class MockRotationHttpMessageHandler : HttpMessageHandler
{
	private HttpStatusCode _statusCode = HttpStatusCode.OK;
	private string _responseContent = string.Empty;
	private bool _disposed;

	public int RequestCount { get; private set; }
	public string? LastRequestContent { get; private set; }
	public Uri? LastRequestUri { get; private set; }

	public void ConfigureResponse(HttpStatusCode statusCode, string content)
	{
		_statusCode = statusCode;
		_responseContent = content;
	}

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		RequestCount++;
		LastRequestUri = request.RequestUri;

		if (request.Content != null)
		{
			LastRequestContent = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		}

		return new HttpResponseMessage(_statusCode)
		{
			Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json"),
		};
	}

	protected override void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			_disposed = true;
		}

		base.Dispose(disposing);
	}
}

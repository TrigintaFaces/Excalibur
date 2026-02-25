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
/// Unit tests for token refresh methods in <see cref="SecureElasticsearchAuthenticationProvider"/>.
/// Covers T398.5: OAuth2 and service account token refresh functionality.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "ElasticSearch.Security")]
[Trait("Sprint", "398")]
public sealed class SecureElasticsearchAuthenticationProviderTokenRefreshShould : IDisposable
{
	private readonly IElasticsearchKeyProvider _mockKeyProvider;
	private readonly IHttpClientFactory _mockHttpClientFactory;
	private readonly ILogger<SecureElasticsearchAuthenticationProvider> _logger;
	private readonly MockHttpMessageHandler _mockHttpHandler;
	private SecureElasticsearchAuthenticationProvider? _sut;
	private bool _disposed;

	public SecureElasticsearchAuthenticationProviderTokenRefreshShould()
	{
		_mockKeyProvider = A.Fake<IElasticsearchKeyProvider>();
		_mockHttpClientFactory = A.Fake<IHttpClientFactory>();
		_logger = NullLogger<SecureElasticsearchAuthenticationProvider>.Instance;
		_mockHttpHandler = new MockHttpMessageHandler();
	}

	#region RefreshOAuth2TokenAsync Tests

	[Fact]
	public async Task RefreshOAuth2TokenAsync_ReturnTrue_WhenRefreshSucceeds()
	{
		// Arrange
		var settings = CreateOAuth2EnabledSettings();
		ConfigureSuccessfulOAuth2RefreshScenario();

		_sut = CreateProvider(settings);

		// Act - Force token refresh via RefreshAuthenticationAsync
		var result = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		result.ShouldBeTrue();

		// Verify new access token was stored
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:oauth2:accesstoken",
				"new-access-token",
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.MustHaveHappened();

		// Verify expiration was stored
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:oauth2:expiresat",
				A<string>.That.Matches(s => !string.IsNullOrWhiteSpace(s)),
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RefreshOAuth2TokenAsync_StoreNewRefreshToken_WhenProvided()
	{
		// Arrange
		var settings = CreateOAuth2EnabledSettings();
		ConfigureSuccessfulOAuth2RefreshScenario(includeNewRefreshToken: true);

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		result.ShouldBeTrue();

		// Verify new refresh token was stored
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:oauth2:refreshtoken",
				"new-refresh-token",
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RefreshOAuth2TokenAsync_ReturnFalse_WhenNoRefreshTokenAvailable()
	{
		// Arrange
		var settings = CreateOAuth2EnabledSettings();
		ConfigureNoRefreshTokenScenario();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();

		// Verify no HTTP call was made
		_mockHttpHandler.RequestCount.ShouldBe(0);
	}

	[Fact]
	public async Task RefreshOAuth2TokenAsync_ReturnFalse_WhenAuthorityNotConfigured()
	{
		// Arrange
		var settings = CreateOAuth2OptionsWithoutAuthority();
		ConfigureRefreshTokenAvailable();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();

		// Verify no HTTP call was made
		_mockHttpHandler.RequestCount.ShouldBe(0);
	}

	[Fact]
	public async Task RefreshOAuth2TokenAsync_ReturnFalse_WhenHttpRequestFails()
	{
		// Arrange
		var settings = CreateOAuth2EnabledSettings();
		ConfigureRefreshTokenAvailable();
		_mockHttpHandler.ConfigureResponse(HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\"}");
		ConfigureHttpClientFactory();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RefreshOAuth2TokenAsync_ReturnFalse_WhenResponseHasNoAccessToken()
	{
		// Arrange
		var settings = CreateOAuth2EnabledSettings();
		ConfigureRefreshTokenAvailable();
		_mockHttpHandler.ConfigureResponse(HttpStatusCode.OK, "{\"expires_in\":3600}"); // No access_token
		ConfigureHttpClientFactory();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RefreshOAuth2TokenAsync_ReturnFalse_WhenJsonParsingFails()
	{
		// Arrange
		var settings = CreateOAuth2EnabledSettings();
		ConfigureRefreshTokenAvailable();
		_mockHttpHandler.ConfigureResponse(HttpStatusCode.OK, "not-valid-json");
		ConfigureHttpClientFactory();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RefreshOAuth2TokenAsync_IncludeClientSecret_WhenAvailable()
	{
		// Arrange
		var settings = CreateOAuth2EnabledSettings();
		ConfigureSuccessfulOAuth2RefreshScenario(includeClientSecret: true);

		_sut = CreateProvider(settings);

		// Act
		_ = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		var requestContent = _mockHttpHandler.LastRequestContent;
		_ = requestContent.ShouldNotBeNull();
		requestContent.ShouldContain("client_secret=test-client-secret");
	}

	[Fact]
	public async Task RefreshOAuth2TokenAsync_IncludeScope_WhenConfigured()
	{
		// Arrange
		var settings = CreateOAuth2EnabledSettings();
		ConfigureSuccessfulOAuth2RefreshScenario();

		_sut = CreateProvider(settings);

		// Act
		_ = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		var requestContent = _mockHttpHandler.LastRequestContent;
		_ = requestContent.ShouldNotBeNull();
		requestContent.ShouldContain("scope=");
	}

	#endregion

	#region RefreshServiceAccountTokenAsync Tests

	[Fact]
	public async Task RefreshServiceAccountTokenAsync_ReturnTrue_WhenRefreshSucceeds()
	{
		// Arrange
		var settings = CreateServiceAccountEnabledSettings();
		ConfigureSuccessfulServiceAccountRefreshScenario();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		result.ShouldBeTrue();

		// Verify new token was stored
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:serviceaccount:token",
				"new-service-token",
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.MustHaveHappened();

		// Verify expiration was stored
		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				"elasticsearch:serviceaccount:expiresat",
				A<string>.That.Matches(s => !string.IsNullOrWhiteSpace(s)),
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.MustHaveHappened();
	}

	[Fact]
	public async Task RefreshServiceAccountTokenAsync_ReturnFalse_WhenNoClientSecret()
	{
		// Arrange
		var settings = CreateServiceAccountEnabledSettings();

		// Configure key provider to return null for client secret
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:serviceaccount:secret",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>(null));

		// Configure token to be expired to trigger refresh
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:serviceaccount:token",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>("existing-token"));

		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:serviceaccount:expiresat",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O")));

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RefreshServiceAccountTokenAsync_ReturnFalse_WhenAuthorityNotConfigured()
	{
		// Arrange
		var settings = CreateServiceAccountOptionsWithoutAuthority();

		// Configure credentials
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:serviceaccount:secret",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>("test-secret"));

		// Configure token to be expired to trigger refresh
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:serviceaccount:token",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>("existing-token"));

		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:serviceaccount:expiresat",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O")));

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RefreshServiceAccountTokenAsync_ReturnFalse_WhenHttpRequestFails()
	{
		// Arrange
		var settings = CreateServiceAccountEnabledSettings();
		ConfigureServiceAccountCredentials();
		_mockHttpHandler.ConfigureResponse(HttpStatusCode.Unauthorized, "{\"error\":\"unauthorized_client\"}");
		ConfigureHttpClientFactory();

		_sut = CreateProvider(settings);

		// Act
		var result = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task RefreshServiceAccountTokenAsync_UseClientCredentialsGrant()
	{
		// Arrange
		var settings = CreateServiceAccountEnabledSettings();
		ConfigureSuccessfulServiceAccountRefreshScenario();

		_sut = CreateProvider(settings);

		// Act
		_ = await _sut.RefreshAuthenticationAsync(CancellationToken.None);

		// Assert
		var requestContent = _mockHttpHandler.LastRequestContent;
		_ = requestContent.ShouldNotBeNull();
		requestContent.ShouldContain("grant_type=client_credentials");
		requestContent.ShouldContain("client_id=test-service-account");
		requestContent.ShouldContain("client_secret=test-service-secret");
	}

	#endregion

	#region SupportsRefresh Tests

	[Fact]
	public void SupportsRefresh_ReturnTrue_ForOAuth2Authentication()
	{
		// Arrange
		var settings = CreateOAuth2EnabledSettings();
		_sut = CreateProvider(settings);

		// Act & Assert
		_sut.SupportsRefresh.ShouldBeTrue();
	}

	[Fact]
	public void SupportsRefresh_ReturnTrue_ForServiceAccountAuthentication()
	{
		// Arrange
		var settings = CreateServiceAccountEnabledSettings();
		_sut = CreateProvider(settings);

		// Act & Assert
		_sut.SupportsRefresh.ShouldBeTrue();
	}

	[Fact]
	public void SupportsRefresh_ReturnFalse_ForBasicAuthentication()
	{
		// Arrange
		var settings = CreateBasicAuthSettings();
		_sut = CreateProvider(settings);

		// Act & Assert
		_sut.SupportsRefresh.ShouldBeFalse();
	}

	[Fact]
	public void SupportsRefresh_ReturnFalse_ForApiKeyAuthentication()
	{
		// Arrange
		var settings = CreateApiKeySettings();
		_sut = CreateProvider(settings);

		// Act & Assert
		_sut.SupportsRefresh.ShouldBeFalse();
	}

	#endregion

	#region Helper Methods

	private SecureElasticsearchAuthenticationProvider CreateProvider(ElasticsearchSecurityOptions settings)
	{
		var options = Options.Create(settings);
		return new SecureElasticsearchAuthenticationProvider(options, _mockKeyProvider, _mockHttpClientFactory, _logger);
	}

	private static ElasticsearchSecurityOptions CreateOAuth2EnabledSettings()
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
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateOAuth2OptionsWithoutAuthority()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				OAuth2 = new OAuth2Options
				{
					Enabled = true,
					Authority = null,
					ClientId = "test-client-id",
				},
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateServiceAccountEnabledSettings()
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
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateServiceAccountOptionsWithoutAuthority()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				OAuth2 = new OAuth2Options
				{
					Authority = null, // No authority configured
				},
				ServiceAccount = new ServiceAccountOptions
				{
					Enabled = true,
					AccountId = "test-service-account",
				},
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateBasicAuthSettings()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				Username = "elastic",
			},
		};
	}

	private static ElasticsearchSecurityOptions CreateApiKeySettings()
	{
		return new ElasticsearchSecurityOptions
		{
			Authentication = new AuthenticationOptions
			{
				ApiKeyId = "test-api-key",
			},
		};
	}

	private void ConfigureSuccessfulOAuth2RefreshScenario(bool includeNewRefreshToken = false, bool includeClientSecret = false)
	{
		ConfigureRefreshTokenAvailable();

		if (includeClientSecret)
		{
			_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
					"elasticsearch:oauth2:clientsecret",
					A<CancellationToken>.Ignored))
				.Returns(Task.FromResult<string?>("test-client-secret"));
		}
		else
		{
			_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
					"elasticsearch:oauth2:clientsecret",
					A<CancellationToken>.Ignored))
				.Returns(Task.FromResult<string?>(null));
		}

		var tokenResponse = new
		{
			access_token = "new-access-token",
			refresh_token = includeNewRefreshToken ? "new-refresh-token" : null,
			expires_in = 3600,
			token_type = "Bearer",
		};

		_mockHttpHandler.ConfigureResponse(HttpStatusCode.OK, JsonSerializer.Serialize(tokenResponse));
		ConfigureHttpClientFactory();
	}

	private void ConfigureSuccessfulServiceAccountRefreshScenario()
	{
		ConfigureServiceAccountCredentials();

		var tokenResponse = new
		{
			access_token = "new-service-token",
			expires_in = 3600,
			token_type = "Bearer",
		};

		_mockHttpHandler.ConfigureResponse(HttpStatusCode.OK, JsonSerializer.Serialize(tokenResponse));
		ConfigureHttpClientFactory();
	}

	private void ConfigureRefreshTokenAvailable()
	{
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:oauth2:refreshtoken",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>("existing-refresh-token"));

		// Configure existing access token as expired
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:oauth2:accesstoken",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>("existing-access-token"));

		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:oauth2:expiresat",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O")));

		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				A<string>.Ignored,
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult(true));
	}

	private void ConfigureNoRefreshTokenScenario()
	{
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:oauth2:refreshtoken",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>(null));

		// Configure existing access token as expired to trigger refresh
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:oauth2:accesstoken",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>("existing-access-token"));

		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:oauth2:expiresat",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O")));
	}

	private void ConfigureServiceAccountCredentials()
	{
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:serviceaccount:secret",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>("test-service-secret"));

		// Configure existing token as expired to trigger refresh
		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:serviceaccount:token",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>("existing-service-token"));

		_ = A.CallTo(() => _mockKeyProvider.GetSecretAsync(
				"elasticsearch:serviceaccount:expiresat",
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult<string?>(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O")));

		_ = A.CallTo(() => _mockKeyProvider.SetSecretAsync(
				A<string>.Ignored,
				A<string>.Ignored,
				A<SecretMetadata?>.Ignored,
				A<CancellationToken>.Ignored))
			.Returns(Task.FromResult(true));
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
internal sealed class MockHttpMessageHandler : HttpMessageHandler
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

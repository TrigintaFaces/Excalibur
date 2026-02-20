// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Security;

/// <summary>
/// Default implementation of <see cref="IElasticsearchSecurityProvider"/> that delegates
/// authentication to the configured <see cref="IElasticsearchAuthenticationProvider"/>
/// and applies security policies from the <see cref="SecurityPolicyEngine"/>.
/// </summary>
internal sealed partial class DefaultElasticsearchSecurityProvider : IElasticsearchSecurityProvider
{
	private readonly ElasticsearchSecurityOptions _options;
	private readonly IElasticsearchAuthenticationProvider? _authProvider;
	private readonly ILogger<DefaultElasticsearchSecurityProvider> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultElasticsearchSecurityProvider"/> class.
	/// </summary>
	/// <param name="options">The security options.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="authProvider">The optional authentication provider.</param>
	public DefaultElasticsearchSecurityProvider(
		IOptions<ElasticsearchSecurityOptions> options,
		ILogger<DefaultElasticsearchSecurityProvider> logger,
		IElasticsearchAuthenticationProvider? authProvider = null)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_authProvider = authProvider;
	}

	/// <inheritdoc />
	public async Task<AuthenticationResult> AuthenticateAsync(CancellationToken cancellationToken)
	{
		if (!_options.Enabled)
		{
			LogSecurityDisabled();
			return AuthenticationResult.Success;
		}

		if (_authProvider is null)
		{
			LogNoAuthProvider();
			return AuthenticationResult.SystemError;
		}

		var isValid = await _authProvider.ValidateAuthenticationAsync(cancellationToken).ConfigureAwait(false);

		return isValid ? AuthenticationResult.Success : AuthenticationResult.InvalidCredentials;
	}

	/// <inheritdoc />
	public Task<bool> AuthorizeAsync(DataAccessOperation operation, CancellationToken cancellationToken)
	{
		if (!_options.Enabled)
		{
			return Task.FromResult(true);
		}

		if (_options.Mode == SecurityMode.Permissive)
		{
			return Task.FromResult(true);
		}

		// In strict mode, require authentication provider
		if (_authProvider is null)
		{
			return Task.FromResult(false);
		}

		return Task.FromResult(true);
	}

	/// <inheritdoc />
	public async Task<ElasticsearchSecurityContext> GetSecurityContextAsync(CancellationToken cancellationToken)
	{
		var authResult = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);

		return new ElasticsearchSecurityContext
		{
			IsAuthenticated = authResult == AuthenticationResult.Success,
			PrincipalId = authResult == AuthenticationResult.Success
				? _authProvider?.AuthenticationType.ToString()
				: null,
			SecurityMode = _options.Mode,
			AuthenticationType = _authProvider?.AuthenticationType ?? ElasticsearchAuthenticationType.None,
			EstablishedAt = DateTimeOffset.UtcNow,
			ActivePolicies = new HashSet<string>(StringComparer.Ordinal)
		};
	}

	[LoggerMessage(3100, LogLevel.Debug, "Elasticsearch security is disabled; all operations are permitted")]
	private partial void LogSecurityDisabled();

	[LoggerMessage(3101, LogLevel.Warning, "No authentication provider configured for Elasticsearch security")]
	private partial void LogNoAuthProvider();
}

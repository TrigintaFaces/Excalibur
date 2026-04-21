// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net.Http.Headers;

using Excalibur.A3.Abstractions.Authorization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Policy.Cedar;

/// <summary>
/// Evaluates authorization decisions by querying a Cedar policy engine over HTTP.
/// Supports both local Cedar agents and Amazon Verified Permissions (AVP).
/// </summary>
internal sealed partial class CedarAuthorizationEvaluator : IAuthorizationEvaluator
{
	private static readonly MediaTypeHeaderValue s_jsonContentType = new("application/json");

	private readonly HttpClient _httpClient;
	private readonly CedarOptions _options;
	private readonly ILogger<CedarAuthorizationEvaluator> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CedarAuthorizationEvaluator"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client configured for the Cedar endpoint.</param>
	/// <param name="options">The Cedar configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public CedarAuthorizationEvaluator(
		HttpClient httpClient,
		IOptions<CedarOptions> options,
		ILogger<CedarAuthorizationEvaluator> logger)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_httpClient = httpClient;
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public async Task<AuthorizationDecision> EvaluateAsync(
		AuthorizationSubject subject,
		AuthorizationAction action,
		AuthorizationResource resource,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(subject);
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(resource);

		try
		{
			var inputJson = _options.Mode == CedarMode.AwsVerifiedPermissions
				? CedarInputMapper.MapToAvpJson(subject, action, resource, _options.PolicyStoreId ?? string.Empty)
				: CedarInputMapper.MapToLocalJson(subject, action, resource);

			using var content = new ByteArrayContent(inputJson);
			content.Headers.ContentType = s_jsonContentType;

			var requestUri = new Uri(_options.Endpoint, UriKind.Absolute);
			using var response = await _httpClient.PostAsync(requestUri, content, cancellationToken)
				.ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				LogCedarHttpError((int)response.StatusCode);
				return FailureDecision($"Cedar returned HTTP {(int)response.StatusCode}.");
			}

			var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken)
				.ConfigureAwait(false);

			var decision = _options.Mode == CedarMode.AwsVerifiedPermissions
				? CedarResponseParser.ParseAvp(responseBytes)
				: CedarResponseParser.ParseLocal(responseBytes);

			LogCedarEvaluationResult(subject.ActorId, action.Name, resource.Type, decision.Effect.ToString());
			return decision;
		}
		catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			LogCedarTimeout(_options.TimeoutMs);
			return FailureDecision($"Cedar request timed out after {_options.TimeoutMs}ms.");
		}
		catch (HttpRequestException ex)
		{
			LogCedarConnectionFailure(ex.Message);
			return FailureDecision($"Cedar connection failed: {ex.Message}");
		}
	}

	private AuthorizationDecision FailureDecision(string reason)
	{
		var effect = _options.FailClosed ? AuthorizationEffect.Deny : AuthorizationEffect.Permit;
		return new AuthorizationDecision(effect, reason);
	}

	[LoggerMessage(3200, LogLevel.Warning,
		"Cedar returned HTTP {StatusCode}.")]
	private partial void LogCedarHttpError(int statusCode);

	[LoggerMessage(3201, LogLevel.Debug,
		"Cedar evaluation: actor={ActorId}, action={ActionName}, resourceType={ResourceType}, effect={Effect}")]
	private partial void LogCedarEvaluationResult(string actorId, string actionName, string resourceType, string effect);

	[LoggerMessage(3202, LogLevel.Warning,
		"Cedar request timed out after {TimeoutMs}ms. Applying fail-closed/fail-open policy.")]
	private partial void LogCedarTimeout(int timeoutMs);

	[LoggerMessage(3203, LogLevel.Warning,
		"Cedar connection failed: {ErrorMessage}. Applying fail-closed/fail-open policy.")]
	private partial void LogCedarConnectionFailure(string errorMessage);
}

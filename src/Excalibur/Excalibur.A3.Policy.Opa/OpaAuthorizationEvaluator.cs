// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net.Http.Headers;

using Excalibur.A3.Abstractions.Authorization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.A3.Policy.Opa;

/// <summary>
/// Evaluates authorization decisions by querying an Open Policy Agent (OPA) server over HTTP.
/// </summary>
internal sealed partial class OpaAuthorizationEvaluator : IAuthorizationEvaluator
{
	private static readonly MediaTypeHeaderValue s_jsonContentType = new("application/json");

	private readonly HttpClient _httpClient;
	private readonly OpaOptions _options;
	private readonly ILogger<OpaAuthorizationEvaluator> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="OpaAuthorizationEvaluator"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client configured for the OPA server.</param>
	/// <param name="options">The OPA configuration options.</param>
	/// <param name="logger">The logger instance.</param>
	public OpaAuthorizationEvaluator(
		HttpClient httpClient,
		IOptions<OpaOptions> options,
		ILogger<OpaAuthorizationEvaluator> logger)
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
			var inputJson = OpaInputMapper.MapToInputJson(subject, action, resource);

			using var content = new ByteArrayContent(inputJson);
			content.Headers.ContentType = s_jsonContentType;

			var requestUri = new Uri(_options.PolicyPath, UriKind.Relative);
			using var response = await _httpClient.PostAsync(requestUri, content, cancellationToken)
				.ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				LogOpaHttpError((int)response.StatusCode, _options.PolicyPath);
				return FailureDecision($"OPA returned HTTP {(int)response.StatusCode}.");
			}

			var responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken)
				.ConfigureAwait(false);

			var decision = OpaResponseParser.Parse(responseBytes);

			LogOpaEvaluationResult(subject.ActorId, action.Name, resource.Type, decision.Effect.ToString());
			return decision;
		}
		catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			LogOpaTimeout(_options.TimeoutMs);
			return FailureDecision($"OPA request timed out after {_options.TimeoutMs}ms.");
		}
		catch (HttpRequestException ex)
		{
			LogOpaConnectionFailure(ex.Message);
			return FailureDecision($"OPA connection failed: {ex.Message}");
		}
	}

	private AuthorizationDecision FailureDecision(string reason)
	{
		var effect = _options.FailClosed ? AuthorizationEffect.Deny : AuthorizationEffect.Permit;
		return new AuthorizationDecision(effect, reason);
	}

	[LoggerMessage(3100, LogLevel.Warning,
		"OPA returned HTTP {StatusCode} for policy path '{PolicyPath}'.")]
	private partial void LogOpaHttpError(int statusCode, string policyPath);

	[LoggerMessage(3101, LogLevel.Debug,
		"OPA evaluation: actor={ActorId}, action={ActionName}, resourceType={ResourceType}, effect={Effect}")]
	private partial void LogOpaEvaluationResult(string actorId, string actionName, string resourceType, string effect);

	[LoggerMessage(3102, LogLevel.Warning,
		"OPA request timed out after {TimeoutMs}ms. Applying fail-closed/fail-open policy.")]
	private partial void LogOpaTimeout(int timeoutMs);

	[LoggerMessage(3103, LogLevel.Warning,
		"OPA connection failed: {ErrorMessage}. Applying fail-closed/fail-open policy.")]
	private partial void LogOpaConnectionFailure(string errorMessage);
}

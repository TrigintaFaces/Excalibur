// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net.Http.Headers;

using Excalibur.A3.Authorization;

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

		if (!_options.FailClosed)
		{
			// Fail-open is the single most dangerous authorization posture: an outage PERMITS everything.
			// Make choosing it loud so it can never be adopted silently.
			LogOpaFailOpenConfigured();
		}
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
				return FailureDecision($"OPA returned HTTP {(int)response.StatusCode}.", subject, action, resource);
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
			return FailureDecision($"OPA request timed out after {_options.TimeoutMs}ms.", subject, action, resource);
		}
		catch (HttpRequestException ex)
		{
			LogOpaConnectionFailure(ex.Message);
			return FailureDecision($"OPA connection failed: {ex.Message}", subject, action, resource);
		}
	}

	private AuthorizationDecision FailureDecision(
		string reason,
		AuthorizationSubject subject,
		AuthorizationAction action,
		AuthorizationResource resource)
	{
		if (_options.FailClosed)
		{
			return new AuthorizationDecision(AuthorizationEffect.Deny, reason);
		}

		// Fail-open PERMIT on an engine outage — audit-grade: name the actual effect and the principal, not
		// an ambiguous either/or, so a security review can see exactly what was allowed and why.
		LogOpaFailOpenPermit(subject.ActorId, action.Name, resource.Type, reason);
		return new AuthorizationDecision(AuthorizationEffect.Permit, reason);
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

	[LoggerMessage(3104, LogLevel.Warning,
		"OPA authorization is configured FAIL-OPEN (FailClosed=false): a policy-engine outage or error will PERMIT requests instead of denying them. This is insecure; set FailClosed=true unless this risk has been explicitly accepted.")]
	private partial void LogOpaFailOpenConfigured();

	[LoggerMessage(3105, LogLevel.Warning,
		"OPA FAIL-OPEN: PERMITTING actor={ActorId} action={ActionName} resourceType={ResourceType} because the policy engine was unreachable (FailClosed=false). Reason: {Reason}")]
	private partial void LogOpaFailOpenPermit(string actorId, string actionName, string resourceType, string reason);
}

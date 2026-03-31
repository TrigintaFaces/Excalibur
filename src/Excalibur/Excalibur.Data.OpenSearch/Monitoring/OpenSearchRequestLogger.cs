// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenSearch.Client;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Excalibur.Data.OpenSearch.Monitoring;

/// <summary>
/// Provides detailed logging of OpenSearch requests and responses with configurable verbosity and data sanitization.
/// </summary>
internal sealed partial class OpenSearchRequestLogger
{
	private readonly ILogger<OpenSearchRequestLogger> _logger;
	private readonly RequestLoggingOptions _settings;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenSearchRequestLogger" /> class.
	/// </summary>
	/// <param name="logger"> The logger for outputting request/response information. </param>
	/// <param name="options"> The request logging configuration options. </param>
	public OpenSearchRequestLogger(
		ILogger<OpenSearchRequestLogger> logger,
		IOptions<OpenSearchMonitoringOptions> options)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_settings = options?.Value?.RequestLogging ?? throw new ArgumentNullException(nameof(options));

		_jsonOptions = new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <summary>
	/// Logs the details of an OpenSearch request if logging is enabled.
	/// </summary>
	/// <param name="operationType"> The type of operation being performed. </param>
	/// <param name="request"> The OpenSearch request object. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	/// <param name="documentId"> The document ID for single-document operations. </param>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public void LogRequest(string operationType, object request, string? indexName = null, string? documentId = null)
	{
		if (!_settings.Enabled)
		{
			return;
		}

		try
		{
			const LogLevel logLevel = LogLevel.Debug;
			var message = new StringBuilder($"OpenSearch {operationType} request");

			if (!string.IsNullOrWhiteSpace(indexName))
			{
				_ = message.Append($" to index '{indexName}'");
			}

			if (!string.IsNullOrWhiteSpace(documentId))
			{
				_ = message.Append($" for document '{documentId}'");
			}

			var logData = new Dictionary<string, object>
				(StringComparer.Ordinal)
			{
				["OperationType"] = operationType,
				["RequestType"] = request.GetType().Name,
				["Timestamp"] = DateTimeOffset.UtcNow,
			};

			if (!string.IsNullOrWhiteSpace(indexName))
			{
				logData["IndexName"] = indexName;
			}

			if (!string.IsNullOrWhiteSpace(documentId))
			{
				logData["DocumentId"] = documentId;
			}

			// Add request routing information
			AddRoutingInformation(logData, request);

			// Add request body if enabled
			if (_settings.LogRequestBody)
			{
				var requestBody = SerializeRequestBody(request);
				if (!string.IsNullOrEmpty(requestBody))
				{
					logData["RequestBody"] = _settings.SanitizeSensitiveData
						? SanitizeContent(requestBody)
						: requestBody;
				}
			}

			_logger.Log(logLevel, "OpenSearch Request: {Message} {@LogData}", message.ToString(), logData);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to log OpenSearch request for operation: {OperationType}", operationType);
		}
	}

	/// <summary>
	/// Logs the details of an OpenSearch response if logging is enabled.
	/// </summary>
	/// <param name="operationType"> The type of operation that was performed. </param>
	/// <param name="response"> The OpenSearch response object. </param>
	/// <param name="duration"> The operation duration. </param>
	/// <param name="indexName"> The name of the index that was operated on. </param>
	/// <param name="documentId"> The document ID for single-document operations. </param>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public void LogResponse(
		string operationType,
		IResponse response,
		TimeSpan duration,
		string? indexName = null,
		string? documentId = null)
	{
		if (!_settings.Enabled)
		{
			return;
		}

		try
		{
			var isValidResponse = response.ApiCall?.HttpStatusCode is >= 200 and < 300;
			if (_settings.LogFailuresOnly && isValidResponse)
			{
				return;
			}

			var logLevel = isValidResponse ? LogLevel.Debug : LogLevel.Warning;
			var message = new StringBuilder($"OpenSearch {operationType} response");

			if (!string.IsNullOrWhiteSpace(indexName))
			{
				_ = message.Append($" from index '{indexName}'");
			}

			if (!string.IsNullOrWhiteSpace(documentId))
			{
				_ = message.Append($" for document '{documentId}'");
			}

			var logData = new Dictionary<string, object>
				(StringComparer.Ordinal)
			{
				["OperationType"] = operationType,
				["Success"] = isValidResponse,
				["DurationMs"] = duration.TotalMilliseconds,
				["Timestamp"] = DateTimeOffset.UtcNow,
			};

			if (!string.IsNullOrWhiteSpace(indexName))
			{
				logData["IndexName"] = indexName;
			}

			if (!string.IsNullOrWhiteSpace(documentId))
			{
				logData["DocumentId"] = documentId;
			}

			// Add HTTP details if available
			if (response.ApiCall != null)
			{
				AddHttpDetails(logData, response.ApiCall);
			}

			// Add error details if response failed
			if (!isValidResponse)
			{
				AddErrorDetails(logData, response);
			}

			// Add response body if enabled
			if (_settings.LogResponseBody && isValidResponse)
			{
				var responseBody = SerializeResponseBody(response);
				if (!string.IsNullOrEmpty(responseBody))
				{
					logData["ResponseBody"] = _settings.SanitizeSensitiveData
						? SanitizeContent(responseBody)
						: responseBody;
				}
			}

			_logger.Log(logLevel, "OpenSearch Response: {Message} {@LogData}", message.ToString(), logData);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to log OpenSearch response for operation: {OperationType}", operationType);
		}
	}

	/// <summary>
	/// Logs retry attempt information.
	/// </summary>
	/// <param name="operationType"> The type of operation being retried. </param>
	/// <param name="attemptNumber"> The current attempt number. </param>
	/// <param name="maxAttempts"> The maximum number of attempts. </param>
	/// <param name="delay"> The delay before this attempt. </param>
	/// <param name="exception"> The exception that triggered the retry. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	public void LogRetryAttempt(
		string operationType,
		int attemptNumber,
		int maxAttempts,
		TimeSpan delay,
		Exception exception,
		string? indexName = null)
	{
		if (!_settings.Enabled)
		{
			return;
		}

		try
		{
			var logData = new Dictionary<string, object>
				(StringComparer.Ordinal)
			{
				["OperationType"] = operationType,
				["AttemptNumber"] = attemptNumber,
				["MaxAttempts"] = maxAttempts,
				["DelayMs"] = delay.TotalMilliseconds,
				["ExceptionType"] = exception.GetType().Name,
				["ExceptionMessage"] = exception.Message,
				["Timestamp"] = DateTimeOffset.UtcNow,
			};

			if (!string.IsNullOrWhiteSpace(indexName))
			{
				logData["IndexName"] = indexName;
			}

			_logger.LogInformation(
				"OpenSearch operation {OperationType} retry attempt {AttemptNumber}/{MaxAttempts} after {DelayMs}ms due to {ExceptionType}: {ExceptionMessage} {@LogData}",
				operationType, attemptNumber, maxAttempts, delay.TotalMilliseconds, exception.GetType().Name, exception.Message, logData);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to log retry attempt for operation: {OperationType}", operationType);
		}
	}

	/// <summary>
	/// Logs circuit breaker state change information.
	/// </summary>
	/// <param name="fromState"> The previous circuit breaker state. </param>
	/// <param name="toState"> The new circuit breaker state. </param>
	/// <param name="operationType"> The operation type that triggered the state change. </param>
	/// <param name="reason"> The reason for the state change. </param>
	public void LogCircuitBreakerStateChange(string fromState, string toState, string? operationType = null, string? reason = null)
	{
		if (!_settings.Enabled)
		{
			return;
		}

		try
		{
			var logData = new Dictionary<string, object>
				(StringComparer.Ordinal)
			{ ["FromState"] = fromState, ["ToState"] = toState, ["Timestamp"] = DateTimeOffset.UtcNow, };

			if (!string.IsNullOrWhiteSpace(operationType))
			{
				logData["OperationType"] = operationType;
			}

			if (!string.IsNullOrWhiteSpace(reason))
			{
				logData["Reason"] = reason;
			}

			var logLevel = string.Equals(toState, "open", StringComparison.OrdinalIgnoreCase)
				? LogLevel.Warning
				: LogLevel.Information;

			_logger.Log(
				logLevel,
				"OpenSearch circuit breaker state changed from {FromState} to {ToState} {@LogData}",
				fromState, toState, logData);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to log circuit breaker state change from {FromState} to {ToState}", fromState, toState);
		}
	}

	/// <summary>
	/// Adds routing information from the request to the log data.
	/// </summary>
	private static void AddRoutingInformation(Dictionary<string, object> logData, object request)
	{
		try
		{
			var requestType = request.GetType();
			var routeValuesProperty = requestType.GetProperty("RouteValues");

			if (routeValuesProperty?.GetValue(request) is IDictionary<string, object> routeValues)
			{
				var routeInfo = new Dictionary<string, object>(StringComparer.Ordinal);
				foreach (var kvp in routeValues)
				{
					routeInfo[kvp.Key] = kvp.Value;
				}

				logData["RouteValues"] = routeInfo;
			}
		}
		catch (Exception)
		{
			// Ignore errors when extracting routing information
		}
	}

	/// <summary>
	/// Adds HTTP call details to the log data.
	/// </summary>
	private static void AddHttpDetails(Dictionary<string, object> logData, global::OpenSearch.Net.IApiCallDetails apiCallDetails)
	{
		try
		{
			if (apiCallDetails.HttpStatusCode.HasValue)
			{
				logData["HttpStatusCode"] = apiCallDetails.HttpStatusCode.Value;
			}

			logData["HttpMethod"] = apiCallDetails.HttpMethod.ToString();

			if (apiCallDetails.Uri != null)
			{
				logData["RequestUri"] = apiCallDetails.Uri.ToString();
			}
		}
		catch (Exception)
		{
			// Ignore errors when extracting HTTP details
		}
	}

	/// <summary>
	/// Adds error details to the log data for failed responses.
	/// </summary>
	private static void AddErrorDetails(Dictionary<string, object> logData, IResponse response)
	{
		try
		{
			if (response.ApiCall?.OriginalException != null)
			{
				var serverError = response.ApiCall.OriginalException;
				logData["ErrorType"] = serverError.GetType().Name;
				logData["ErrorReason"] = serverError.Message ?? "unknown";

				if (serverError.InnerException != null)
				{
					logData["ErrorCausedBy"] = new
					{
						Type = serverError.InnerException.GetType().Name,
						Reason = serverError.InnerException.Message,
					};
				}
			}

			if (response.ServerError?.Error != null)
			{
				logData["ServerErrorType"] = response.ServerError.Error.Type ?? "unknown";
				logData["ServerErrorReason"] = response.ServerError.Error.Reason ?? "unknown";
			}

			if (response.ApiCall?.DebugInformation != null)
			{
				logData["DebugInformation"] = response.ApiCall.DebugInformation;
			}
		}
		catch (Exception)
		{
			// Ignore errors when extracting error details
		}
	}

	/// <summary>
	/// Sanitizes sensitive data from content based on common patterns.
	/// </summary>
	private static string SanitizeContent(string content)
	{
		// Mask common sensitive patterns
		content = PasswordRegex().Replace(content, """
		                                           "password":"***"
		                                           """);
		content = ApiKeyRegex().Replace(content, """
		                                         "apiKey":"***"
		                                         """);
		content = TokenRegex().Replace(content, """
		                                        "token":"***"
		                                        """);
		content = AuthRegex().Replace(content, """
		                                       "authorization":"***"
		                                       """);

		return content;
	}

	[GeneratedRegex("""
	                "password"\s*:\s*"[^"]*"
	                """, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
	private static partial Regex PasswordRegex();

	[GeneratedRegex("""
	                "api[_-]?key"\s*:\s*"[^"]*"
	                """, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
	private static partial Regex ApiKeyRegex();

	[GeneratedRegex("""
	                "token"\s*:\s*"[^"]*"
	                """, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
	private static partial Regex TokenRegex();

	[GeneratedRegex("""
	                "authorization"\s*:\s*"[^"]*"
	                """, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
	private static partial Regex AuthRegex();

	/// <summary>
	/// Serializes a request object to JSON string.
	/// </summary>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private string? SerializeRequestBody(object request)
	{
		try
		{
			var json = JsonSerializer.Serialize(request, _jsonOptions);
			return TruncateIfNeeded(json);
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>
	/// Serializes a response object to JSON string.
	/// </summary>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private string? SerializeResponseBody(IResponse response)
	{
		try
		{
			var json = JsonSerializer.Serialize(response, _jsonOptions);
			return TruncateIfNeeded(json);
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>
	/// Truncates content if it exceeds the maximum body size.
	/// </summary>
	private string TruncateIfNeeded(string content)
	{
		if (content.Length <= _settings.MaxBodySizeBytes)
		{
			return content;
		}

		return content[..(_settings.MaxBodySizeBytes - 20)] + "...[TRUNCATED]";
	}
}

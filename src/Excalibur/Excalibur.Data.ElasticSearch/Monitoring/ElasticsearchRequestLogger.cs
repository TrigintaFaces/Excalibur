// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Provides detailed logging of Elasticsearch requests and responses with configurable verbosity and data sanitization.
/// </summary>
public sealed partial class ElasticsearchRequestLogger
{
	private readonly ILogger<ElasticsearchRequestLogger> _logger;
	private readonly RequestLoggingOptions _settings;
	private readonly JsonSerializerOptions _jsonOptions;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchRequestLogger" /> class.
	/// </summary>
	/// <param name="logger"> The logger for outputting request/response information. </param>
	/// <param name="options"> The request logging configuration options. </param>
	public ElasticsearchRequestLogger(
		ILogger<ElasticsearchRequestLogger> logger,
		IOptions<ElasticsearchMonitoringOptions> options)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_settings = options?.Value?.RequestLogging ?? throw new ArgumentNullException(nameof(options));

		_jsonOptions = new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
	}

	/// <summary>
	/// Logs the details of an Elasticsearch request if logging is enabled.
	/// </summary>
	/// <param name="operationType"> The type of operation being performed. </param>
	/// <param name="request"> The Elasticsearch request object. </param>
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
			var message = new StringBuilder($"Elasticsearch {operationType} request");

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

			_logger.Log(logLevel, "Elasticsearch Request: {Message} {@LogData}", message.ToString(), logData);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to log Elasticsearch request for operation: {OperationType}", operationType);
		}
	}

	/// <summary>
	/// Logs the details of an Elasticsearch response if logging is enabled.
	/// </summary>
	/// <param name="operationType"> The type of operation that was performed. </param>
	/// <param name="response"> The Elasticsearch response object. </param>
	/// <param name="duration"> The operation duration. </param>
	/// <param name="indexName"> The name of the index that was operated on. </param>
	/// <param name="documentId"> The document ID for single-document operations. </param>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public void LogResponse(
		string operationType,
		TransportResponse response,
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
			var isValidResponse = response.ApiCallDetails?.HttpStatusCode is >= 200 and < 300;
			if (_settings.LogFailuresOnly && isValidResponse)
			{
				return;
			}

			var logLevel = isValidResponse ? LogLevel.Debug : LogLevel.Warning;
			var message = new StringBuilder($"Elasticsearch {operationType} response");

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
			if (response.ApiCallDetails != null)
			{
				AddHttpDetails(logData, response.ApiCallDetails);
			}

			// Add response-specific metrics
			AddResponseMetrics(logData, response);

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

			_logger.Log(logLevel, "Elasticsearch Response: {Message} {@LogData}", message.ToString(), logData);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to log Elasticsearch response for operation: {OperationType}", operationType);
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
				"Elasticsearch operation {OperationType} retry attempt {AttemptNumber}/{MaxAttempts} after {DelayMs}ms due to {ExceptionType}: {ExceptionMessage} {@LogData}",
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

			var logLevel = string.Equals(toState.ToLowerInvariant(), "open", StringComparison.Ordinal)
				? LogLevel.Warning
				: LogLevel.Information;

			_logger.Log(
				logLevel,
				"Elasticsearch circuit breaker state changed from {FromState} to {ToState} {@LogData}",
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
	/// <param name="logData"> The log data dictionary to populate. </param>
	/// <param name="request"> The Elasticsearch request. </param>
	private static void AddRoutingInformation(Dictionary<string, object> logData, object request)
	{
		try
		{
			// Use reflection to extract routing information since IRequest interface is no longer available
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
	/// <param name="logData"> The log data dictionary to populate. </param>
	/// <param name="apiCallDetails"> The API call details from the response. </param>
	private static void AddHttpDetails(Dictionary<string, object> logData, ApiCallDetails apiCallDetails)
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
	/// Adds response-specific metrics to the log data.
	/// </summary>
	/// <param name="logData"> The log data dictionary to populate. </param>
	/// <param name="response"> The Elasticsearch response. </param>
	private static void AddResponseMetrics(Dictionary<string, object> logData, TransportResponse response)
	{
		try
		{
			switch (response)
			{
				case SearchResponse<object> { IsValidResponse: true } searchResponse:
					logData["HitsTotal"] = searchResponse.HitsMetadata?.Total?.Match(
						static totalHits => totalHits != null ? totalHits.Value : 0,
						static longValue => longValue) ?? 0;
					logData["TookMs"] = searchResponse.Took;
					logData["TimedOut"] = searchResponse.TimedOut;
					logData["Shards"] = new
					{
						Total = searchResponse.Shards?.Total ?? 0,
						Successful = searchResponse.Shards?.Successful ?? 0,
						Failed = searchResponse.Shards?.Failed ?? 0,
					};
					break;

				case BulkResponse { IsValidResponse: true } bulkResponse:
					logData["BulkItems"] = bulkResponse.Items.Count;
					logData["BulkErrors"] = bulkResponse.Errors;
					logData["TookMs"] = bulkResponse.Took;
					break;

				case IndexResponse { IsValidResponse: true } indexResponse:
					logData["DocumentResult"] = indexResponse.Result.ToString();
					logData["DocumentVersion"] = indexResponse.Version;
					logData["Shards"] = new
					{
						Total = indexResponse.Shards?.Total ?? 0,
						Successful = indexResponse.Shards?.Successful ?? 0,
						Failed = indexResponse.Shards?.Failed ?? 0,
					};
					break;

				case UpdateResponse<object> { IsValidResponse: true } updateResponse:
					logData["DocumentResult"] = updateResponse.Result.ToString();
					logData["DocumentVersion"] = updateResponse.Version;
					break;

				case DeleteResponse { IsValidResponse: true } deleteResponse:
					logData["DocumentResult"] = deleteResponse.Result.ToString();
					logData["DocumentVersion"] = deleteResponse.Version;
					break;
				default:
					break;
			}
		}
		catch (Exception)
		{
			// Ignore errors when extracting response metrics
		}
	}

	/// <summary>
	/// Adds error details to the log data for failed responses.
	/// </summary>
	/// <param name="logData"> The log data dictionary to populate. </param>
	/// <param name="response"> The failed Elasticsearch response. </param>
	private static void AddErrorDetails(Dictionary<string, object> logData, TransportResponse response)
	{
		try
		{
			// Server error details are accessed through ApiCallDetails
			if (response.ApiCallDetails?.OriginalException != null)
			{
				var serverError = response.ApiCallDetails.OriginalException;
				logData["ErrorType"] = serverError.GetType().Name;
				logData["ErrorReason"] = serverError.Message ?? "unknown";

				// Add inner exception details if available
				if (serverError.InnerException != null)
				{
					logData["ErrorCausedBy"] = new
					{
						Type = serverError.InnerException.GetType().Name,
						Reason = serverError.InnerException.Message,
					};
				}
			}

			// Add debug information if available
			if (response.ApiCallDetails?.DebugInformation != null)
			{
				logData["DebugInformation"] = response.ApiCallDetails.DebugInformation;
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
	/// <param name="content"> The content to sanitize. </param>
	/// <returns> The sanitized content with sensitive data masked. </returns>
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
	/// <param name="request"> The request object to serialize. </param>
	/// <returns> The serialized JSON string, or null if serialization fails. </returns>
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
	/// <param name="response"> The response object to serialize. </param>
	/// <returns> The serialized JSON string, or null if serialization fails. </returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private string? SerializeResponseBody(TransportResponse response)
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
	/// <param name="content"> The content to potentially truncate. </param>
	/// <returns> The truncated content with indication if truncation occurred. </returns>
	private string TruncateIfNeeded(string content)
	{
		if (content.Length <= _settings.MaxBodySizeBytes)
		{
			return content;
		}

		return content[..(_settings.MaxBodySizeBytes - 20)] + "...[TRUNCATED]";
	}
}

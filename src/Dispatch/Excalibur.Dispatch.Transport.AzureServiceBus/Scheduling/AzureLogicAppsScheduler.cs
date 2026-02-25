// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Azure.Core;
using Azure.Identity;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Logic Apps implementation of message scheduler.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="AzureLogicAppsScheduler" /> class. </remarks>
/// <param name="options"> The scheduler options. </param>
/// <param name="logger"> The logger. </param>
/// <param name="httpClient"> Optional HTTP client override. </param>
/// <param name="credential"> Optional credential override. </param>
public sealed class AzureLogicAppsScheduler(
	IOptions<AzureLogicAppsSchedulerOptions> options,
	ILogger<AzureLogicAppsScheduler> logger,
	HttpClient? httpClient = null,
	TokenCredential? credential = null) : IMessageScheduler
{
	private const string ManagementScope = "https://management.azure.com/.default";
	private const string ApiVersion = "2016-06-01";

	private static readonly JsonSerializerOptions SerializerOptions =
		new(JsonSerializerDefaults.Web);

	private readonly AzureLogicAppsSchedulerOptions _options =
		options.Value ?? throw new ArgumentNullException(nameof(options));

	private readonly HttpClient _httpClient = httpClient ?? new HttpClient();
	private readonly TokenCredential _credential = credential ?? new DefaultAzureCredential();
	private Uri? _resolvedCallbackUrl;

	/// <inheritdoc />
	public Task<string> ScheduleAsync(
		IDispatchMessage message,
		DateTimeOffset scheduleTime,
		CancellationToken cancellationToken) =>
		ScheduleObjectAsync(message, message.GetType(), scheduleTime, cancellationToken);

	/// <inheritdoc />
	public Task<string> ScheduleMessageAsync<T>(
		T message,
		DateTimeOffset scheduledTime,
		CancellationToken cancellationToken) =>
		ScheduleObjectAsync(message, typeof(T), scheduledTime, cancellationToken);

	/// <inheritdoc />
	public async Task<bool> CancelAsync(
		string scheduleId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(scheduleId);

		var workflowName = ResolveWorkflowName();
		var url = BuildRunCancelUrl(workflowName, scheduleId);

		using var response = await SendManagementRequestAsync(
				HttpMethod.Post,
				url,
				cancellationToken)
			.ConfigureAwait(false);

		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return false;
		}

		_ = response.EnsureSuccessStatusCode();
		return true;
	}

	/// <inheritdoc />
	public async Task CancelScheduledMessageAsync(
		string scheduleId,
		CancellationToken cancellationToken) =>
		_ = await CancelAsync(scheduleId, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public async Task<ScheduleInfo?> GetScheduleAsync(
		string scheduleId,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(scheduleId);

		var workflowName = ResolveWorkflowName();
		var url = BuildRunUrl(workflowName, scheduleId);

		using var response = await SendManagementRequestAsync(
				HttpMethod.Get,
				url,
				cancellationToken)
			.ConfigureAwait(false);

		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			return null;
		}

		_ = response.EnsureSuccessStatusCode();

		var payload = await response.Content
			.ReadAsStringAsync(cancellationToken)
			.ConfigureAwait(false);

		return ParseScheduleInfo(scheduleId, payload);
	}

	private static string BuildPayload(
		object message,
		Type messageType,
		DateTimeOffset scheduleTime)
	{
		var payloadElement = JsonSerializer.SerializeToElement(
			message,
			messageType,
			SerializerOptions);

		var envelope = new ScheduledMessageEnvelope(
			messageType.FullName ?? messageType.Name,
			payloadElement,
			scheduleTime);

		return JsonSerializer.Serialize(envelope, SerializerOptions);
	}

	private static async Task<string?> TryExtractRunIdAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		if (response.Headers.TryGetValues("x-ms-workflow-run-id", out var runIdValues))
		{
			return runIdValues.FirstOrDefault();
		}

		if (response.Headers.Location is not null)
		{
			var segments = response.Headers.Location.Segments;
			if (segments.Length > 0)
			{
				return segments[^1].Trim('/');
			}
		}

		var content = await response.Content
			.ReadAsStringAsync(cancellationToken)
			.ConfigureAwait(false);

		if (string.IsNullOrWhiteSpace(content))
		{
			return null;
		}

		try
		{
			using var document = JsonDocument.Parse(content);
			if (document.RootElement.TryGetProperty("runId", out var runIdProperty))
			{
				return runIdProperty.GetString();
			}
		}
		catch (JsonException)
		{
			return null;
		}

		return null;
	}

	private static ScheduleInfo? ParseScheduleInfo(string scheduleId, string payload)
	{
		if (string.IsNullOrWhiteSpace(payload))
		{
			return null;
		}

		try
		{
			using var document = JsonDocument.Parse(payload);
			if (!document.RootElement.TryGetProperty("properties", out var properties))
			{
				return null;
			}

			var status = properties.TryGetProperty("status", out var statusProperty)
				? statusProperty.GetString()
				: null;

			var startTime = TryGetDateTimeOffset(properties, "startTime");
			var createdTime = TryGetDateTimeOffset(properties, "createdTime")
							  ?? startTime
							  ?? DateTimeOffset.UtcNow;

			return new ScheduleInfo
			{
				ScheduleId = scheduleId,
				ScheduledTime = startTime ?? DateTimeOffset.UtcNow,
				CreatedTime = createdTime,
				Status = MapStatus(status),
				LastError = TryGetError(properties),
			};
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static DateTimeOffset? TryGetDateTimeOffset(
		JsonElement element,
		string propertyName)
	{
		if (!element.TryGetProperty(propertyName, out var value))
		{
			return null;
		}

		if (value.ValueKind == JsonValueKind.String &&
			DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
		{
			return parsed;
		}

		return null;
	}

	private static string? TryGetError(JsonElement properties)
	{
		if (!properties.TryGetProperty("error", out var error))
		{
			return null;
		}

		if (error.ValueKind == JsonValueKind.Object &&
			error.TryGetProperty("message", out var message))
		{
			return message.GetString();
		}

		return error.ToString();
	}

	private static ScheduleStatus MapStatus(string? status) =>
		status?.ToUpperInvariant() switch
		{
			"RUNNING" or "WAITING" => ScheduleStatus.InProgress,
			"SUCCEEDED" => ScheduleStatus.Completed,
			"FAILED" => ScheduleStatus.Failed,
			"CANCELLED" or "CANCELED" => ScheduleStatus.Cancelled,
			_ => ScheduleStatus.Scheduled,
		};

	private static bool TryParseCallbackUrl(string payload, out Uri callbackUrl)
	{
		callbackUrl = null!;
		if (string.IsNullOrWhiteSpace(payload))
		{
			return false;
		}

		try
		{
			using var document = JsonDocument.Parse(payload);
			if (document.RootElement.TryGetProperty("value", out var value) &&
				value.ValueKind == JsonValueKind.String)
			{
				var urlValue = value.GetString();
				if (!string.IsNullOrWhiteSpace(urlValue) &&
					Uri.TryCreate(urlValue, UriKind.Absolute, out var parsed) &&
					parsed is not null)
				{
					callbackUrl = parsed;
					return true;
				}
			}

			if (document.RootElement.TryGetProperty("basePath", out var basePathElement) &&
				document.RootElement.TryGetProperty("relativePath", out var relativePathElement))
			{
				var basePath = basePathElement.GetString();
				var relativePath = relativePathElement.GetString();
				if (!string.IsNullOrWhiteSpace(basePath) &&
					!string.IsNullOrWhiteSpace(relativePath) &&
					Uri.TryCreate(basePath, UriKind.Absolute, out var baseUri) &&
					Uri.TryCreate(baseUri, relativePath, out var composite))
				{
					callbackUrl = composite;
					return true;
				}
			}
		}
		catch (JsonException)
		{
			return false;
		}

		return false;
	}

	private async Task<string> ScheduleObjectAsync(
		object message,
		Type messageType,
		DateTimeOffset scheduleTime,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(messageType);

		if (_options.CallbackUrl is null)
		{
			_resolvedCallbackUrl ??= await ResolveCallbackUrlAsync(cancellationToken)
				.ConfigureAwait(false);
		}

		logger.LogInformation(
			"Scheduling message {MessageType} for {ScheduleTime} using Azure Logic Apps",
			messageType.Name,
			scheduleTime);

		var payload = BuildPayload(message, messageType, scheduleTime);

		using var response = await SendWithRetriesAsync(
				() =>
				{
					var request = new HttpRequestMessage(
						HttpMethod.Post,
						_options.CallbackUrl ?? _resolvedCallbackUrl)
					{
						Content = new StringContent(
							payload,
							Encoding.UTF8,
							"application/json"),
					};
					return request;
				},
				cancellationToken)
			.ConfigureAwait(false);

		_ = response.EnsureSuccessStatusCode();

		var scheduleId = await TryExtractRunIdAsync(response, cancellationToken)
			.ConfigureAwait(false);

		if (string.IsNullOrWhiteSpace(scheduleId))
		{
			scheduleId = Guid.NewGuid().ToString("N");
			logger.LogWarning(
				"Azure Logic Apps response did not include a run id. Generated schedule id {ScheduleId}.",
				scheduleId);
		}

		return scheduleId;
	}

	private async Task<HttpResponseMessage> SendManagementRequestAsync(
		HttpMethod method,
		string url,
		CancellationToken cancellationToken)
	{
		var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

		return await SendWithRetriesAsync(
				() =>
				{
					var request = new HttpRequestMessage(method, url);
					request.Headers.Authorization =
						new AuthenticationHeaderValue("Bearer", token);
					return request;
				},
				cancellationToken)
			.ConfigureAwait(false);
	}

	private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
	{
		var token = await _credential
			.GetTokenAsync(
				new TokenRequestContext([ManagementScope]),
				cancellationToken)
			.ConfigureAwait(false);

		return token.Token;
	}

	private async Task<HttpResponseMessage> SendWithRetriesAsync(
		Func<HttpRequestMessage> requestFactory,
		CancellationToken cancellationToken)
	{
		var attempt = 0;
		var delay = TimeSpan.FromSeconds(_options.RetryDelaySeconds);

		while (true)
		{
			attempt++;
			using var request = requestFactory();
			var response = await _httpClient
				.SendAsync(request, cancellationToken)
				.ConfigureAwait(false);

			if (response.IsSuccessStatusCode || attempt > _options.MaxRetries)
			{
				return response;
			}

			response.Dispose();
			if (delay > TimeSpan.Zero)
			{
				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	private string ResolveWorkflowName()
	{
		if (!string.IsNullOrWhiteSpace(_options.WorkflowName))
		{
			return _options.WorkflowName;
		}

		if (!string.IsNullOrWhiteSpace(_options.LogicAppName))
		{
			return _options.LogicAppName;
		}

		throw new InvalidOperationException(
			"WorkflowName or LogicAppName must be configured for Azure Logic Apps scheduling.");
	}

	private string BuildRunUrl(string workflowName, string runId) =>
		$"{BuildRunsBaseUrl(workflowName)}/{runId}?api-version={ApiVersion}";

	private string BuildRunCancelUrl(string workflowName, string runId) =>
		$"{BuildRunsBaseUrl(workflowName)}/{runId}/cancel?api-version={ApiVersion}";

	private string BuildRunsBaseUrl(string workflowName)
	{
		if (string.IsNullOrWhiteSpace(_options.SubscriptionId))
		{
			throw new InvalidOperationException(
				"SubscriptionId must be configured for Azure Logic Apps scheduling.");
		}

		if (string.IsNullOrWhiteSpace(_options.ResourceGroupName))
		{
			throw new InvalidOperationException(
				"ResourceGroupName must be configured for Azure Logic Apps scheduling.");
		}

		return
			$"https://management.azure.com/subscriptions/{_options.SubscriptionId}/resourceGroups/{_options.ResourceGroupName}/providers/Microsoft.Logic/workflows/{workflowName}/runs";
	}

	private string BuildTriggerCallbackUrl(string workflowName, string triggerName)
	{
		if (string.IsNullOrWhiteSpace(_options.SubscriptionId))
		{
			throw new InvalidOperationException(
				"SubscriptionId must be configured for Azure Logic Apps scheduling.");
		}

		if (string.IsNullOrWhiteSpace(_options.ResourceGroupName))
		{
			throw new InvalidOperationException(
				"ResourceGroupName must be configured for Azure Logic Apps scheduling.");
		}

		return
			$"https://management.azure.com/subscriptions/{_options.SubscriptionId}/resourceGroups/{_options.ResourceGroupName}/providers/Microsoft.Logic/workflows/{workflowName}/triggers/{triggerName}/listCallbackUrl?api-version={ApiVersion}";
	}

	private async Task<Uri> ResolveCallbackUrlAsync(CancellationToken cancellationToken)
	{
		var workflowName = ResolveWorkflowName();
		var triggerName = _options.TriggerName;
		if (string.IsNullOrWhiteSpace(triggerName))
		{
			throw new InvalidOperationException(
				"TriggerName must be configured when CallbackUrl is not provided.");
		}

		var url = BuildTriggerCallbackUrl(workflowName, triggerName);
		using var response = await SendManagementRequestAsync(
				HttpMethod.Post,
				url,
				cancellationToken)
			.ConfigureAwait(false);

		_ = response.EnsureSuccessStatusCode();

		var payload = await response.Content
			.ReadAsStringAsync(cancellationToken)
			.ConfigureAwait(false);

		if (TryParseCallbackUrl(payload, out var callbackUrl))
		{
			logger.LogInformation(
				"Resolved Azure Logic Apps callback URL for workflow {WorkflowName} trigger {TriggerName}.",
				workflowName,
				triggerName);
			return callbackUrl;
		}

		throw new InvalidOperationException(
			"Azure Logic Apps callback URL could not be resolved from the management API response.");
	}

	private sealed record ScheduledMessageEnvelope(
		string MessageType,
		JsonElement Payload,
		DateTimeOffset ScheduledTime);
}

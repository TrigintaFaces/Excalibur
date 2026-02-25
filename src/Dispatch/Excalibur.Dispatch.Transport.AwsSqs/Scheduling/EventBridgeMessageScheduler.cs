// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using Amazon.Scheduler;
using Amazon.Scheduler.Model;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS EventBridge implementation of message scheduler.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="EventBridgeMessageScheduler" /> class. </remarks>
/// <param name="options"> The scheduler options. </param>
/// <param name="scheduler"> The AWS Scheduler client. </param>
/// <param name="logger"> The logger. </param>
public class EventBridgeMessageScheduler(
	IOptions<EventBridgeSchedulerOptions> options,
	IAmazonScheduler scheduler,
	ILogger<EventBridgeMessageScheduler> logger) : IMessageScheduler
{
	private static readonly JsonSerializerOptions SerializerOptions =
		new(JsonSerializerDefaults.Web);

	private readonly EventBridgeSchedulerOptions _options =
		options.Value ?? throw new ArgumentNullException(nameof(options));

	private readonly IAmazonScheduler _scheduler =
		scheduler ?? throw new ArgumentNullException(nameof(scheduler));

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

		var (groupName, name) = ParseScheduleId(scheduleId, _options.ScheduleGroupName);

		logger.LogInformation("Cancelling schedule {ScheduleId}", scheduleId);

		try
		{
			_ = await _scheduler
				.DeleteScheduleAsync(
					new DeleteScheduleRequest { GroupName = groupName, Name = name, },
					cancellationToken)
				.ConfigureAwait(false);
			return true;
		}
		catch (AmazonSchedulerException ex) when (IsNotFound(ex))
		{
			return false;
		}
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

		var (groupName, name) = ParseScheduleId(scheduleId, _options.ScheduleGroupName);

		logger.LogDebug("Getting schedule {ScheduleId}", scheduleId);

		try
		{
			var response = await _scheduler
				.GetScheduleAsync(
					new GetScheduleRequest { GroupName = groupName, Name = name, },
					cancellationToken)
				.ConfigureAwait(false);

			var scheduledTime = ToDateTimeOffset(response.StartDate)
								?? ToDateTimeOffset(response.EndDate)
								?? DateTimeOffset.UtcNow;
			var createdTime = ToDateTimeOffset(response.CreationDate) ??
							  DateTimeOffset.UtcNow;

			return new ScheduleInfo
			{
				ScheduleId = scheduleId,
				ScheduledTime = scheduledTime,
				CreatedTime = createdTime,
				Status = MapStatus(response.State),
			};
		}
		catch (AmazonSchedulerException ex) when (IsNotFound(ex))
		{
			return null;
		}
	}

	private static string BuildScheduleExpression(DateTimeOffset scheduleTime) =>
		$"at({scheduleTime.ToUniversalTime():yyyy-MM-ddTHH:mm:ss})";

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.SerializeToElement(Object, Type, JsonSerializerOptions)")]
	private static string BuildInputPayload(
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

	private static (string groupName, string name) ParseScheduleId(
		string scheduleId,
		string defaultGroup)
	{
		var trimmed = scheduleId.Trim();
		if (trimmed.Contains('/', StringComparison.Ordinal))
		{
			var split = trimmed.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
			if (split.Length == 2)
			{
				return (split[0], split[1]);
			}
		}

		if (trimmed.Contains(':', StringComparison.Ordinal))
		{
			var split = trimmed.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
			if (split.Length == 2)
			{
				return (split[0], split[1]);
			}
		}

		return (defaultGroup, trimmed);
	}

	private static bool IsEventBridgeTarget(string arn) =>
		arn.Contains(":events:", StringComparison.OrdinalIgnoreCase);

	private static bool IsNotFound(AmazonSchedulerException ex) =>
		ex.StatusCode == HttpStatusCode.NotFound ||
		string.Equals(ex.ErrorCode, "ResourceNotFoundException", StringComparison.OrdinalIgnoreCase);

	private static bool IsConflict(AmazonSchedulerException ex) =>
		ex.StatusCode == HttpStatusCode.Conflict ||
		string.Equals(ex.ErrorCode, "ResourceAlreadyExistsException", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(ex.ErrorCode, "ConflictException", StringComparison.OrdinalIgnoreCase);

	private static ScheduleStatus MapStatus(ScheduleState? state)
	{
		if (ScheduleState.DISABLED.Equals(state))
		{
			return ScheduleStatus.Cancelled;
		}

		return ScheduleStatus.Scheduled;
	}

	private static DateTimeOffset? ToDateTimeOffset(DateTime? value)
	{
		if (!value.HasValue)
		{
			return null;
		}

		var dateTime = value.Value;
		if (dateTime.Kind == DateTimeKind.Unspecified)
		{
			dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
		}

		return new DateTimeOffset(dateTime);
	}

	private async Task<string> ScheduleObjectAsync(
		object message,
		Type messageType,
		DateTimeOffset scheduleTime,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(messageType);

		if (string.IsNullOrWhiteSpace(_options.TargetArn))
		{
			throw new InvalidOperationException(
				"EventBridge scheduler TargetArn must be configured.");
		}

		if (string.IsNullOrWhiteSpace(_options.RoleArn))
		{
			throw new InvalidOperationException(
				"EventBridge scheduler RoleArn must be configured.");
		}

		var scheduleId = Guid.NewGuid().ToString("N");
		var groupName = _options.ScheduleGroupName;

		await EnsureScheduleGroupAsync(groupName, cancellationToken)
			.ConfigureAwait(false);

		var scheduleExpression = BuildScheduleExpression(scheduleTime);
		var input = BuildInputPayload(message, messageType, scheduleTime);

		var request = new CreateScheduleRequest
		{
			Name = scheduleId,
			GroupName = groupName,
			ScheduleExpression = scheduleExpression,
			ScheduleExpressionTimezone = string.IsNullOrWhiteSpace(_options.ScheduleTimeZone)
				? "UTC"
				: _options.ScheduleTimeZone,
			FlexibleTimeWindow = new FlexibleTimeWindow { Mode = FlexibleTimeWindowMode.OFF },
			ActionAfterCompletion = ActionAfterCompletion.DELETE,
			Target = BuildTarget(messageType, input),
		};

		logger.LogInformation(
			"Scheduling message {MessageType} for {ScheduleTime}",
			messageType.Name,
			scheduleTime);

		_ = await _scheduler
			.CreateScheduleAsync(request, cancellationToken)
			.ConfigureAwait(false);

		return scheduleId;
	}

	private async Task EnsureScheduleGroupAsync(
		string groupName,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(groupName) ||
			groupName.Equals("default", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		try
		{
			_ = await _scheduler
				.CreateScheduleGroupAsync(
					new CreateScheduleGroupRequest { Name = groupName },
					cancellationToken)
				.ConfigureAwait(false);
		}
		catch (AmazonSchedulerException ex) when (IsConflict(ex))
		{
			// Group already exists.
		}
	}

	private Target BuildTarget(Type messageType, string input)
	{
		var target = new Target
		{
			Arn = _options.TargetArn,
			RoleArn = _options.RoleArn,
			Input = input,
			RetryPolicy = new Amazon.Scheduler.Model.RetryPolicy { MaximumRetryAttempts = _options.MaxRetries, },
		};

		if (!string.IsNullOrWhiteSpace(_options.DeadLetterQueueArn))
		{
			target.DeadLetterConfig = new DeadLetterConfig { Arn = _options.DeadLetterQueueArn, };
		}

		if (IsEventBridgeTarget(_options.TargetArn))
		{
			target.EventBridgeParameters = new EventBridgeParameters { DetailType = messageType.Name, Source = "dispatch", };
		}

		return target;
	}

	private sealed record ScheduledMessageEnvelope(
		string MessageType,
		JsonElement Payload,
		DateTimeOffset ScheduledTime);
}

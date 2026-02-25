// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Excalibur.A3.Audit.Events;
using Excalibur.A3.Diagnostics;
using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Jobs;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Domain;

using Microsoft.Extensions.Logging;

using ApiException = Excalibur.Dispatch.Abstractions.ApiException;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using OutboxMessage = Excalibur.Dispatch.Delivery.OutboxMessage;
using TenantId = Excalibur.Dispatch.Abstractions.TenantId;

namespace Excalibur.A3.Audit;

/// <summary>
/// Dispatch middleware that records audit information for <see cref="IAmAuditable" /> actions.
/// </summary>
public sealed partial class AuditMiddleware(
	IActivityContext activityContext,
	IAuditMessagePublisher auditMessagePublisher,
	ILogger<AuditMiddleware> logger,
	IOutboxDispatcher outbox) : IDispatchMiddleware
{
	/// <summary>
	/// Gets the middleware execution stage. Audit middleware runs at the end of the pipeline.
	/// </summary>
	/// <value> The middleware execution stage, set to <see cref="DispatchMiddlewareStage.End" />. </value>
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.End;

	/// <summary>
	/// Executes the audit middleware logic to record audit information for auditable actions.
	/// </summary>
	/// <param name="message"> The dispatch message being processed. </param>
	/// <param name="context"> The message context containing metadata. </param>
	/// <param name="nextDelegate"> The next middleware delegate in the pipeline. </param>
	/// <param name="cancellationToken"> Token to cancel the operation. </param>
	/// <returns> The result of the message processing. </returns>
	[RequiresDynamicCode("Audit middleware uses reflection for dynamic type resolution.")]
	[RequiresUnreferencedCode("Audit middleware may access types that could be trimmed.")]
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		if (message is not IAmAuditable)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var activityAudit = new ActivityAudit<IDispatchMessage, object?>(activityContext, message);
		IMessageResult result;
		try
		{
			result = await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
			if (result is IMessageResult<object> typed)
			{
				activityAudit.Response = typed.ReturnValue;
			}

			activityAudit.StatusCode = result.Succeeded ? 200 : 500;
		}
		catch (ApiException ex)
		{
			activityAudit.Exception = ex;
			activityAudit.StatusCode = ex.StatusCode;
			throw;
		}
		catch (Exception ex)
		{
			activityAudit.Exception = ex;
			activityAudit.StatusCode = 500;
			throw;
		}
		finally
		{
			activityAudit.Timestamp = DateTimeOffset.UtcNow;
			if (!IsJobWithNoWorkPerformed(activityAudit.Response))
			{
				var activityAudited = new ActivityAudited(activityAudit);
				try
				{
					await auditMessagePublisher.PublishAsync(activityAudited, activityContext, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogFailure(activityAudited, ex);
					await SaveToOutboxAsync(activityAudited, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		return result;
	}

	private static bool IsJobWithNoWorkPerformed(object? response) => response is JobResult job && JobResult.NoWorkPerformed.Equals(job);

	[LoggerMessage(A3EventId.AuditPublishFailure, LogLevel.Critical,
		"[AUDIT]==> {Error} occurred while publishing an ActivityAudited event! The event will be queued to the Outbox. \n{ApplicationName}/{ActivityName}?u={UserName}\n[AUDIT]<== ERROR 500: {Message}")]
	private partial void LogAuditPublishFailure(Exception exception, string error, string applicationName, string activityName,
		string userName, string message);

	private void LogFailure(ActivityAudited activityAudited, Exception exception)
	{
		var dictionary =
			new Dictionary<string, object>(StringComparer.Ordinal) { { nameof(CorrelationId), activityAudited.CorrelationId } };

		if (activityAudited.TenantId is not null)
		{
			dictionary.Add(nameof(TenantId), activityAudited.TenantId);
		}

		using (logger.BeginScope(dictionary))
		{
			if (logger.IsEnabled(LogLevel.Critical))
			{
				LogAuditPublishFailure(
					exception,
					exception.GetType().Name,
					activityAudited.ApplicationName,
					activityAudited.ActivityName,
					activityAudited.UserName,
					exception.Message);
			}
		}
	}

	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, JsonSerializerOptions)")]
	private async Task SaveToOutboxAsync(ActivityAudited activityAudited, CancellationToken cancellationToken)
	{
		var headers = new Dictionary<string, string>
			(StringComparer.Ordinal)
			{
				{
					ExcaliburHeaderNames.RaisedBy,
					activityContext.AccessToken() is { } token ? JsonSerializer.Serialize(new RaisedBy(token)) : "Unknown"
				},
				{ ExcaliburHeaderNames.CorrelationId, activityAudited.CorrelationId.ToString() },
			};

		if (activityAudited.TenantId is not null)
		{
			headers.Add(ExcaliburHeaderNames.TenantId, activityAudited.TenantId);
		}

		var message = new OutboxMessage(
			Uuid7Extensions.GenerateString(),
			nameof(ActivityAudited),
			JsonSerializer.Serialize(headers),
			JsonSerializer.Serialize(activityAudited),
			DateTimeOffset.UtcNow);

		_ = await outbox.SaveMessagesAsync([message], cancellationToken).ConfigureAwait(false);
	}
}

// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using Excalibur.A3.Audit.Events;
using Excalibur.A3.Audit.Requests;
using Excalibur.Application.Requests.Jobs;
using Excalibur.Core;
using Excalibur.Core.Extensions;
using Excalibur.Data.Outbox;
using Excalibur.Domain;

using MediatR;

using Microsoft.Extensions.Logging;

namespace Excalibur.A3.Audit;

/// <summary>
///     Implements behavior for auditing activities during request processing in the pipeline.
/// </summary>
/// <typeparam name="TRequest"> The type of the request being processed. </typeparam>
/// <typeparam name="TResponse"> The type of the response returned by the request handler. </typeparam>
/// <param name="context"> The activity context containing metadata about the current activity. </param>
/// <param name="auditMessagePublisher"> The publisher for sending audit messages. </param>
/// <param name="logger"> The logger used to record audit-related information and errors. </param>
/// <param name="outbox"> The outbox used to queue messages for eventual consistency. </param>
public class AuditBehavior<TRequest, TResponse>(
	IActivityContext context,
	IAuditMessagePublisher auditMessagePublisher,
	ILogger<AuditBehavior<TRequest, TResponse>> logger,
	IOutbox outbox) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
	/// <inheritdoc />
	public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(next);

		if (request is not IAmAuditable)
		{
			return await next().ConfigureAwait(false);
		}

		var activityAudit = new ActivityAudit<TRequest, TResponse>(context, request);

		try
		{
			return (await activityAudit.DecorateAsync(next.Invoke).ConfigureAwait(false))!;
		}
		finally
		{
			if (!IsJobWithNoWorkPerformed(activityAudit.Response!))
			{
				var activityAudited = new ActivityAudited(activityAudit);

				try
				{
					await auditMessagePublisher.PublishAsync(activityAudited, context).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogFailure(activityAudited, ex);
					await SaveToOutbox(activityAudited, cancellationToken).ConfigureAwait(false);
				}
			}
		}
	}

	/// <summary>
	///     Determines if the response indicates that no work was performed for a job.
	/// </summary>
	/// <param name="response"> The response to check. </param>
	/// <returns> <c> true </c> if no work was performed; otherwise, <c> false </c>. </returns>
	private static bool IsJobWithNoWorkPerformed(TResponse response) => JobResult.NoWorkPerformed.Equals(response);

	/// <summary>
	///     Logs a failure that occurred while publishing an audit event.
	/// </summary>
	/// <param name="activityAudited"> The audited activity associated with the failure. </param>
	/// <param name="exception"> The exception that occurred during publishing. </param>
	private void LogFailure(IActivityAudited activityAudited, Exception exception)
	{
		var dictionary = new Dictionary<string, object>
		{
			{ nameof(CorrelationId), activityAudited.CorrelationId }, { nameof(TenantId), activityAudited.TenantId }
		};

		using (logger.BeginScope(dictionary))
		{
			logger.LogCritical(
				exception,
				"[AUDIT]==> {Error} occurred while publishing an ActivityAudited event! The event will be queued to the Outbox. \n{ApplicationName}/{ActivityName}?u={UserName}\n[AUDIT]<== ERROR 500: {Message}",
				exception.GetType().Name,
				activityAudited.ApplicationName,
				activityAudited.ActivityName,
				activityAudited.UserName,
				exception.Message);
		}
	}

	/// <summary>
	///     Saves an audited activity to the outbox for eventual consistency.
	/// </summary>
	/// <param name="activityAudited"> The audited activity to save to the outbox. </param>
	private async Task SaveToOutbox(IActivityAudited activityAudited, CancellationToken cancellationToken)
	{
		var message = new OutboxMessage
		{
			MessageId = Uuid7Extensions.GenerateString(),
			MessageBody = activityAudited,
			MessageHeaders = new Dictionary<string, string>
			{
				{
					ExcaliburHeaderNames.RaisedBy, context.AccessToken() != null
						? System.Text.Json.JsonSerializer.Serialize(new RaisedBy(context.AccessToken()))
						: "Unknown"
				},
				{ ExcaliburHeaderNames.CorrelationId, activityAudited.CorrelationId.ToString() },
				{ ExcaliburHeaderNames.TenantId, activityAudited.TenantId }
			}
		};

		_ = await outbox.SaveMessagesAsync([message], cancellationToken).ConfigureAwait(false);
	}
}

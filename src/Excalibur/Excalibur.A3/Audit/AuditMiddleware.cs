// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using System.Text.Json;

using Excalibur.A3.Audit.Events;
using Excalibur.A3.Diagnostics;
using Excalibur.Application.Requests;
using Excalibur.Application.Requests.Jobs;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Domain;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ApiException = Excalibur.Dispatch.ApiException;
using ExcaliburHeaderNames = Excalibur.Application.ExcaliburHeaderNames;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using OutboxMessage = Excalibur.Outbox.OutboxMessage;
using TenantId = Excalibur.Dispatch.TenantId;

namespace Excalibur.A3.Audit;

/// <summary>
/// Dispatch middleware that records audit information for <see cref="IAmAuditable" /> actions.
/// </summary>
/// <param name="auditMessagePublisher"> Publishes the completed audit record. </param>
/// <param name="outbox"> Receives the audit record when publishing fails. </param>
/// <param name="scopeFactory"> Creates a scope for an auditable action dispatched without one. </param>
/// <param name="logger"> Records a failed publish. </param>
/// <remarks>
/// <para>
/// The audit context is resolved from the scope the action is being dispatched in, on every invocation,
/// and is never held in a field. A middleware instance is built once, from the root provider, and lives
/// for the process: a context captured in a constructor would carry the first caller's tenant,
/// correlation id and client address into every audit record the process ever writes. Its registered
/// service lifetime cannot change this, because the instance is materialised once regardless.
/// </para>
/// <para>
/// An auditable action dispatched without a request scope — from a background worker, a console host or
/// a serverless entry point — is still audited. It is recorded under a scope this middleware creates for
/// the invocation, which reads the ambient tenant live rather than inheriting another caller's, and the
/// context is resolved before the action runs, so a composition that cannot produce one fails on the
/// first auditable action instead of dropping its records silently.
/// </para>
/// </remarks>
internal sealed partial class AuditMiddleware(
	IAuditMessagePublisher auditMessagePublisher,
	IOutboxDispatcher outbox,
	IServiceScopeFactory scopeFactory,
	ILogger<AuditMiddleware> logger) : IDispatchMiddleware
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
	[UnconditionalSuppressMessage("Trimming", "IL2046",
		Justification = "This middleware serializes the audited request reflectively; IDispatchMiddleware does not declare that, because a consumer-authored middleware need not reflect. The requirement reaches the consumer at AuditExcaliburBuilderExtensions.AddAudit, which registers this type; this type is internal to that registration.")]
	[UnconditionalSuppressMessage("AOT", "IL3051",
		Justification = "This middleware serializes the audited request reflectively; IDispatchMiddleware does not declare that, because a consumer-authored middleware need not reflect. The requirement reaches the consumer at AuditExcaliburBuilderExtensions.AddAudit, which registers this type; this type is internal to that registration.")]
	[RequiresUnreferencedCode("Audit serializes the audited request with the reflection-based JSON serializer to record it; a trimmed host must leave auditing unregistered or supply a source-generated serializer resolver.")]
	[RequiresDynamicCode("Audit serializes the audited request with the reflection-based JSON serializer to record it, which requires runtime code generation.")]
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		if (message is not IAmAuditable)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// The action's own scope carries its caller: tenant, correlation id, ETag and client address. A
		// provider returned by BuildServiceProvider() is not an IServiceScope while a real scope's
		// provider is, so this accepts the request scope an integration supplies (for example the
		// ASP.NET Core request scope) and rejects the root provider. Resolving a scoped context from the
		// root would pin one caller's tenant for the lifetime of the container, which is the same
		// cross-request leak in a different place.
		var requestScope = (context.RequestServices as IServiceScope)?.ServiceProvider;
		var requestContext = requestScope?.GetService<IActivityContext>();
		if (requestContext is not null)
		{
			AttachCaller(requestContext, requestScope!);

			return await AuditAsync(requestContext, message, context, nextDelegate, cancellationToken).ConfigureAwait(false);
		}

		// No request scope. The action is still auditable and everything the record needs is still
		// available, so it is audited under a scope belonging to this invocation alone: the ambient
		// tenant is read live, and an action running with no tenant established is recorded against the
		// untenanted partition rather than against whichever tenant happened to dispatch first.
		await using var scope = scopeFactory.CreateAsyncScope();
		var ownedContext = scope.ServiceProvider.GetRequiredService<IActivityContext>();

		return await AuditAsync(ownedContext, message, context, nextDelegate, cancellationToken).ConfigureAwait(false);
	}

	private static bool IsJobWithNoWorkPerformed(object? response) => response is JobResult job && JobResult.NoWorkPerformed.Equals(job);

	/// <summary>
	/// Puts the caller's access token on the activity context so the audit record names who acted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The token and the context are resolved from the same scope, so the identity recorded belongs to
	/// the caller whose action is being audited. Without this the record's Login, UserId and UserName all
	/// read "System" and the outbox RaisedBy header reads "Unknown" on every action the process audits,
	/// including one dispatched in a request scope carrying a live token.
	/// </para>
	/// <para>
	/// A token already present wins: a host that established the caller itself is not overwritten.
	/// Resolving the token can fail — the identity it is built from may come from a remote authorization
	/// service — and a failure there must not fail the action being audited, so it is recorded as an
	/// unattributed action and logged rather than thrown.
	/// </para>
	/// </remarks>
	private void AttachCaller(IActivityContext activityContext, IServiceProvider requestScope)
	{
		if (activityContext.ContainsKey(ActivityContextExtensions.AccessTokenKey))
		{
			return;
		}

		try
		{
			if (requestScope.GetService<IAccessToken>() is { } accessToken)
			{
				activityContext.SetValue(ActivityContextExtensions.AccessTokenKey, accessToken);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogAccessTokenUnavailable(ex);
		}
	}

	private async ValueTask<IMessageResult> AuditAsync(
		IActivityContext activityContext,
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
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
					await SaveToOutboxAsync(activityContext, activityAudited, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		return result;
	}

	[LoggerMessage(A3EventId.AuditAccessTokenUnavailable, LogLevel.Warning,
		"[AUDIT]==> The caller's access token could not be resolved for this action. The audit record is "
		+ "written without an actor identity.")]
	private partial void LogAccessTokenUnavailable(Exception exception);

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

	private async Task SaveToOutboxAsync(
		IActivityContext activityContext,
		ActivityAudited activityAudited,
		CancellationToken cancellationToken)
	{
		var headers = new Dictionary<string, string>
			(StringComparer.Ordinal)
			{
				{
					ExcaliburHeaderNames.RaisedBy,
					activityContext.AccessToken() is { } token
						? JsonSerializer.Serialize(new RaisedBy(token), AuditJsonContext.Default.RaisedBy)
						: "Unknown"
				},
				{ ExcaliburHeaderNames.CorrelationId, activityAudited.CorrelationId.ToString() },
			};

		if (activityAudited.TenantId is not null)
		{
			headers.Add(ExcaliburHeaderNames.TenantId, activityAudited.TenantId);
		}

		var message = new OutboxMessage
		{
			MessageId = Uuid7Extensions.GenerateString(),
			MessageType = nameof(ActivityAudited),
			MessageMetadata = JsonSerializer.Serialize(headers, AuditJsonContext.Default.DictionaryStringString),
			MessageBody = JsonSerializer.SerializeToUtf8Bytes(activityAudited, AuditJsonContext.Default.ActivityAudited),
			CreatedAt = DateTimeOffset.UtcNow,
			TenantId = activityAudited.TenantId,
		};

		_ = await outbox.SaveMessagesAsync([message], cancellationToken).ConfigureAwait(false);
	}
}

// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.AuditLogging.Diagnostics;
using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Excalibur.AuditLogging;

/// <summary>
/// Middleware that populates the scoped <see cref="IAuditContext"/> with pipeline context
/// (correlation, actor, tenant, timestamp) before the handler executes.
/// </summary>
/// <remarks>
/// <para>
/// This middleware runs at <see cref="DispatchMiddlewareStage.PreProcessing"/> so that
/// the <see cref="IAuditContext"/> is fully initialized when handlers receive it via DI.
/// Handlers can then call <see cref="IAuditContext.AssertAsync"/> or
/// <see cref="IAuditContext.ObserveAsync"/> without manually constructing audit events.
/// </para>
/// <para>
/// The audit context and the actor provider are resolved from the scope the message is being
/// processed in, on every invocation, and are never held in a field. A middleware instance is
/// built once and lives for the process: an actor provider captured in a constructor would report
/// the first caller's identity on every audit entry the process ever writes, and a context
/// captured there would carry the first caller's correlation id and tenant. Its registered
/// service lifetime cannot change this, because the instance is materialised once regardless.
/// </para>
/// <para>
/// A message dispatched without a request scope is passed through unchanged. The audit context a
/// handler receives in that case belongs to a scope created for the handler, which this middleware
/// has no handle on, so there is nothing here to initialize; resolving one from the root provider
/// instead would bind a single instance for the life of the container and mutate it per message,
/// which is a cross-request identity leak rather than a fix. The gap is logged rather than hidden.
/// </para>
/// <para>
/// Missing providers are handled gracefully:
/// <list type="bullet">
/// <item>No <see cref="IAuditActorProvider"/> registered → ActorId defaults to "system"</item>
/// <item>No tenant in context → TenantId remains null</item>
/// <item>No correlation ID → CorrelationId remains null</item>
/// </list>
/// </para>
/// </remarks>
internal sealed partial class AuditContextMiddleware : IDispatchMiddleware
{
	private readonly ILogger<AuditContextMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditContextMiddleware"/> class.
	/// </summary>
	/// <param name="logger">The logger for diagnostic output.</param>
	public AuditContextMiddleware(ILogger<AuditContextMiddleware> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// A provider returned by BuildServiceProvider() is not an IServiceScope while a real scope's
		// provider is, so this accepts the request scope an integration supplies (for example the
		// ASP.NET Core request scope) and rejects the root provider.
		var scopedServices = (context.RequestServices as IServiceScope)?.ServiceProvider;
		if (scopedServices is null)
		{
			LogNoRequestScope(message.GetType().Name);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var auditContext = scopedServices.GetService<IAuditContext>();
		if (auditContext is not DefaultAuditContext defaultAuditContext)
		{
			// No IAuditContext registered or not our implementation — pass through
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var correlationId = context.CorrelationId;
		var tenantId = context.GetTenantId();

		string? actorId = null;
		var actorProvider = scopedServices.GetService<IAuditActorProvider>();
		if (actorProvider is not null)
		{
			try
			{
				actorId = await actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				LogActorResolutionFailed(ex);
			}
		}

		actorId ??= "system";

		defaultAuditContext.Initialize(correlationId, actorId, tenantId, message.GetType().Name);

		LogAuditContextPopulated(correlationId, actorId, tenantId);

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	[LoggerMessage(AuditLoggingEventId.AuditContextMiddlewarePopulated, LogLevel.Debug,
		"Audit context populated: CorrelationId={CorrelationId}, ActorId={ActorId}, TenantId={TenantId}")]
	private partial void LogAuditContextPopulated(string? correlationId, string actorId, string? tenantId);

	[LoggerMessage(AuditLoggingEventId.AuditActorResolutionFailed, LogLevel.Warning,
		"Failed to resolve audit actor from IAuditActorProvider; defaulting to 'system'")]
	private partial void LogActorResolutionFailed(Exception exception);

	[LoggerMessage(AuditLoggingEventId.AuditContextNoRequestScope, LogLevel.Warning,
		"{MessageType} was dispatched without a request scope, so the audit context could not be "
		+ "initialized. Audit entries recorded by its handler will have no correlation id, tenant or actor.")]
	private partial void LogNoRequestScope(string messageType);
}

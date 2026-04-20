// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Features;
using Excalibur.AuditLogging.Diagnostics;
using Excalibur.Compliance;

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
	private readonly IAuditActorProvider? _actorProvider;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<AuditContextMiddleware> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="AuditContextMiddleware"/> class.
	/// </summary>
	/// <param name="timeProvider">The time provider for timestamps.</param>
	/// <param name="actorProvider">Optional actor provider for resolving the current actor.</param>
	/// <param name="logger">The logger for diagnostic output.</param>
	public AuditContextMiddleware(
		TimeProvider timeProvider,
		IAuditActorProvider? actorProvider,
		ILogger<AuditContextMiddleware> logger)
	{
		_timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_actorProvider = actorProvider;
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

		var auditContext = context.RequestServices.GetService<IAuditContext>();
		if (auditContext is not DefaultAuditContext defaultAuditContext)
		{
			// No IAuditContext registered or not our implementation — pass through
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var correlationId = context.CorrelationId;
		var tenantId = context.GetTenantId();
		var timestamp = _timeProvider.GetUtcNow();

		string? actorId = null;
		if (_actorProvider is not null)
		{
			try
			{
				actorId = await _actorProvider.GetCurrentActorIdAsync(cancellationToken).ConfigureAwait(false);
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
}

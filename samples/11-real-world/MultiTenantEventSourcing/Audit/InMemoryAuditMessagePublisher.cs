// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.A3.Audit;
using Excalibur.A3.Audit.Events;
using Excalibur.Domain;

using Microsoft.Extensions.Logging;

namespace MultiTenantEventSourcing.Audit;

/// <summary>
/// Demo <see cref="IAuditMessagePublisher"/> that captures
/// <see cref="ActivityAudited"/> records into <see cref="InMemoryAuditStore"/>.
/// </summary>
/// <remarks>
/// Tenant-scoped commands preserve their <c>TenantId</c> in the audit record
/// via <see cref="IAuditContext.TenantId"/>, so the captured audit can be
/// filtered per tenant in production log sinks.
/// </remarks>
public sealed class InMemoryAuditMessagePublisher : IAuditMessagePublisher
{
	private readonly InMemoryAuditStore _store;
	private readonly ILogger<InMemoryAuditMessagePublisher> _logger;

	public InMemoryAuditMessagePublisher(
		InMemoryAuditStore store,
		ILogger<InMemoryAuditMessagePublisher> logger)
	{
		_store = store;
		_logger = logger;
	}

	/// <inheritdoc />
	public Task PublishAsync<TMessage>(
		TMessage message,
		IActivityContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		cancellationToken.ThrowIfCancellationRequested();

		if (message is ActivityAudited audited)
		{
			_store.Add(audited);
			_logger.LogInformation(
				"AUDIT {Activity} tenant={Tenant} status={Status} correlation={Correlation}",
				audited.ActivityName,
				audited.TenantId,
				audited.StatusCode,
				audited.CorrelationId);
		}

		return Task.CompletedTask;
	}
}

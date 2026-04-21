// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.A3.Audit;
using Excalibur.A3.Audit.Events;
using Excalibur.Domain;

using Microsoft.Extensions.Logging;

namespace FullStackAddExcalibur.Audit;

/// <summary>
/// Demo <see cref="IAuditMessagePublisher"/> that captures
/// <see cref="ActivityAudited"/> records into an <see cref="InMemoryAuditStore"/>.
/// </summary>
/// <remarks>
/// Real deployments publish to Kafka, RabbitMQ, SNS, Event Hubs, or an
/// ElasticSearch audit index. The contract is the same in every case:
/// receive the <see cref="ActivityAudited"/> record from the
/// <see cref="AuditMiddleware"/> and move it to durable storage.
/// </remarks>
public sealed class InMemoryAuditMessagePublisher : IAuditMessagePublisher
{
	private readonly InMemoryAuditStore _store;
	private readonly ILogger<InMemoryAuditMessagePublisher> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="InMemoryAuditMessagePublisher"/> class.
	/// </summary>
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
				"AUDIT {Activity} tenant={Tenant} correlation={Correlation} status={Status} user={User}",
				audited.ActivityName,
				audited.TenantId,
				audited.CorrelationId,
				audited.StatusCode,
				audited.UserName);
		}

		return Task.CompletedTask;
	}
}

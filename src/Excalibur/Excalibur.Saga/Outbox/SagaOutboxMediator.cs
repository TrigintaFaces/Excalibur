// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Saga.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Outbox;

/// <summary>
/// Default implementation of <see cref="ISagaOutboxMediator"/> that delegates
/// event publishing to a host-configured outbox delegate.
/// </summary>
internal sealed partial class SagaOutboxMediator : ISagaOutboxMediator
{
	private readonly ILogger<SagaOutboxMediator> _logger;
	private readonly SagaOutboxOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="SagaOutboxMediator"/> class.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="options">The outbox options containing the publish delegate.</param>
	public SagaOutboxMediator(
		ILogger<SagaOutboxMediator> logger,
		IOptions<SagaOutboxOptions> options)
	{
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(options);

		_logger = logger;
		_options = options.Value;
	}

	/// <inheritdoc />
	public async Task PublishThroughOutboxAsync(
		IReadOnlyList<IDomainEvent> events,
		string sagaId,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(events);
		ArgumentException.ThrowIfNullOrEmpty(sagaId);

		if (events.Count == 0)
		{
			return;
		}

		if (_options.PublishDelegate is null)
		{
			LogOutboxDelegateNotConfigured(_logger, sagaId);
			throw new InvalidOperationException(
				"Saga outbox publish delegate is not configured. " +
				"Call WithOutbox(options => options.PublishDelegate = ...) to configure the outbox integration.");
		}

		LogPublishingThroughOutbox(_logger, sagaId, events.Count);

		await _options.PublishDelegate(events, sagaId, cancellationToken).ConfigureAwait(false);

		LogPublishedThroughOutbox(_logger, sagaId, events.Count);
	}

	[LoggerMessage(
		SagaEventId.SagaOutboxPublishing,
		LogLevel.Debug,
		"Publishing {EventCount} events through outbox for saga {SagaId}")]
	private static partial void LogPublishingThroughOutbox(
		ILogger logger, string sagaId, int eventCount);

	[LoggerMessage(
		SagaEventId.SagaOutboxPublished,
		LogLevel.Information,
		"Published {EventCount} events through outbox for saga {SagaId}")]
	private static partial void LogPublishedThroughOutbox(
		ILogger logger, string sagaId, int eventCount);

	[LoggerMessage(
		SagaEventId.SagaOutboxDelegateNotConfigured,
		LogLevel.Error,
		"Saga outbox publish delegate is not configured for saga {SagaId}")]
	private static partial void LogOutboxDelegateNotConfigured(
		ILogger logger, string sagaId);
}
